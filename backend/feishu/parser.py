"""
Feishu document content parser.
Converts raw Feishu API responses into normalized structured data
suitable for knowledge graph construction.
"""
from typing import Any
from loguru import logger


BLOCK_TYPE_MAP = {
    1: "page",
    2: "text",
    3: "heading1",
    4: "heading2",
    5: "heading3",
    6: "heading4",
    7: "heading5",
    8: "heading6",
    9: "heading7",
    10: "heading8",
    11: "heading9",
    12: "bullet",
    13: "ordered",
    14: "code",
    15: "quote",
    16: "todo",
    17: "bitable",
    18: "callout",
    19: "chat_card",
    20: "diagram",
    21: "divider",
    22: "file",
    23: "grid",
    24: "grid_column",
    25: "iframe",
    26: "image",
    27: "isv",
    28: "mindnote",
    29: "sheet",
    30: "table",
    31: "table_cell",
    32: "view",
    33: "undefined",
    34: "quote_container",
    35: "task",
    36: "okr",
    37: "okr_objective",
    38: "okr_key_result",
    39: "okr_progress",
    40: "add_ons",
    41: "jira_issue",
    42: "wiki_catalog",
    43: "board",
    44: "gallery",
    45: "synced_block",
    47: "heading",
    999: "unknown",
}


class DocumentParser:
    """Parse Feishu document blocks into a normalized tree structure."""

    def parse_docx(self, raw: dict) -> dict:
        """
        Parse a docx document response into a structured node tree.

        Returns:
            {
                "title": str,
                "type": "docx",
                "token": str,
                "children": [NodeDict, ...]
            }
        """
        meta = raw.get("meta", {})
        blocks = raw.get("blocks", [])
        content = raw.get("content", {})

        title = meta.get("title", "Untitled")
        token = raw.get("token", "")

        # Build block map for parent-child resolution
        block_map = {b["block_id"]: b for b in blocks}

        # Find root block (page block, type=1)
        root_block = next((b for b in blocks if b.get("block_type") == 1), None)

        if root_block:
            tree = self._parse_block_tree(root_block, block_map)
        else:
            # Fallback: use raw_content text
            tree = {"type": "text", "content": content.get("content", ""), "children": []}

        return {
            "title": title,
            "type": "docx",
            "token": token,
            "revision_id": meta.get("revision_id"),
            "create_time": meta.get("create_time"),
            "edit_time": meta.get("edit_time"),
            "children": tree.get("children", []),
            "headings": self._extract_headings(blocks),
            "code_blocks": self._extract_code_blocks(blocks),
            "links": self._extract_links(blocks),
        }

    def _parse_block_tree(self, block: dict, block_map: dict, depth: int = 0) -> dict:
        block_type_id = block.get("block_type", 999)
        block_type = BLOCK_TYPE_MAP.get(block_type_id, "unknown")
        block_id = block.get("block_id", "")

        node = {
            "id": block_id,
            "type": block_type,
            "content": self._extract_block_text(block),
            "depth": depth,
            "children": [],
        }

        child_ids = block.get("children", [])
        for child_id in child_ids:
            child_block = block_map.get(child_id)
            if child_block:
                child_node = self._parse_block_tree(child_block, block_map, depth + 1)
                node["children"].append(child_node)

        return node

    def _extract_block_text(self, block: dict) -> str:
        """Extract plain text from a block's rich text elements."""
        text_parts = []
        block_type_id = block.get("block_type", 999)
        block_type = BLOCK_TYPE_MAP.get(block_type_id, "unknown")

        # Try common text content fields
        for key in ["text", "heading1", "heading2", "heading3", "heading4",
                    "heading5", "heading6", "heading7", "heading8", "heading9",
                    "bullet", "ordered", "code", "quote", "todo"]:
            content = block.get(key, {})
            if content and isinstance(content, dict):
                elements = content.get("elements", [])
                for el in elements:
                    tr = el.get("text_run", {})
                    text_parts.append(tr.get("content", ""))
                if text_parts:
                    break

        return "".join(text_parts)

    def _extract_headings(self, blocks: list) -> list:
        """Extract all headings for TOC / graph hierarchy."""
        heading_types = {3, 4, 5, 6, 7, 8, 9, 10, 11}
        headings = []
        for b in blocks:
            if b.get("block_type") in heading_types:
                level = b["block_type"] - 2  # heading1 = type 3, level 1
                text = self._extract_block_text(b)
                if text:
                    headings.append({"level": level, "text": text, "id": b.get("block_id")})
        return headings

    def _extract_code_blocks(self, blocks: list) -> list:
        """Extract code blocks with language info."""
        code_blocks = []
        for b in blocks:
            if b.get("block_type") == 14:  # code
                code_content = b.get("code", {})
                elements = code_content.get("elements", [])
                code_text = "".join(e.get("text_run", {}).get("content", "") for e in elements)
                lang = code_content.get("style", {}).get("language", 1)
                code_blocks.append({
                    "id": b.get("block_id"),
                    "language": lang,
                    "code": code_text,
                })
        return code_blocks

    def _extract_links(self, blocks: list) -> list:
        """Extract all hyperlinks from document."""
        links = []
        for b in blocks:
            for key in ["text", "heading1", "heading2", "heading3", "heading4",
                        "heading5", "heading6", "bullet", "ordered", "quote"]:
                content = b.get(key, {})
                if not isinstance(content, dict):
                    continue
                for el in content.get("elements", []):
                    tr = el.get("text_run", {})
                    link = tr.get("text_element_style", {}).get("link", {})
                    url = link.get("url", "")
                    if url:
                        links.append({"text": tr.get("content", ""), "url": url})
        return links

    def parse_wiki_space(self, raw: dict) -> dict:
        """Parse wiki space with full tree."""
        space = raw.get("space", {})
        tree_items = raw.get("tree", [])

        children = self._build_wiki_tree(tree_items)
        return {
            "title": space.get("name", "Wiki Space"),
            "type": "wiki_space",
            "token": raw.get("token"),
            "description": space.get("description", ""),
            "children": children,
        }

    def _build_wiki_tree(self, items: list, parent_token: str = None) -> list:
        """Build hierarchical wiki tree from flat item list."""
        result = []
        for item in items:
            if item.get("parent_node_token", "") == (parent_token or ""):
                children = self._build_wiki_tree(items, item.get("node_token"))
                result.append({
                    "id": item.get("node_token"),
                    "title": item.get("title", "Untitled"),
                    "type": item.get("obj_type", "unknown"),
                    "token": item.get("obj_token"),
                    "depth": item.get("depth", 0),
                    "children": children,
                })
        return result

    def parse_spreadsheet(self, raw: dict) -> dict:
        """Parse spreadsheet metadata."""
        meta = raw.get("meta", {})
        sheets = raw.get("sheets", [])
        return {
            "title": meta.get("title", "Spreadsheet"),
            "type": "sheet",
            "token": raw.get("token"),
            "sheets": [
                {
                    "id": s.get("sheet_id"),
                    "title": s.get("title"),
                    "index": s.get("index"),
                    "row_count": s.get("grid_properties", {}).get("row_count"),
                    "column_count": s.get("grid_properties", {}).get("column_count"),
                }
                for s in sheets
            ],
            "children": [
                {"id": s.get("sheet_id"), "title": s.get("title"), "type": "sheet_tab", "children": []}
                for s in sheets
            ],
        }

    def parse_bitable(self, raw: dict) -> dict:
        """Parse bitable (multi-dimensional table) metadata."""
        meta = raw.get("meta", {})
        tables = raw.get("tables", [])
        return {
            "title": meta.get("name", "Bitable"),
            "type": "bitable",
            "token": raw.get("token"),
            "children": [
                {"id": t.get("table_id"), "title": t.get("name"), "type": "bitable_table", "children": []}
                for t in tables
            ],
        }


document_parser = DocumentParser()
