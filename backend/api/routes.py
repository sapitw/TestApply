"""
FastAPI REST API routes for Feishu Knowledge Graph system.
"""
import asyncio
import json
import uuid
from fastapi import APIRouter, HTTPException, BackgroundTasks
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
from typing import Optional
from loguru import logger

from backend.feishu.client import feishu_client
from backend.feishu.parser import document_parser
from backend.graph.builder import graph_builder
from backend.graph.models import KnowledgeGraph
from backend.feishu.scanner import (
    deep_scanner, ScanJob, ScanEvent,
    get_scan_jobs, get_scan_job, get_tracker,
    _scan_jobs,
)
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


async def _auto_temporal_sync(graph_id: str):
    """Fire-and-forget: temporal sync + disk persistence after indexing / scanning."""
    try:
        from backend.api.temporal import sync_graph as _sync
        from backend.graph.persistence import save_graph, save_temporal
        from backend.graph.temporal import get_temporal_store
        from backend.mcp.server import _graph_store
        from backend.graph.algorithms import invalidate_cache
        # Temporal diff
        await _sync(graph_id)
        logger.info(f"Temporal auto-sync completed for graph {graph_id[:8]}")
        # Persist to disk
        kg = _graph_store.get(graph_id)
        if kg:
            save_graph(kg)
        ts = get_temporal_store(graph_id)
        if ts:
            save_temporal(ts)
        # Invalidate algorithm cache
        invalidate_cache(graph_id)
        logger.info(f"Graph {graph_id[:8]} persisted to disk")
    except Exception as e:
        logger.warning(f"Post-index tasks failed for {graph_id[:8]}: {e}")


@router.post("/documents/index")
async def index_document(req: IndexRequest, background_tasks: BackgroundTasks):
    """
    Index a Feishu document and build its knowledge graph.
    Returns graph_id for subsequent queries.
    Auto-triggers temporal sync in background.
    """
    result_str = await tool_index_feishu_document(req.url, req.recursive)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=400, detail=result["error"])
    background_tasks.add_task(_auto_temporal_sync, result["graph_id"])
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
    """Remove a graph from memory and disk."""
    store = get_graph_store()
    if graph_id not in store:
        raise HTTPException(status_code=404, detail="Graph not found")
    del store[graph_id]
    from backend.graph.persistence import delete_graph_files
    from backend.graph.algorithms import invalidate_cache
    delete_graph_files(graph_id)
    invalidate_cache(graph_id)
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


# ─── Deep Scan routes ─────────────────────────────────────────────────────────

class ScanRequest(BaseModel):
    url: str                        # Feishu root URL or raw token
    root_type: str = "auto"         # "folder" | "wiki_space" | "auto"
    clear_visited: bool = False     # Reset visited tracker before this scan


@router.post("/scan")
async def start_scan(req: ScanRequest, background_tasks: BackgroundTasks):
    """
    Start a deep recursive scan from a Feishu root path.
    Returns a job_id immediately; use /scan/{job_id}/events for SSE progress.
    """
    job_id = str(uuid.uuid4())

    # Determine root type from URL if "auto"
    root_type = req.root_type
    root_token = req.url
    if req.url.startswith("http"):
        try:
            info = feishu_client.parse_feishu_url(req.url)
            root_token = info["token"]
            if root_type == "auto":
                root_type = info["type"]
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"Cannot parse URL: {e}")
    elif root_type == "auto":
        root_type = "folder"  # default assumption for raw tokens

    if req.clear_visited:
        get_tracker().clear()

    job = ScanJob(
        id=job_id,
        root_token=root_token,
        root_type=root_type,
    )
    _scan_jobs[job_id] = job

    # Event queue for streaming
    event_queue: asyncio.Queue[ScanEvent] = asyncio.Queue()

    def on_event(ev: ScanEvent):
        try:
            event_queue.put_nowait(ev)
        except asyncio.QueueFull:
            pass

    async def run_scan():
        try:
            kg = await deep_scanner.scan(req.url, job, on_event=on_event)
            # Store the merged graph
            from backend.mcp.server import store_graph
            store_graph(kg)
            job.graph_id = kg.id
            # Auto temporal sync
            await _auto_temporal_sync(kg.id)
        except Exception as e:
            job.status = "error"
            job.error = str(e)
            logger.exception(f"Scan job {job_id} failed")
        finally:
            # Signal stream end
            await event_queue.put(None)

    background_tasks.add_task(run_scan)

    # Store event queue on job for SSE endpoint
    _scan_event_queues[job_id] = event_queue

    return {
        "job_id": job_id,
        "root_token": root_token,
        "root_type": root_type,
        "status": "started",
        "sse_url": f"/api/scan/{job_id}/events",
    }


# { job_id: asyncio.Queue }
_scan_event_queues: dict[str, asyncio.Queue] = {}


@router.get("/scan/{job_id}/events")
async def scan_events(job_id: str):
    """
    SSE stream for scan progress.
    Connect to this endpoint to receive real-time scan events.

    Event types:
    - start    : scan started
    - found    : a document was discovered
    - skip     : document already visited, skipped
    - indexed  : document successfully indexed
    - error    : error processing a document
    - done     : scan complete (stream ends)
    """
    job = get_scan_job(job_id)
    if not job:
        raise HTTPException(status_code=404, detail=f"Scan job {job_id} not found")

    queue = _scan_event_queues.get(job_id)

    async def event_generator():
        # First, replay already-emitted events
        for ev in job.events:
            yield ev.to_sse()

        if job.status in ("done", "error"):
            # Job already finished, just close
            return

        # Stream new events
        if queue:
            while True:
                try:
                    ev = await asyncio.wait_for(queue.get(), timeout=60.0)
                    if ev is None:
                        break
                    yield ev.to_sse()
                    if ev.type == "done":
                        break
                except asyncio.TimeoutError:
                    # Keep-alive ping
                    yield ": ping\n\n"

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",
            "Connection": "keep-alive",
        },
    )


@router.get("/scan/{job_id}")
async def get_scan_status(job_id: str):
    """Get the current status of a scan job."""
    job = get_scan_job(job_id)
    if not job:
        raise HTTPException(status_code=404, detail=f"Scan job {job_id} not found")
    return {
        "job_id": job.id,
        "status": job.status,
        "root_token": job.root_token,
        "root_type": job.root_type,
        "graph_id": job.graph_id,
        "total_found": job.total_found,
        "total_indexed": job.total_indexed,
        "total_skipped": job.total_skipped,
        "error": job.error,
        "started_at": job.started_at,
        "finished_at": job.finished_at,
    }


@router.get("/scan")
async def list_scans():
    """List all scan jobs."""
    jobs = get_scan_jobs()
    return {
        "jobs": [
            {
                "job_id": j.id,
                "status": j.status,
                "root_token": j.root_token,
                "root_type": j.root_type,
                "graph_id": j.graph_id,
                "total_found": j.total_found,
                "total_indexed": j.total_indexed,
                "total_skipped": j.total_skipped,
                "started_at": j.started_at,
                "finished_at": j.finished_at,
            }
            for j in jobs.values()
        ],
        "total": len(jobs),
    }


@router.delete("/scan/{job_id}")
async def delete_scan_job(job_id: str):
    """Remove a scan job from memory."""
    if job_id not in _scan_jobs:
        raise HTTPException(status_code=404, detail="Scan job not found")
    del _scan_jobs[job_id]
    _scan_event_queues.pop(job_id, None)
    return {"message": f"Scan job {job_id} deleted"}


# ─── Visited tracker routes ───────────────────────────────────────────────────

@router.get("/visited")
async def get_visited():
    """List all visited (already-scanned) page tokens."""
    tracker = get_tracker()
    return {
        "count": tracker.count,
        "visited": tracker.all_visited(),
    }


@router.delete("/visited")
async def clear_visited():
    """Clear the visited tracker — next scan will re-index all pages."""
    get_tracker().clear()
    return {"message": "Visited tracker cleared."}


@router.delete("/visited/{token}")
async def remove_visited(token: str):
    """Remove a specific token from the visited tracker so it gets re-scanned."""
    tracker = get_tracker()
    if not tracker.has_visited(token):
        raise HTTPException(status_code=404, detail=f"Token {token} not in visited set")
    # Remove by clearing and re-adding all except the target
    all_v = tracker.all_visited()
    tracker.clear()
    for t, info in all_v.items():
        if t != token:
            tracker.mark_visited(t, info["title"], info["type"])
    return {"message": f"Token {token} removed from visited set."}
