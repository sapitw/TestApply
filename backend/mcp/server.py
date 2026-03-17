"""
MCP (Model Context Protocol) server for Feishu Knowledge Graph.

Exposes tools that allow other AI agents to:
1. Read and index Feishu documents
2. Query the knowledge graph
3. Get document structure and content
4. Analyze code dependencies (upstream/downstream)
5. Generate training/maintenance plans

Usage:
    python -m backend.mcp.server  (stdio transport for Claude Desktop)
    or via FastAPI /mcp endpoint  (HTTP transport)
"""
import json
import asyncio
import uuid
from typing import Any
from loguru import logger

try:
    from mcp.server import Server
    from mcp.server.stdio import stdio_server
    from mcp import types as mcp_types
    MCP_AVAILABLE = True
except ImportError:
    MCP_AVAILABLE = False
    logger.warning("MCP package not available; MCP server will be disabled.")

from backend.feishu.client import feishu_client
from backend.feishu.parser import document_parser
from backend.graph.builder import graph_builder
from backend.graph.models import KnowledgeGraph, EdgeType

# In-memory graph store (replace with Redis/DB for production)
_graph_store: dict[str, KnowledgeGraph] = {}


# ─── Tool Definitions ──────────────────────────────────────────────────────────

TOOLS = [
    {
        "name": "scan_feishu_root",
        "description": (
            "Deep recursive scan of an entire Feishu root path (folder or wiki space). "
            "Automatically traverses ALL layers, indexes every document, and builds a "
            "merged knowledge graph. Already-scanned pages are skipped automatically. "
            "Returns graph_id when complete. Use this instead of index_feishu_document "
            "when you want to index an entire knowledge base."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": (
                        "Feishu root URL or token. "
                        "Examples: https://xxx.feishu.cn/wiki/SPACE_TOKEN  "
                        "or  https://xxx.feishu.cn/drive/folder/FOLDER_TOKEN"
                    ),
                },
                "clear_visited": {
                    "type": "boolean",
                    "description": "If true, clears the visited cache so all pages are re-scanned.",
                    "default": False,
                },
            },
            "required": ["url"],
        },
    },
    {
        "name": "get_visited_pages",
        "description": (
            "List all Feishu pages that have already been scanned and cached. "
            "Use this to see what is already indexed before running a new scan."
        ),
        "inputSchema": {"type": "object", "properties": {}},
    },
    {
        "name": "clear_visited_cache",
        "description": "Clear the visited page cache so the next scan re-indexes all pages.",
        "inputSchema": {"type": "object", "properties": {}},
    },
    {
        "name": "index_feishu_document",
        "description": (
            "Read a Feishu document by URL and build a knowledge graph. "
            "Supports all document types: Docx, Wiki, Sheet, Bitable, Folder. "
            "Returns a graph_id to reference in subsequent queries."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "Feishu document URL (e.g. https://xxx.feishu.cn/docx/TOKEN)",
                },
                "recursive": {
                    "type": "boolean",
                    "description": "For wiki spaces, recursively index all child pages.",
                    "default": False,
                },
            },
            "required": ["url"],
        },
    },
    {
        "name": "get_graph_overview",
        "description": (
            "Get a high-level overview of a knowledge graph: title, root node, "
            "total nodes, total edges, and top-level children."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "graph_id": {"type": "string", "description": "Graph ID from index_feishu_document"},
            },
            "required": ["graph_id"],
        },
    },
    {
        "name": "get_node_subtree",
        "description": (
            "Get a node and its entire subtree (children, grandchildren, etc.) "
            "from the knowledge graph."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "graph_id": {"type": "string"},
                "node_id": {"type": "string", "description": "Node ID to expand"},
                "max_depth": {"type": "integer", "default": 3, "description": "Max depth to traverse"},
            },
            "required": ["graph_id", "node_id"],
        },
    },
    {
        "name": "search_graph",
        "description": "Search the knowledge graph for nodes matching a keyword or concept.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "graph_id": {"type": "string"},
                "query": {"type": "string", "description": "Search keyword"},
                "node_types": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "Filter by node types (optional)",
                },
            },
            "required": ["graph_id", "query"],
        },
    },
    {
        "name": "get_code_dependencies",
        "description": (
            "Analyze code blocks in the knowledge graph to find upstream/downstream "
            "dependencies. Useful for understanding code flow and impact analysis."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "graph_id": {"type": "string"},
                "node_id": {"type": "string", "description": "Code block or section node ID"},
                "direction": {
                    "type": "string",
                    "enum": ["upstream", "downstream", "both"],
                    "default": "both",
                },
            },
            "required": ["graph_id"],
        },
    },
    {
        "name": "generate_training_plan",
        "description": (
            "Generate a structured training plan from a knowledge graph. "
            "Organizes document headings and sections into a logical learning path."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "graph_id": {"type": "string"},
                "audience": {
                    "type": "string",
                    "description": "Target audience (e.g. 'new engineers', 'operations team')",
                    "default": "general",
                },
                "format": {
                    "type": "string",
                    "enum": ["outline", "markdown", "json"],
                    "default": "markdown",
                },
            },
            "required": ["graph_id"],
        },
    },
    {
        "name": "generate_maintenance_plan",
        "description": (
            "Generate an operations/maintenance plan from a knowledge graph. "
            "Extracts TODOs, processes, and action items."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "graph_id": {"type": "string"},
                "scope": {
                    "type": "string",
                    "description": "Scope filter (e.g. 'database', 'deployment', 'monitoring')",
                    "default": "all",
                },
            },
            "required": ["graph_id"],
        },
    },
    {
        "name": "list_graphs",
        "description": "List all indexed knowledge graphs with their metadata.",
        "inputSchema": {
            "type": "object",
            "properties": {},
        },
    },
    {
        "name": "export_graph",
        "description": "Export the knowledge graph in various formats (JSON, Markdown outline, Mermaid diagram).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "graph_id": {"type": "string"},
                "format": {
                    "type": "string",
                    "enum": ["json", "markdown", "mermaid"],
                    "default": "markdown",
                },
            },
            "required": ["graph_id"],
        },
    },
]


# ─── Tool Implementations ──────────────────────────────────────────────────────

async def tool_scan_feishu_root(url: str, clear_visited: bool = False) -> str:
    """Deep recursive scan of entire Feishu root — all layers, skip already-visited."""
    from backend.feishu.scanner import deep_scanner, ScanJob, get_tracker, _scan_jobs

    if clear_visited:
        get_tracker().clear()

    job_id = str(uuid.uuid4())
    job = ScanJob(id=job_id, root_token=url, root_type="auto")
    _scan_jobs[job_id] = job

    progress_lines: list[str] = []

    def on_event(ev):
        line = f"[{ev.type.upper()}] {ev.doc_type} | {ev.title[:50]} | depth={ev.depth}"
        if ev.type == "skip":
            line += " (SKIP — already indexed)"
        elif ev.type == "indexed":
            line += f" | nodes={ev.message}"
        elif ev.type == "error":
            line += f" | ERROR: {ev.message}"
        progress_lines.append(line)

    try:
        kg = await deep_scanner.scan(url, job, on_event=on_event)
        _graph_store[kg.id] = kg
        return json.dumps({
            "graph_id": kg.id,
            "title": kg.title,
            "node_count": len(kg.nodes),
            "edge_count": len(kg.edges),
            "total_found": job.total_found,
            "total_indexed": job.total_indexed,
            "total_skipped": job.total_skipped,
            "progress_log": progress_lines[-50:],  # last 50 lines
            "message": (
                f"Deep scan complete. "
                f"Found {job.total_found} pages, "
                f"indexed {job.total_indexed}, "
                f"skipped {job.total_skipped} (already cached). "
                f"Graph has {len(kg.nodes)} nodes."
            ),
        }, ensure_ascii=False)
    except Exception as e:
        logger.exception("scan_feishu_root failed")
        return json.dumps({"error": str(e), "progress_log": progress_lines})


async def tool_get_visited_pages() -> str:
    from backend.feishu.scanner import get_tracker
    tracker = get_tracker()
    return json.dumps({
        "count": tracker.count,
        "pages": list(tracker.all_visited().items())[:100],
    }, ensure_ascii=False)


async def tool_clear_visited_cache() -> str:
    from backend.feishu.scanner import get_tracker
    get_tracker().clear()
    return json.dumps({"message": "Visited cache cleared. Next scan will re-index all pages."})


async def tool_index_feishu_document(url: str, recursive: bool = False) -> str:
    try:
        raw = await feishu_client.read_document_by_url(url)
        doc_type = raw.get("type")

        if doc_type == "docx":
            parsed = document_parser.parse_docx(raw)
        elif doc_type == "wiki_space":
            parsed = document_parser.parse_wiki_space(raw)
        elif doc_type == "sheet":
            parsed = document_parser.parse_spreadsheet(raw)
        elif doc_type == "bitable":
            parsed = document_parser.parse_bitable(raw)
        else:
            parsed = raw

        kg = graph_builder.build_from_parsed(parsed, source_url=url)
        _graph_store[kg.id] = kg

        return json.dumps({
            "graph_id": kg.id,
            "title": kg.title,
            "node_count": len(kg.nodes),
            "edge_count": len(kg.edges),
            "root_id": kg.root_id,
            "message": f"Successfully indexed '{kg.title}' with {len(kg.nodes)} nodes.",
        }, ensure_ascii=False)

    except Exception as e:
        logger.exception(f"Error indexing document: {url}")
        return json.dumps({"error": str(e)})


async def tool_get_graph_overview(graph_id: str) -> str:
    kg = _graph_store.get(graph_id)
    if not kg:
        return json.dumps({"error": f"Graph {graph_id} not found. Index a document first."})

    root = kg.get_node(kg.root_id)
    children = kg.get_children(kg.root_id)

    return json.dumps({
        "graph_id": graph_id,
        "title": kg.title,
        "root": root.model_dump() if root else None,
        "total_nodes": len(kg.nodes),
        "total_edges": len(kg.edges),
        "top_level_children": [
            {"id": c.id, "label": c.label, "type": c.type, "children_count": c.children_count}
            for c in children
        ],
    }, ensure_ascii=False)


async def tool_get_node_subtree(graph_id: str, node_id: str, max_depth: int = 3) -> str:
    kg = _graph_store.get(graph_id)
    if not kg:
        return json.dumps({"error": f"Graph {graph_id} not found."})

    def get_subtree(nid: str, depth: int = 0) -> dict:
        node = kg.get_node(nid)
        if not node:
            return {}
        result = node.model_dump()
        if depth < max_depth:
            result["children"] = [get_subtree(c.id, depth + 1) for c in kg.get_children(nid)]
        else:
            result["children"] = []
            result["truncated"] = node.children_count > 0
        return result

    subtree = get_subtree(node_id)
    return json.dumps(subtree, ensure_ascii=False)


async def tool_search_graph(graph_id: str, query: str, node_types: list = None) -> str:
    kg = _graph_store.get(graph_id)
    if not kg:
        return json.dumps({"error": f"Graph {graph_id} not found."})

    query_lower = query.lower()
    results = []
    for node in kg.nodes:
        if node_types and node.type not in node_types:
            continue
        if (query_lower in node.label.lower() or
                query_lower in (node.content_preview or "").lower()):
            results.append({
                "id": node.id,
                "label": node.label,
                "type": node.type,
                "depth": node.depth,
                "content_preview": node.content_preview,
                "url": node.url,
            })

    return json.dumps({
        "query": query,
        "total": len(results),
        "results": results[:50],  # limit to 50 results
    }, ensure_ascii=False)


async def tool_get_code_dependencies(graph_id: str, node_id: str = None, direction: str = "both") -> str:
    kg = _graph_store.get(graph_id)
    if not kg:
        return json.dumps({"error": f"Graph {graph_id} not found."})

    import networkx as nx
    g = nx.DiGraph()
    for e in kg.edges:
        g.add_edge(e.source, e.target, type=e.type)

    from backend.graph.models import NodeType
    code_nodes = [n for n in kg.nodes if n.type == NodeType.CODE_BLOCK]
    if node_id:
        code_nodes = [n for n in code_nodes if n.id == node_id]

    result = {"code_nodes": [], "dependency_map": {}}

    for node in code_nodes:
        entry = {
            "id": node.id,
            "label": node.label,
            "language": node.metadata.get("language", "unknown"),
            "preview": node.content_preview,
        }

        if direction in ("upstream", "both"):
            ancestors = list(nx.ancestors(g, node.id))
            entry["upstream"] = [
                {"id": a, "label": kg.get_node(a).label if kg.get_node(a) else a}
                for a in ancestors
            ]

        if direction in ("downstream", "both"):
            descendants = list(nx.descendants(g, node.id))
            entry["downstream"] = [
                {"id": d, "label": kg.get_node(d).label if kg.get_node(d) else d}
                for d in descendants
            ]

        result["code_nodes"].append(entry)

    return json.dumps(result, ensure_ascii=False)


async def tool_generate_training_plan(graph_id: str, audience: str = "general", fmt: str = "markdown") -> str:
    kg = _graph_store.get(graph_id)
    if not kg:
        return json.dumps({"error": f"Graph {graph_id} not found."})

    from backend.graph.models import NodeType

    # Extract headings in order
    headings = [n for n in kg.nodes if n.type == NodeType.HEADING]
    headings.sort(key=lambda n: (n.depth, kg.nodes.index(n)))

    if fmt == "markdown":
        lines = [f"# Training Plan: {kg.title}", f"\n**Target Audience:** {audience}\n"]
        lines.append("## Learning Objectives\n")
        lines.append(f"This training plan covers the content from **{kg.title}** "
                     f"and is structured to guide {audience} through the material progressively.\n")
        lines.append("## Curriculum\n")
        for h in headings:
            indent = "  " * (h.depth - 1)
            level_marker = "#" * min(h.depth + 1, 6)
            lines.append(f"{level_marker} {h.label}\n")

        code_nodes = [n for n in kg.nodes if n.type == NodeType.CODE_BLOCK]
        if code_nodes:
            lines.append("\n## Hands-on Exercises\n")
            for i, cn in enumerate(code_nodes, 1):
                lang = cn.metadata.get("language", "Code")
                lines.append(f"{i}. **Exercise {i}** ({lang})\n   ```\n   {cn.content_preview[:150]}\n   ```\n")

        return "\n".join(lines)

    elif fmt == "json":
        modules = []
        for h in headings:
            children = kg.get_children(h.id)
            modules.append({
                "module": h.label,
                "level": h.depth,
                "sub_topics": [c.label for c in children if c.type == NodeType.HEADING],
            })
        return json.dumps({"title": kg.title, "audience": audience, "modules": modules}, ensure_ascii=False)

    else:
        lines = [f"Training Plan: {kg.title}"]
        for h in headings:
            lines.append("  " * (h.depth - 1) + f"- {h.label}")
        return "\n".join(lines)


async def tool_generate_maintenance_plan(graph_id: str, scope: str = "all") -> str:
    kg = _graph_store.get(graph_id)
    if not kg:
        return json.dumps({"error": f"Graph {graph_id} not found."})

    from backend.graph.models import NodeType

    todos = [n for n in kg.nodes if n.type == NodeType.TODO]
    headings = [n for n in kg.nodes if n.type == NodeType.HEADING]

    # Filter by scope
    if scope != "all":
        scope_lower = scope.lower()
        headings = [h for h in headings if scope_lower in h.label.lower()]
        todos = [t for t in todos if scope_lower in (t.content_preview or "").lower()]

    lines = [
        f"# Maintenance & Operations Plan: {kg.title}",
        f"\n**Scope:** {scope}\n",
        "## Overview\n",
        f"This plan covers operational procedures from **{kg.title}**.\n",
    ]

    if headings:
        lines.append("## Procedures\n")
        for h in headings:
            lines.append(f"### {h.label}\n")
            children = kg.get_children(h.id)
            for child in children:
                if child.content_preview:
                    lines.append(f"- {child.content_preview[:150]}\n")

    if todos:
        lines.append("## Action Items\n")
        for i, todo in enumerate(todos, 1):
            lines.append(f"{i}. {todo.content_preview or todo.label}\n")

    lines.append("\n## Review Schedule\n")
    lines.append("- [ ] Initial review upon deployment\n")
    lines.append("- [ ] Weekly review during first month\n")
    lines.append("- [ ] Monthly review thereafter\n")

    return "\n".join(lines)


async def tool_list_graphs() -> str:
    graphs = []
    for gid, kg in _graph_store.items():
        graphs.append({
            "graph_id": gid,
            "title": kg.title,
            "node_count": len(kg.nodes),
            "edge_count": len(kg.edges),
        })
    return json.dumps({"graphs": graphs, "total": len(graphs)}, ensure_ascii=False)


async def tool_export_graph(graph_id: str, fmt: str = "markdown") -> str:
    kg = _graph_store.get(graph_id)
    if not kg:
        return json.dumps({"error": f"Graph {graph_id} not found."})

    if fmt == "json":
        return json.dumps(kg.to_dict(), ensure_ascii=False, indent=2)

    elif fmt == "mermaid":
        lines = ["graph TD"]
        for node in kg.nodes[:100]:  # limit for readability
            safe_label = node.label.replace('"', "'").replace("\n", " ")[:40]
            lines.append(f'  {node.id.replace("-", "_")}["{safe_label}"]')
        for edge in kg.edges[:200]:
            src = edge.source.replace("-", "_")
            tgt = edge.target.replace("-", "_")
            lines.append(f"  {src} --> {tgt}")
        return "\n".join(lines)

    else:  # markdown outline
        def render_node(node_id: str, depth: int = 0) -> list[str]:
            node = kg.get_node(node_id)
            if not node:
                return []
            icon = node.icon or "•"
            indent = "  " * depth
            lines_out = [f"{indent}- {icon} **{node.label}** `[{node.type}]`"]
            if node.content_preview:
                lines_out.append(f"{indent}  > {node.content_preview[:100]}")
            for child in kg.get_children(node_id):
                lines_out.extend(render_node(child.id, depth + 1))
            return lines_out

        lines = [f"# {kg.title}\n"]
        lines.extend(render_node(kg.root_id))
        return "\n".join(lines)


# ─── MCP Server Setup ──────────────────────────────────────────────────────────

def create_mcp_server():
    """Create and configure the MCP server instance."""
    if not MCP_AVAILABLE:
        raise RuntimeError("MCP package is not installed. Run: pip install mcp")

    server = Server("feishu-knowledge-graph")

    @server.list_tools()
    async def list_tools():
        return [mcp_types.Tool(**t) for t in TOOLS]

    @server.call_tool()
    async def call_tool(name: str, arguments: dict) -> list[mcp_types.TextContent]:
        try:
            if name == "scan_feishu_root":
                result = await tool_scan_feishu_root(**arguments)
            elif name == "get_visited_pages":
                result = await tool_get_visited_pages()
            elif name == "clear_visited_cache":
                result = await tool_clear_visited_cache()
            elif name == "index_feishu_document":
                result = await tool_index_feishu_document(**arguments)
            elif name == "get_graph_overview":
                result = await tool_get_graph_overview(**arguments)
            elif name == "get_node_subtree":
                result = await tool_get_node_subtree(**arguments)
            elif name == "search_graph":
                result = await tool_search_graph(**arguments)
            elif name == "get_code_dependencies":
                result = await tool_get_code_dependencies(**arguments)
            elif name == "generate_training_plan":
                result = await tool_generate_training_plan(
                    arguments["graph_id"],
                    arguments.get("audience", "general"),
                    arguments.get("format", "markdown"),
                )
            elif name == "generate_maintenance_plan":
                result = await tool_generate_maintenance_plan(**arguments)
            elif name == "list_graphs":
                result = await tool_list_graphs()
            elif name == "export_graph":
                result = await tool_export_graph(**arguments)
            else:
                result = json.dumps({"error": f"Unknown tool: {name}"})
        except Exception as e:
            logger.exception(f"Error calling tool {name}")
            result = json.dumps({"error": str(e)})

        return [mcp_types.TextContent(type="text", text=result)]

    return server


async def run_stdio_server():
    """Run MCP server over stdio (for Claude Desktop integration)."""
    server = create_mcp_server()
    async with stdio_server() as (read_stream, write_stream):
        await server.run(read_stream, write_stream, server.create_initialization_options())


# ─── Shared store for HTTP API access ──────────────────────────────────────────

def get_graph_store() -> dict[str, KnowledgeGraph]:
    return _graph_store


def store_graph(kg: KnowledgeGraph):
    _graph_store[kg.id] = kg


if __name__ == "__main__":
    asyncio.run(run_stdio_server())
