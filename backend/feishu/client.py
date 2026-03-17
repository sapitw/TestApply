"""
Feishu (Lark) API client supporting all document types:
- Docs (文档)
- Sheets (表格)
- Wiki (知识库)
- Bitable (多维表格)
- Mindnotes (思维笔记)
- Files (文件)
- Folders (文件夹)
"""
import httpx
import asyncio
from typing import Optional, Any
from loguru import logger
from datetime import datetime, timedelta

from backend.config import settings


class FeishuAuthManager:
    """Manages Feishu tenant access token lifecycle."""

    def __init__(self):
        self._token: Optional[str] = None
        self._expires_at: Optional[datetime] = None

    async def get_token(self) -> str:
        if self._token and self._expires_at and datetime.utcnow() < self._expires_at:
            return self._token
        await self._refresh_token()
        return self._token

    async def _refresh_token(self):
        async with httpx.AsyncClient() as client:
            resp = await client.post(
                f"{settings.FEISHU_BASE_URL}/auth/v3/tenant_access_token/internal",
                json={
                    "app_id": settings.FEISHU_APP_ID,
                    "app_secret": settings.FEISHU_APP_SECRET,
                },
                timeout=10,
            )
            resp.raise_for_status()
            data = resp.json()
            if data.get("code") != 0:
                raise RuntimeError(f"Feishu auth failed: {data.get('msg')}")
            self._token = data["tenant_access_token"]
            self._expires_at = datetime.utcnow() + timedelta(seconds=data.get("expire", 7200) - 60)
            logger.info("Feishu access token refreshed.")


auth_manager = FeishuAuthManager()


class FeishuClient:
    """Unified Feishu document reader supporting all document types."""

    BASE = settings.FEISHU_BASE_URL

    async def _headers(self) -> dict:
        token = await auth_manager.get_token()
        return {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}

    async def _get(self, path: str, params: dict = None) -> dict:
        headers = await self._headers()
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.get(f"{self.BASE}{path}", headers=headers, params=params)
            resp.raise_for_status()
            data = resp.json()
            if data.get("code") not in (0, None):
                raise RuntimeError(f"Feishu API error {data.get('code')}: {data.get('msg')}")
            return data

    async def _get_paginated(self, path: str, data_key: str, params: dict = None) -> list:
        """Fetch all pages of a paginated endpoint."""
        results = []
        page_token = None
        while True:
            p = dict(params or {})
            if page_token:
                p["page_token"] = page_token
            data = await self._get(path, p)
            items = data.get("data", {}).get(data_key, [])
            results.extend(items)
            page_token = data.get("data", {}).get("page_token")
            has_more = data.get("data", {}).get("has_more", False)
            if not has_more or not page_token:
                break
        return results

    # ─── Document (Docx) ───────────────────────────────────────────────────────

    async def get_document_meta(self, document_id: str) -> dict:
        """Get document metadata."""
        data = await self._get(f"/docx/v1/documents/{document_id}")
        return data.get("data", {}).get("document", {})

    async def get_document_content(self, document_id: str) -> dict:
        """Get full document content as raw blocks."""
        data = await self._get(f"/docx/v1/documents/{document_id}/raw_content")
        return data.get("data", {})

    async def get_document_blocks(self, document_id: str) -> list:
        """Get document blocks (structured)."""
        return await self._get_paginated(
            f"/docx/v1/documents/{document_id}/blocks",
            "items",
            {"document_revision_id": -1},
        )

    # ─── Wiki (知識庫) ─────────────────────────────────────────────────────────

    async def list_wiki_spaces(self) -> list:
        """List all wiki spaces."""
        return await self._get_paginated("/wiki/v2/spaces", "items", {"page_size": 50})

    async def get_wiki_space(self, space_id: str) -> dict:
        data = await self._get(f"/wiki/v2/spaces/{space_id}")
        return data.get("data", {}).get("space", {})

    async def list_wiki_nodes(self, space_id: str, parent_node_token: str = None) -> list:
        """List wiki nodes (tree structure)."""
        params = {"space_id": space_id, "page_size": 50}
        if parent_node_token:
            params["parent_node_token"] = parent_node_token
        return await self._get_paginated("/wiki/v2/spaces/get_node", "items", params)

    async def get_wiki_node(self, space_id: str, node_token: str) -> dict:
        data = await self._get("/wiki/v2/spaces/get_node", {"space_id": space_id, "token": node_token})
        return data.get("data", {}).get("node", {})

    async def get_wiki_tree(self, space_id: str) -> list:
        """Recursively fetch entire wiki tree."""
        return await self._get_paginated(
            f"/wiki/v2/spaces/{space_id}/nodes",
            "items",
            {"page_size": 50},
        )

    # ─── Sheets (電子表格) ──────────────────────────────────────────────────────

    async def get_spreadsheet_meta(self, spreadsheet_token: str) -> dict:
        data = await self._get(f"/sheets/v3/spreadsheets/{spreadsheet_token}")
        return data.get("data", {}).get("spreadsheet", {})

    async def list_sheets(self, spreadsheet_token: str) -> list:
        data = await self._get(f"/sheets/v3/spreadsheets/{spreadsheet_token}/sheets/query")
        return data.get("data", {}).get("sheets", [])

    async def get_sheet_values(self, spreadsheet_token: str, range_str: str) -> dict:
        """Get cell values from a sheet range (e.g. 'Sheet1!A1:Z100')."""
        data = await self._get(
            f"/sheets/v2/spreadsheets/{spreadsheet_token}/values/{range_str}",
            {"valueRenderOption": "ToString", "dateTimeRenderOption": "FormattedString"},
        )
        return data.get("data", {}).get("valueRange", {})

    # ─── Bitable (多維表格) ────────────────────────────────────────────────────

    async def get_bitable_meta(self, app_token: str) -> dict:
        data = await self._get(f"/bitable/v1/apps/{app_token}")
        return data.get("data", {}).get("app", {})

    async def list_bitable_tables(self, app_token: str) -> list:
        return await self._get_paginated(f"/bitable/v1/apps/{app_token}/tables", "items")

    async def list_bitable_records(self, app_token: str, table_id: str) -> list:
        return await self._get_paginated(
            f"/bitable/v1/apps/{app_token}/tables/{table_id}/records",
            "items",
            {"page_size": 200},
        )

    async def list_bitable_fields(self, app_token: str, table_id: str) -> list:
        return await self._get_paginated(
            f"/bitable/v1/apps/{app_token}/tables/{table_id}/fields",
            "items",
        )

    # ─── Files & Folders ───────────────────────────────────────────────────────

    async def list_folder_children(self, folder_token: str) -> list:
        return await self._get_paginated(
            f"/drive/v1/files",
            "files",
            {"folder_token": folder_token, "page_size": 50},
        )

    async def get_file_meta(self, file_token: str, file_type: str) -> dict:
        data = await self._get(f"/drive/v1/metas", {"request_docs": [{"doc_token": file_token, "doc_type": file_type}]})
        return data.get("data", {})

    async def get_root_folder(self) -> dict:
        data = await self._get("/drive/explorer/v2/root_folder/meta")
        return data.get("data", {})

    # ─── URL Parser ───────────────────────────────────────────────────────────

    def parse_feishu_url(self, url: str) -> dict:
        """
        Parse a Feishu/Lark URL and return document type + token.

        Supported URL patterns:
        - https://xxx.feishu.cn/docx/<token>
        - https://xxx.feishu.cn/sheets/<token>
        - https://xxx.feishu.cn/wiki/<token>
        - https://xxx.feishu.cn/base/<token>
        - https://xxx.feishu.cn/drive/folder/<token>
        - https://xxx.larksuite.com/... (same patterns)
        """
        import re
        url = url.strip().rstrip("/")

        patterns = [
            (r"/docx/([A-Za-z0-9]+)", "docx"),
            (r"/docs/([A-Za-z0-9]+)", "doc"),
            (r"/wiki/([A-Za-z0-9]+)", "wiki"),
            (r"/sheets/([A-Za-z0-9]+)", "sheet"),
            (r"/base/([A-Za-z0-9]+)", "bitable"),
            (r"/drive/folder/([A-Za-z0-9]+)", "folder"),
            (r"/mindnotes/([A-Za-z0-9]+)", "mindnote"),
            (r"/file/([A-Za-z0-9]+)", "file"),
        ]

        for pattern, doc_type in patterns:
            m = re.search(pattern, url)
            if m:
                return {"type": doc_type, "token": m.group(1), "url": url}

        # Try wiki space pattern: /wiki/spaces/<space_id>
        m = re.search(r"/wiki/spaces/([A-Za-z0-9]+)", url)
        if m:
            return {"type": "wiki_space", "token": m.group(1), "url": url}

        raise ValueError(f"Cannot parse Feishu URL: {url}")

    async def read_document_by_url(self, url: str) -> dict:
        """
        Universal entry point: parse URL, read document, return structured content.
        """
        info = self.parse_feishu_url(url)
        doc_type = info["type"]
        token = info["token"]

        logger.info(f"Reading {doc_type} document: {token}")

        if doc_type == "docx":
            meta = await self.get_document_meta(token)
            content = await self.get_document_content(token)
            blocks = await self.get_document_blocks(token)
            return {"type": "docx", "token": token, "meta": meta, "content": content, "blocks": blocks}

        elif doc_type == "wiki":
            node = await self.get_wiki_node("", token)
            return {"type": "wiki", "token": token, "node": node}

        elif doc_type == "wiki_space":
            space = await self.get_wiki_space(token)
            tree = await self.get_wiki_tree(token)
            return {"type": "wiki_space", "token": token, "space": space, "tree": tree}

        elif doc_type == "sheet":
            meta = await self.get_spreadsheet_meta(token)
            sheets = await self.list_sheets(token)
            return {"type": "sheet", "token": token, "meta": meta, "sheets": sheets}

        elif doc_type == "bitable":
            meta = await self.get_bitable_meta(token)
            tables = await self.list_bitable_tables(token)
            return {"type": "bitable", "token": token, "meta": meta, "tables": tables}

        elif doc_type == "folder":
            children = await self.list_folder_children(token)
            return {"type": "folder", "token": token, "children": children}

        else:
            raise ValueError(f"Unsupported document type: {doc_type}")


feishu_client = FeishuClient()
