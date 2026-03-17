"""
FastAPI REST API routes for Feishu Knowledge Graph system.
"""
import json
from fastapi import APIRouter, HTTPException, BackgroundTasks
from pydantic import BaseModel
from typing import Optional
from loguru import logger

from backend.feishu.client import feishu_client
from backend.feishu.parser import document_parser
from backend.graph.builder import graph_builder
from backend.graph.models import KnowledgeGraph
from backend.mcp.server import (
    tool_index_feishu_document,
    tool_get_graph_overview,
    tool_get_node_subtree,
    tool_search_graph,
    tool_get_code_dependencies,
    tool_generate_training_plan,
    tool_generate_maintenance_plan,
    tool_list_graphs,
    tool_export_graph,
    get_graph_store,
    store_graph,
)

router = APIRouter(prefix="/api", tags=["knowledge-graph"])


# ─── Request / Response Models ─────────────────────────────────────────────────

class IndexRequest(BaseModel):
    url: str
    recursive: bool = False


class SearchRequest(BaseModel):
    graph_id: str
    query: str
    node_types: Optional[list[str]] = None


class TrainingPlanRequest(BaseModel):
    graph_id: str
    audience: str = "general"
    format: str = "markdown"


class MaintenancePlanRequest(BaseModel):
    graph_id: str
    scope: str = "all"


class ExportRequest(BaseModel):
    graph_id: str
    format: str = "markdown"


# ─── Routes ───────────────────────────────────────────────────────────────────

@router.get("/health")
async def health():
    return {"status": "ok", "service": "feishu-knowledge-graph"}


@router.post("/documents/index")
async def index_document(req: IndexRequest):
    """
    Index a Feishu document and build its knowledge graph.
    Returns graph_id for subsequent queries.
    """
    result_str = await tool_index_feishu_document(req.url, req.recursive)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=400, detail=result["error"])
    return result


@router.get("/graphs")
async def list_graphs():
    """List all indexed knowledge graphs."""
    result_str = await tool_list_graphs()
    return json.loads(result_str)


@router.get("/graphs/{graph_id}")
async def get_graph(graph_id: str):
    """Get full knowledge graph data."""
    store = get_graph_store()
    kg = store.get(graph_id)
    if not kg:
        raise HTTPException(status_code=404, detail=f"Graph {graph_id} not found")
    return kg.to_dict()


@router.get("/graphs/{graph_id}/overview")
async def get_overview(graph_id: str):
    """Get graph overview with top-level children."""
    result_str = await tool_get_graph_overview(graph_id)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=404, detail=result["error"])
    return result


@router.get("/graphs/{graph_id}/nodes/{node_id}/subtree")
async def get_subtree(graph_id: str, node_id: str, max_depth: int = 3):
    """Get a node and its subtree."""
    result_str = await tool_get_node_subtree(graph_id, node_id, max_depth)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=404, detail=result["error"])
    return result


@router.post("/graphs/search")
async def search(req: SearchRequest):
    """Search the knowledge graph."""
    result_str = await tool_search_graph(req.graph_id, req.query, req.node_types)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=404, detail=result["error"])
    return result


@router.get("/graphs/{graph_id}/code-dependencies")
async def code_dependencies(graph_id: str, node_id: Optional[str] = None, direction: str = "both"):
    """Get code upstream/downstream dependency analysis."""
    result_str = await tool_get_code_dependencies(graph_id, node_id, direction)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=404, detail=result["error"])
    return result


@router.post("/plans/training")
async def generate_training_plan(req: TrainingPlanRequest):
    """Generate a training plan from a knowledge graph."""
    result = await tool_generate_training_plan(req.graph_id, req.audience, req.format)
    if isinstance(result, str) and result.startswith('{"error'):
        err = json.loads(result)
        raise HTTPException(status_code=404, detail=err["error"])
    return {"content": result, "format": req.format}


@router.post("/plans/maintenance")
async def generate_maintenance_plan(req: MaintenancePlanRequest):
    """Generate a maintenance/operations plan."""
    result = await tool_generate_maintenance_plan(req.graph_id, req.scope)
    if isinstance(result, str) and result.startswith('{"error'):
        err = json.loads(result)
        raise HTTPException(status_code=404, detail=err["error"])
    return {"content": result}


@router.post("/graphs/export")
async def export_graph(req: ExportRequest):
    """Export knowledge graph in different formats."""
    result = await tool_export_graph(req.graph_id, req.format)
    return {"content": result, "format": req.format}


@router.delete("/graphs/{graph_id}")
async def delete_graph(graph_id: str):
    """Remove a graph from memory."""
    store = get_graph_store()
    if graph_id not in store:
        raise HTTPException(status_code=404, detail="Graph not found")
    del store[graph_id]
    return {"message": f"Graph {graph_id} deleted"}


# ─── MCP HTTP endpoint (for non-stdio MCP transport) ──────────────────────────

class MCPToolRequest(BaseModel):
    tool: str
    arguments: dict = {}


@router.post("/mcp/call")
async def mcp_call(req: MCPToolRequest):
    """
    HTTP endpoint for MCP tool calls.
    Allows agents to call MCP tools via HTTP without stdio transport.
    """
    tool_map = {
        "index_feishu_document": tool_index_feishu_document,
        "get_graph_overview": tool_get_graph_overview,
        "get_node_subtree": tool_get_node_subtree,
        "search_graph": tool_search_graph,
        "get_code_dependencies": tool_get_code_dependencies,
        "list_graphs": tool_list_graphs,
        "export_graph": tool_export_graph,
    }

    if req.tool == "generate_training_plan":
        args = req.arguments
        result = await tool_generate_training_plan(
            args["graph_id"], args.get("audience", "general"), args.get("format", "markdown")
        )
    elif req.tool == "generate_maintenance_plan":
        result = await tool_generate_maintenance_plan(**req.arguments)
    elif req.tool in tool_map:
        result = await tool_map[req.tool](**req.arguments)
    else:
        raise HTTPException(status_code=400, detail=f"Unknown tool: {req.tool}")

    try:
        return {"result": json.loads(result)}
    except (json.JSONDecodeError, TypeError):
        return {"result": result}
