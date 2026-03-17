"""
Deep recursive scanner for Feishu documents.

Features:
- Accepts a root folder token or wiki space token
- Recursively traverses ALL layers (folders, wiki nodes, linked docs)
- Tracks visited page tokens → skips already-scanned pages
- Emits progress events for real-time frontend streaming
- Incrementally builds and merges knowledge graphs
- Persists visited set across scans (optional Redis, otherwise in-memory)
"""
import asyncio
import uuid
from dataclasses import dataclass, field
from datetime import datetime
from typing import AsyncIterator, Callable, Optional
from loguru import logger

from backend.feishu.client import feishu_client
from backend.feishu.parser import document_parser
from backend.graph.builder import graph_builder
from backend.graph.models import KnowledgeGraph, EdgeType


# ─── Progress Event ───────────────────────────────────────────────────────────

@dataclass
class ScanEvent:
    """Single progress event emitted during scanning."""
    type: str           # "start" | "found" | "skip" | "indexed" | "error" | "done"
    token: str = ""
    title: str = ""
    doc_type: str = ""
    depth: int = 0
    total_found: int = 0
    total_indexed: int = 0
    total_skipped: int = 0
    message: str = ""
    timestamp: str = field(default_factory=lambda: datetime.utcnow().isoformat())

    def to_sse(self) -> str:
        """Format as Server-Sent Events data line."""
        import json
        data = {k: v for k, v in self.__dict__.items()}
        return f"data: {json.dumps(data, ensure_ascii=False)}\n\n"


# ─── Visited Tracker ─────────────────────────────────────────────────────────

class VisitedTracker:
    """
    Tracks which Feishu tokens have already been scanned.
    Prevents re-scanning in the same session and across scans.
    """

    def __init__(self):
        # { token: {"title": str, "type": str, "scanned_at": str} }
        self._visited: dict[str, dict] = {}

    def has_visited(self, token: str) -> bool:
        return token in self._visited

    def mark_visited(self, token: str, title: str, doc_type: str):
        self._visited[token] = {
            "title": title,
            "type": doc_type,
            "scanned_at": datetime.utcnow().isoformat(),
        }

    def get_info(self, token: str) -> Optional[dict]:
        return self._visited.get(token)

    def clear(self):
        self._visited.clear()

    def all_visited(self) -> dict[str, dict]:
        return dict(self._visited)

    @property
    def count(self) -> int:
        return len(self._visited)


# Global tracker (shared across all scans in this process)
_global_tracker = VisitedTracker()


def get_tracker() -> VisitedTracker:
    return _global_tracker


# ─── Scan Job State ───────────────────────────────────────────────────────────

@dataclass
class ScanJob:
    id: str
    root_token: str
    root_type: str          # "folder" | "wiki_space" | "wiki"
    status: str = "pending" # pending | running | done | error
    graph_id: Optional[str] = None
    total_found: int = 0
    total_indexed: int = 0
    total_skipped: int = 0
    events: list[ScanEvent] = field(default_factory=list)
    error: str = ""
    started_at: str = ""
    finished_at: str = ""


# In-memory job store
_scan_jobs: dict[str, ScanJob] = {}


def get_scan_jobs() -> dict[str, ScanJob]:
    return _scan_jobs


def get_scan_job(job_id: str) -> Optional[ScanJob]:
    return _scan_jobs.get(job_id)


# ─── Deep Scanner ─────────────────────────────────────────────────────────────

class DeepScanner:
    """
    Recursively scans a Feishu root path (folder or wiki space),
    indexes every document, and builds a merged knowledge graph.
    """

    def __init__(self, tracker: VisitedTracker = None):
        self.tracker = tracker or _global_tracker

    async def scan(
        self,
        root_url_or_token: str,
        job: ScanJob,
        on_event: Callable[[ScanEvent], None] = None,
    ) -> KnowledgeGraph:
        """
        Main entry point. Returns merged KnowledgeGraph.

        Args:
            root_url_or_token: Feishu URL or raw token
            job: ScanJob to update
            on_event: callback for progress events
        """
        job.status = "running"
        job.started_at = datetime.utcnow().isoformat()

        def emit(ev: ScanEvent):
            ev.total_found = job.total_found
            ev.total_indexed = job.total_indexed
            ev.total_skipped = job.total_skipped
            job.events.append(ev)
            if on_event:
                on_event(ev)
            logger.info(f"[scan:{job.id[:8]}] {ev.type} | {ev.doc_type} | {ev.title[:40]} | depth={ev.depth}")

        # Parse root
        try:
            if root_url_or_token.startswith("http"):
                info = feishu_client.parse_feishu_url(root_url_or_token)
                root_token = info["token"]
                root_type = info["type"]
            else:
                root_token = root_url_or_token
                root_type = job.root_type
        except Exception as e:
            job.status = "error"
            job.error = str(e)
            emit(ScanEvent(type="error", message=str(e)))
            raise

        emit(ScanEvent(
            type="start",
            token=root_token,
            doc_type=root_type,
            message=f"Starting deep scan from {root_type}:{root_token}",
        ))

        # Collect all sub-graphs
        sub_graphs: list[KnowledgeGraph] = []

        if root_type in ("wiki_space", "wiki"):
            await self._scan_wiki_space(root_token, sub_graphs, emit, job, depth=0)
        elif root_type == "folder":
            await self._scan_folder(root_token, sub_graphs, emit, job, depth=0)
        elif root_type in ("docx", "doc"):
            await self._scan_document(root_token, "docx", "", sub_graphs, emit, job, depth=0)
        else:
            logger.warning(f"Root type '{root_type}' — attempting as folder")
            await self._scan_folder(root_token, sub_graphs, emit, job, depth=0)

        # Merge all sub-graphs
        if not sub_graphs:
            emit(ScanEvent(type="done", message="Scan complete — no documents found."))
            job.status = "done"
            job.finished_at = datetime.utcnow().isoformat()
            empty = KnowledgeGraph(id=str(uuid.uuid4()), title="Empty Scan", root_id="")
            return empty

        merged = graph_builder.merge_graphs(sub_graphs, title=f"Knowledge Graph ({job.total_indexed} docs)")
        job.graph_id = merged.id
        job.status = "done"
        job.finished_at = datetime.utcnow().isoformat()

        emit(ScanEvent(
            type="done",
            message=(
                f"Scan complete. "
                f"Indexed: {job.total_indexed}, "
                f"Skipped: {job.total_skipped}, "
                f"Nodes: {len(merged.nodes)}, "
                f"Edges: {len(merged.edges)}"
            ),
        ))

        return merged

    # ─── Wiki Space ─────────────────────────────────────────────────────────

    async def _scan_wiki_space(
        self,
        space_id: str,
        sub_graphs: list,
        emit: Callable,
        job: ScanJob,
        depth: int,
    ):
        """Recursively scan all nodes in a wiki space."""
        try:
            nodes = await feishu_client.get_wiki_tree(space_id)
        except Exception as e:
            emit(ScanEvent(type="error", message=f"Failed to list wiki {space_id}: {e}"))
            return

        # Build parent→children map
        children_map: dict[str, list] = {}
        for node in nodes:
            parent = node.get("parent_node_token", "")
            children_map.setdefault(parent, []).append(node)

        # Find top-level nodes (no parent or parent = space root)
        top_level = children_map.get("", []) + children_map.get(space_id, [])

        await self._scan_wiki_nodes_recursive(
            top_level, children_map, space_id, sub_graphs, emit, job, depth
        )

    async def _scan_wiki_nodes_recursive(
        self,
        nodes: list,
        children_map: dict,
        space_id: str,
        sub_graphs: list,
        emit: Callable,
        job: ScanJob,
        depth: int,
    ):
        for node in nodes:
            obj_type = node.get("obj_type", "doc")
            obj_token = node.get("obj_token", "")
            node_token = node.get("node_token", "")
            title = node.get("title", "Untitled")

            job.total_found += 1
            emit(ScanEvent(type="found", token=obj_token, title=title, doc_type=obj_type, depth=depth))

            # Skip if already visited
            if self.tracker.has_visited(obj_token):
                job.total_skipped += 1
                info = self.tracker.get_info(obj_token)
                emit(ScanEvent(
                    type="skip",
                    token=obj_token,
                    title=title,
                    doc_type=obj_type,
                    depth=depth,
                    message=f"Already scanned at {info.get('scanned_at', '?')}",
                ))
                # Still recurse into children
            else:
                # Mark visited before scanning to prevent loops
                self.tracker.mark_visited(obj_token, title, obj_type)

                if obj_type in ("doc", "docx"):
                    await self._scan_document(obj_token, "docx", title, sub_graphs, emit, job, depth)
                elif obj_type in ("sheet", "spreadsheet"):
                    await self._scan_spreadsheet(obj_token, title, sub_graphs, emit, job, depth)
                elif obj_type == "bitable":
                    await self._scan_bitable(obj_token, title, sub_graphs, emit, job, depth)
                else:
                    logger.debug(f"Wiki node type '{obj_type}' not deeply scanned, adding as stub")
                    job.total_indexed += 1

            # Recurse into children
            children = children_map.get(node_token, [])
            if children:
                await self._scan_wiki_nodes_recursive(
                    children, children_map, space_id, sub_graphs, emit, job, depth + 1
                )

            # Throttle to avoid rate limiting
            await asyncio.sleep(0.15)

    # ─── Folder ─────────────────────────────────────────────────────────────

    async def _scan_folder(
        self,
        folder_token: str,
        sub_graphs: list,
        emit: Callable,
        job: ScanJob,
        depth: int,
    ):
        """Recursively scan a cloud drive folder."""
        try:
            children = await feishu_client.list_folder_children(folder_token)
        except Exception as e:
            emit(ScanEvent(type="error", message=f"Failed to list folder {folder_token}: {e}"))
            return

        for child in children:
            child_token = child.get("token", "")
            child_type = child.get("type", "file")
            child_name = child.get("name", "Untitled")

            job.total_found += 1
            emit(ScanEvent(type="found", token=child_token, title=child_name, doc_type=child_type, depth=depth))

            if self.tracker.has_visited(child_token):
                job.total_skipped += 1
                emit(ScanEvent(type="skip", token=child_token, title=child_name, doc_type=child_type, depth=depth))
                continue

            self.tracker.mark_visited(child_token, child_name, child_type)

            if child_type == "folder":
                await self._scan_folder(child_token, sub_graphs, emit, job, depth + 1)
            elif child_type in ("doc", "docx"):
                await self._scan_document(child_token, "docx", child_name, sub_graphs, emit, job, depth)
            elif child_type == "sheet":
                await self._scan_spreadsheet(child_token, child_name, sub_graphs, emit, job, depth)
            elif child_type == "bitable":
                await self._scan_bitable(child_token, child_name, sub_graphs, emit, job, depth)

            await asyncio.sleep(0.15)

    # ─── Document Types ──────────────────────────────────────────────────────

    async def _scan_document(
        self,
        token: str,
        doc_type: str,
        title: str,
        sub_graphs: list,
        emit: Callable,
        job: ScanJob,
        depth: int,
    ):
        try:
            raw = await feishu_client.get_document_meta(token)
            actual_title = raw.get("title", title or "Untitled")
            full_raw = await feishu_client.read_document_by_url(
                f"https://placeholder.feishu.cn/docx/{token}"
            )
            parsed = document_parser.parse_docx(full_raw)
            kg = graph_builder.build_from_parsed(parsed)
            sub_graphs.append(kg)
            job.total_indexed += 1
            emit(ScanEvent(
                type="indexed",
                token=token,
                title=actual_title,
                doc_type="docx",
                depth=depth,
                message=f"Indexed: {len(kg.nodes)} nodes",
            ))

            # Recursively follow internal Feishu links found in the document
            for link in parsed.get("links", []):
                link_url = link.get("url", "")
                if ("feishu.cn" in link_url or "larksuite.com" in link_url):
                    try:
                        link_info = feishu_client.parse_feishu_url(link_url)
                        linked_token = link_info["token"]
                        if not self.tracker.has_visited(linked_token):
                            self.tracker.mark_visited(linked_token, link.get("text", ""), link_info["type"])
                            job.total_found += 1
                            await self._scan_document(
                                linked_token, link_info["type"],
                                link.get("text", ""), sub_graphs, emit, job, depth + 1
                            )
                    except Exception:
                        pass  # Ignore unparseable links

        except Exception as e:
            emit(ScanEvent(type="error", token=token, title=title, message=str(e)))

    async def _scan_spreadsheet(
        self,
        token: str,
        title: str,
        sub_graphs: list,
        emit: Callable,
        job: ScanJob,
        depth: int,
    ):
        try:
            raw = {"type": "sheet", "token": token,
                   "meta": await feishu_client.get_spreadsheet_meta(token),
                   "sheets": await feishu_client.list_sheets(token)}
            parsed = document_parser.parse_spreadsheet(raw)
            kg = graph_builder.build_from_parsed(parsed)
            sub_graphs.append(kg)
            job.total_indexed += 1
            emit(ScanEvent(type="indexed", token=token, title=parsed["title"], doc_type="sheet", depth=depth))
        except Exception as e:
            emit(ScanEvent(type="error", token=token, title=title, message=str(e)))

    async def _scan_bitable(
        self,
        token: str,
        title: str,
        sub_graphs: list,
        emit: Callable,
        job: ScanJob,
        depth: int,
    ):
        try:
            raw = {"type": "bitable", "token": token,
                   "meta": await feishu_client.get_bitable_meta(token),
                   "tables": await feishu_client.list_bitable_tables(token)}
            parsed = document_parser.parse_bitable(raw)
            kg = graph_builder.build_from_parsed(parsed)
            sub_graphs.append(kg)
            job.total_indexed += 1
            emit(ScanEvent(type="indexed", token=token, title=parsed["title"], doc_type="bitable", depth=depth))
        except Exception as e:
            emit(ScanEvent(type="error", token=token, title=title, message=str(e)))


# Singleton scanner
deep_scanner = DeepScanner()
