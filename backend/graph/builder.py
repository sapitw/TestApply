"""
Knowledge graph builder.
Converts parsed Feishu document structures into a navigable knowledge graph.
"""
import uuid
import networkx as nx
from typing import Optional
from loguru import logger

from backend.graph.models import (
    KnowledgeGraph, GraphNode, GraphEdge,
    NodeType, EdgeType,
)


# Color palette per node type
NODE_COLORS = {
    NodeType.WIKI_SPACE: "#4F46E5",
    NodeType.WIKI_PAGE: "#7C3AED",
    NodeType.DOCUMENT: "#2563EB",
    NodeType.SPREADSHEET: "#059669",
    NodeType.SHEET_TAB: "#10B981",
    NodeType.BITABLE: "#D97706",
    NodeType.BITABLE_TABLE: "#F59E0B",
    NodeType.FOLDER: "#6B7280",
    NodeType.FILE: "#9CA3AF",
    NodeType.HEADING: "#1D4ED8",
    NodeType.SECTION: "#3B82F6",
    NodeType.CODE_BLOCK: "#7C3AED",
    NodeType.TABLE: "#0891B2",
    NodeType.TODO: "#DC2626",
    NodeType.LINK: "#0284C7",
    NodeType.CONCEPT: "#6D28D9",
    NodeType.PROCESS: "#B45309",
    NodeType.ENTITY: "#065F46",
}

NODE_ICONS = {
    NodeType.WIKI_SPACE: "📚",
    NodeType.WIKI_PAGE: "📄",
    NodeType.DOCUMENT: "📝",
    NodeType.SPREADSHEET: "📊",
    NodeType.SHEET_TAB: "📋",
    NodeType.BITABLE: "🗃️",
    NodeType.BITABLE_TABLE: "📑",
    NodeType.FOLDER: "📁",
    NodeType.FILE: "📎",
    NodeType.HEADING: "🔖",
    NodeType.SECTION: "📌",
    NodeType.CODE_BLOCK: "💻",
    NodeType.TABLE: "📐",
    NodeType.TODO: "✅",
    NodeType.LINK: "🔗",
    NodeType.CONCEPT: "💡",
    NodeType.PROCESS: "⚙️",
}


class KnowledgeGraphBuilder:
    """Build a KnowledgeGraph from parsed Feishu document data."""

    def build_from_parsed(self, parsed: dict, source_url: str = "") -> KnowledgeGraph:
        """
        Entry point. Dispatch to type-specific builder.
        """
        doc_type = parsed.get("type", "unknown")
        graph_id = str(uuid.uuid4())
        title = parsed.get("title", "Knowledge Graph")

        kg = KnowledgeGraph(id=graph_id, title=title, root_id="")

        if doc_type == "docx":
            root_id = self._build_docx(kg, parsed, source_url)
        elif doc_type == "wiki_space":
            root_id = self._build_wiki_space(kg, parsed, source_url)
        elif doc_type == "sheet":
            root_id = self._build_spreadsheet(kg, parsed, source_url)
        elif doc_type == "bitable":
            root_id = self._build_bitable(kg, parsed, source_url)
        elif doc_type == "folder":
            root_id = self._build_folder(kg, parsed, source_url)
        else:
            root_id = self._make_node(kg, title, NodeType.DOCUMENT, parsed.get("token", ""), source_url, depth=0)

        kg.root_id = root_id
        self._compute_sizes(kg)
        logger.info(f"Built knowledge graph: {len(kg.nodes)} nodes, {len(kg.edges)} edges")
        return kg

    def _make_node(
        self,
        kg: KnowledgeGraph,
        label: str,
        node_type: NodeType,
        token: str = "",
        url: str = "",
        depth: int = 0,
        content_preview: str = "",
        metadata: dict = None,
        node_id: str = None,
    ) -> str:
        nid = node_id or f"{node_type}-{token or uuid.uuid4().hex[:8]}"
        node = GraphNode(
            id=nid,
            label=label[:120],  # truncate long titles
            type=node_type,
            depth=depth,
            token=token,
            url=url,
            content_preview=content_preview[:300] if content_preview else "",
            metadata=metadata or {},
            color=NODE_COLORS.get(node_type, "#6B7280"),
            icon=NODE_ICONS.get(node_type, "📄"),
        )
        kg.nodes.append(node)
        return nid

    def _add_edge(
        self,
        kg: KnowledgeGraph,
        source: str,
        target: str,
        edge_type: EdgeType,
        label: str = "",
    ):
        edge = GraphEdge(
            id=f"{source}->{target}",
            source=source,
            target=target,
            type=edge_type,
            label=label,
        )
        kg.edges.append(edge)

    # ─── Docx ──────────────────────────────────────────────────────────────────

    def _build_docx(self, kg: KnowledgeGraph, parsed: dict, url: str) -> str:
        token = parsed.get("token", "")
        title = parsed.get("title", "Document")
        root_id = self._make_node(kg, title, NodeType.DOCUMENT, token, url, depth=0)

        # Build heading hierarchy
        headings = parsed.get("headings", [])
        self._build_heading_tree(kg, root_id, headings, token, depth=1)

        # Code blocks as children
        for cb in parsed.get("code_blocks", []):
            lang_map = {1: "PlainText", 2: "ABAP", 3: "Ada", 4: "Apache", 5: "Apex",
                        6: "Assembly", 7: "Bash", 8: "CSharp", 9: "C++", 10: "C",
                        11: "COBOL", 12: "CSS", 13: "CoffeeScript", 14: "D", 15: "Dart",
                        16: "Delphi", 49: "Go", 55: "HTML", 56: "JSON", 63: "Java",
                        64: "JavaScript", 65: "Kotlin", 68: "Markdown", 70: "Nginx",
                        73: "PHP", 78: "Python", 80: "R", 82: "Ruby", 83: "Rust",
                        84: "Scala", 85: "Shell", 86: "SQL", 87: "Swift", 88: "TypeScript"}
            lang = lang_map.get(cb.get("language", 1), "Code")
            preview = cb.get("code", "")[:200]
            cb_id = self._make_node(
                kg, f"[{lang}] Code Block", NodeType.CODE_BLOCK,
                cb.get("id", ""), depth=1, content_preview=preview,
                metadata={"language": lang}
            )
            self._add_edge(kg, root_id, cb_id, EdgeType.CONTAINS)

        # External links
        for link in parsed.get("links", []):
            url_val = link.get("url", "")
            # Check if it's a Feishu link
            if "feishu.cn" in url_val or "larksuite.com" in url_val:
                link_id = self._make_node(
                    kg, link.get("text", url_val)[:80],
                    NodeType.LINK, url=url_val, depth=1, metadata={"url": url_val}
                )
                self._add_edge(kg, root_id, link_id, EdgeType.LINKS_TO)

        return root_id

    def _build_heading_tree(self, kg: KnowledgeGraph, parent_id: str, headings: list, doc_token: str, depth: int):
        """Build a nested heading structure respecting H1 > H2 > H3 hierarchy."""
        stack = [(parent_id, 0)]  # (node_id, heading_level)

        for h in headings:
            level = h.get("level", 1)
            text = h.get("text", "")
            hid = h.get("id", uuid.uuid4().hex[:8])

            node_id = f"heading-{hid}"
            self._make_node(
                kg, text, NodeType.HEADING,
                token=hid, depth=depth + level - 1,
                metadata={"heading_level": level},
                node_id=node_id,
            )

            # Pop stack until we find a parent at a lower level
            while len(stack) > 1 and stack[-1][1] >= level:
                stack.pop()

            self._add_edge(kg, stack[-1][0], node_id, EdgeType.CONTAINS)
            stack.append((node_id, level))

    # ─── Wiki Space ────────────────────────────────────────────────────────────

    def _build_wiki_space(self, kg: KnowledgeGraph, parsed: dict, url: str) -> str:
        token = parsed.get("token", "")
        title = parsed.get("title", "Wiki")
        root_id = self._make_node(kg, title, NodeType.WIKI_SPACE, token, url, depth=0)
        self._build_wiki_children(kg, root_id, parsed.get("children", []))
        return root_id

    def _build_wiki_children(self, kg: KnowledgeGraph, parent_id: str, children: list):
        for child in children:
            child_type = NodeType.WIKI_PAGE
            nid = self._make_node(
                kg,
                child.get("title", "Untitled"),
                child_type,
                token=child.get("token", ""),
                depth=child.get("depth", 1),
                metadata={"obj_type": child.get("type")},
            )
            self._add_edge(kg, parent_id, nid, EdgeType.CONTAINS)
            if child.get("children"):
                self._build_wiki_children(kg, nid, child["children"])
                node = kg.get_node(nid)
                if node:
                    node.expandable = True
                    node.children_count = len(child["children"])

    # ─── Spreadsheet ───────────────────────────────────────────────────────────

    def _build_spreadsheet(self, kg: KnowledgeGraph, parsed: dict, url: str) -> str:
        token = parsed.get("token", "")
        title = parsed.get("title", "Spreadsheet")
        root_id = self._make_node(kg, title, NodeType.SPREADSHEET, token, url, depth=0)
        for sheet in parsed.get("sheets", []):
            nid = self._make_node(
                kg,
                sheet.get("title", "Sheet"),
                NodeType.SHEET_TAB,
                token=sheet.get("id", ""),
                depth=1,
                metadata={"row_count": sheet.get("row_count"), "column_count": sheet.get("column_count")},
            )
            self._add_edge(kg, root_id, nid, EdgeType.CONTAINS)
        return root_id

    # ─── Bitable ───────────────────────────────────────────────────────────────

    def _build_bitable(self, kg: KnowledgeGraph, parsed: dict, url: str) -> str:
        token = parsed.get("token", "")
        title = parsed.get("title", "Bitable")
        root_id = self._make_node(kg, title, NodeType.BITABLE, token, url, depth=0)
        for table in parsed.get("children", []):
            nid = self._make_node(
                kg,
                table.get("title", "Table"),
                NodeType.BITABLE_TABLE,
                token=table.get("id", ""),
                depth=1,
            )
            self._add_edge(kg, root_id, nid, EdgeType.CONTAINS)
        return root_id

    # ─── Folder ────────────────────────────────────────────────────────────────

    def _build_folder(self, kg: KnowledgeGraph, parsed: dict, url: str) -> str:
        token = parsed.get("token", "")
        root_id = self._make_node(kg, "Folder", NodeType.FOLDER, token, url, depth=0)
        for child in parsed.get("children", []):
            child_name = child.get("name", "File")
            child_type_str = child.get("type", "file")
            child_type = NodeType.FOLDER if child_type_str == "folder" else NodeType.FILE
            nid = self._make_node(kg, child_name, child_type, token=child.get("token", ""), depth=1)
            self._add_edge(kg, root_id, nid, EdgeType.CONTAINS)
        return root_id

    def _compute_sizes(self, kg: KnowledgeGraph):
        """Compute visual node sizes based on number of children (root = largest)."""
        g = nx.DiGraph()
        for n in kg.nodes:
            g.add_node(n.id)
        for e in kg.edges:
            if e.type == EdgeType.CONTAINS:
                g.add_edge(e.source, e.target)

        for node in kg.nodes:
            descendants = nx.descendants(g, node.id)
            node.children_count = len(list(g.successors(node.id)))
            # Size: root=60, leaf=16, scale with descendant count
            node.size = max(16, min(60, 16 + len(descendants)))

    def merge_graphs(self, graphs: list[KnowledgeGraph], title: str = "Merged Knowledge Graph") -> KnowledgeGraph:
        """Merge multiple knowledge graphs into one, connecting roots."""
        merged = KnowledgeGraph(id=str(uuid.uuid4()), title=title, root_id="")

        root_id = self._make_node(merged, title, NodeType.WIKI_SPACE, depth=0)
        merged.root_id = root_id

        for g in graphs:
            # Add all nodes and edges
            merged.nodes.extend(g.nodes)
            merged.edges.extend(g.edges)
            # Connect sub-root to master root
            self._add_edge(merged, root_id, g.root_id, EdgeType.CONTAINS)

        return merged


graph_builder = KnowledgeGraphBuilder()
