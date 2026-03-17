"""
Temporal Knowledge Graph API
==============================
Endpoints for versioning, pending review, and correction cascade.

Routes:
  POST /temporal/graphs/{graph_id}/sync    — sync KG snapshot → temporal store
  GET  /temporal/graphs/{graph_id}         — temporal store overview
  GET  /temporal/graphs/{graph_id}/timeline — full change timeline
  GET  /temporal/graphs/{graph_id}/at/{iso_date} — snapshot at a date
  GET  /temporal/graphs/{graph_id}/pending — list pending items
  POST /temporal/graphs/{graph_id}/pending/{id}/resolve — submit correction
  POST /temporal/graphs/{graph_id}/pending/{id}/dismiss
  GET  /temporal/graphs/{graph_id}/nodes/{node_id}/history — version history
  GET  /temporal/graphs/{graph_id}/corrections — correction batches
  GET  /temporal/pending — all open pending items across all graphs
"""
import uuid
from datetime import datetime, timezone
from typing import Optional
from fastapi import APIRouter, HTTPException, Query
from pydantic import BaseModel
from loguru import logger

from backend.mcp.server import _graph_store
from backend.graph.temporal import (
    TemporalGraphStore, PendingStatus,
    get_temporal_store, set_temporal_store, all_temporal_stores,
)
from backend.graph.delta import detect_delta, apply_delta_to_store
from backend.graph.corrector import CorrectionEngine, CorrectionRequest

router = APIRouter(prefix="/temporal", tags=["Temporal Knowledge Graph"])
_correction_engine = CorrectionEngine()


# ─── Request models ───────────────────────────────────────────────────────────

class ResolveRequest(BaseModel):
    correct_value: str
    valid_from: Optional[str] = None     # ISO date or natural language
    valid_until: Optional[str] = None
    note: str = ""
    cascade: bool = True
    submitted_by: str = "human"


class DismissRequest(BaseModel):
    reason: str = ""


# ─── Helper ───────────────────────────────────────────────────────────────────

def _get_store(graph_id: str) -> TemporalGraphStore:
    store = get_temporal_store(graph_id)
    if not store:
        raise HTTPException(
            status_code=404,
            detail=f"Temporal store for graph {graph_id!r} not found. Call POST /temporal/graphs/{graph_id}/sync first."
        )
    return store


# ─── 1. Sync snapshot → temporal store ───────────────────────────────────────

@router.post("/graphs/{graph_id}/sync")
async def sync_graph(graph_id: str):
    """
    Pull the latest KnowledgeGraph snapshot, diff it against the temporal store,
    create change records and pending items, then persist.
    This is called automatically after every scan/index.
    """
    new_graph = _graph_store.get(graph_id)
    if not new_graph:
        raise HTTPException(status_code=404, detail=f"Graph {graph_id} not found")

    store = get_temporal_store(graph_id)
    scan_id = str(uuid.uuid4())

    if store is None:
        # First sync — bootstrap
        store = TemporalGraphStore(
            graph_id=graph_id,
            title=new_graph.title,
            root_id=new_graph.root_id,
        )
        old_graph = None
    else:
        # Reconstruct old KG from current temporal nodes for diffing
        from backend.graph.models import KnowledgeGraph, GraphNode, GraphEdge
        old_graph = KnowledgeGraph(
            id=graph_id, title=store.title, root_id=store.root_id,
            nodes=[
                GraphNode(
                    id=n.id, label=n.label, type=n.type, depth=n.depth,
                    token=n.token or "", url=n.url, content_preview=n.content_preview,
                    metadata=n.metadata, color=n.color or "", size=n.size,
                    icon=n.icon, expandable=n.expandable, children_count=n.children_count,
                )
                for n in store.current_nodes()
            ],
            edges=[
                GraphEdge(
                    id=e.id, source=e.source, target=e.target,
                    type=e.type, label=e.label, weight=e.weight,
                )
                for e in store.current_edges()
            ],
        )

    delta = detect_delta(old_graph, new_graph, scan_id)
    store = apply_delta_to_store(store, new_graph, delta, scan_id)
    set_temporal_store(store)

    logger.info(f"Temporal sync [{graph_id[:8]}]: {delta.summary()}")

    return {
        "graph_id": graph_id,
        "scan_id": scan_id,
        "created": len(delta.created),
        "updated": len(delta.updated),
        "deprecated": len(delta.deprecated),
        "new_pending_items": len(delta.pending_items),
        "total_pending_open": len(store.open_pending()),
        "summary": delta.summary(),
    }


# ─── 2. Temporal store overview ───────────────────────────────────────────────

@router.get("/graphs/{graph_id}")
async def get_temporal_overview(graph_id: str):
    store = _get_store(graph_id)
    pending_open = store.open_pending()
    return {
        "graph_id": graph_id,
        "title": store.title,
        "created_at": store.created_at.isoformat(),
        "last_scanned_at": store.last_scanned_at.isoformat() if store.last_scanned_at else None,
        "total_nodes_all_versions": len(store.nodes),
        "current_nodes": len(store.current_nodes()),
        "total_edges": len(store.current_edges()),
        "pending_open": len(pending_open),
        "pending_resolved": len([p for p in store.pending_items if p.status == PendingStatus.RESOLVED]),
        "scan_count": len(store.scan_history),
        "last_scan": store.scan_history[-1] if store.scan_history else None,
    }


# ─── 3. Full timeline ────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/timeline")
async def get_timeline(
    graph_id: str,
    node_id: Optional[str] = Query(None, description="过滤特定节点的历史（留空显示全部）"),
    limit: int = Query(100, description="最多显示几笔记录"),
):
    """
    Chronological list of all changes in the graph.
    """
    store = _get_store(graph_id)

    all_crs = []
    for node in store.nodes:
        if node_id and node.id != node_id:
            continue
        for cr in node.change_history:
            all_crs.append({
                "node_id": node.id,
                "node_label": node.label,
                "node_type": node.type,
                "change_type": cr.change_type,
                "field": cr.field,
                "old_value": cr.old_value,
                "new_value": cr.new_value,
                "changed_at": cr.changed_at.isoformat(),
                "source": cr.source,
                "correction_batch_id": cr.correction_batch_id,
                "note": cr.note,
                "display": cr.to_display(),
            })

    # Sort newest first
    all_crs.sort(key=lambda x: x["changed_at"], reverse=True)

    return {
        "graph_id": graph_id,
        "node_id": node_id,
        "total": len(all_crs),
        "records": all_crs[:limit],
    }


# ─── 4. Point-in-time snapshot ───────────────────────────────────────────────

@router.get("/graphs/{graph_id}/at/{iso_date}")
async def get_snapshot_at(graph_id: str, iso_date: str):
    """
    Return the knowledge graph as it appeared at a specific date.
    iso_date format: YYYY-MM-DD or YYYY-MM-DDTHH:MM:SS
    """
    store = _get_store(graph_id)
    try:
        dt = datetime.fromisoformat(iso_date.replace("Z", "+00:00"))
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
    except ValueError:
        raise HTTPException(status_code=400, detail=f"Invalid date format: {iso_date}")

    nodes_at = store.nodes_at(dt)
    return {
        "graph_id": graph_id,
        "as_of": iso_date,
        "node_count": len(nodes_at),
        "nodes": [
            {
                "id": n.id, "label": n.label, "type": n.type,
                "version": n.version, "is_pending": n.is_pending,
                "valid_from": n.valid_from.isoformat(),
                "valid_until": n.valid_until.isoformat() if n.valid_until else None,
            }
            for n in nodes_at
        ],
    }


# ─── 5. Pending items ────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/pending")
async def list_pending(
    graph_id: str,
    status: Optional[str] = Query(None, description="open | resolved | dismissed | auto"),
):
    """List pending items (待判断) for a graph."""
    store = _get_store(graph_id)
    items = store.pending_items
    if status:
        items = [p for p in items if p.status == status]

    return {
        "graph_id": graph_id,
        "total": len(items),
        "items": [
            {
                "id": p.id,
                "node_id": p.node_id,
                "reason": p.reason,
                "description": p.description,
                "context": p.context[:200],
                "conflicting_values": p.conflicting_values,
                "created_at": p.created_at.isoformat(),
                "status": p.status,
                "resolution": p.resolution,
                "resolved_at": p.resolved_at.isoformat() if p.resolved_at else None,
                "resolved_by": p.resolved_by,
                "display": p.to_display(),
            }
            for p in items
        ],
    }


@router.get("/pending")
async def list_all_pending():
    """List ALL open pending items across every graph."""
    all_pending = []
    for gid, store in all_temporal_stores().items():
        for p in store.open_pending():
            all_pending.append({
                "graph_id": gid,
                "graph_title": store.title,
                "id": p.id,
                "node_id": p.node_id,
                "reason": p.reason,
                "description": p.description,
                "created_at": p.created_at.isoformat(),
                "status": p.status,
                "display": p.to_display(),
            })
    all_pending.sort(key=lambda x: x["created_at"], reverse=True)
    return {"total": len(all_pending), "items": all_pending}


# ─── 6. Resolve (submit correction + cascade) ────────────────────────────────

@router.post("/graphs/{graph_id}/pending/{pending_id}/resolve")
async def resolve_pending(graph_id: str, pending_id: str, req: ResolveRequest):
    """
    Submit a correction for a pending item.
    If cascade=true, the engine propagates the correction through all related nodes.
    """
    store = _get_store(graph_id)

    pending = store.get_pending(pending_id)
    if not pending:
        raise HTTPException(status_code=404, detail=f"Pending item {pending_id} not found")

    correction = CorrectionRequest(
        pending_id=pending_id,
        correct_value=req.correct_value,
        valid_from=req.valid_from,
        valid_until=req.valid_until,
        note=req.note,
        cascade=req.cascade,
        submitted_by=req.submitted_by,
    )

    result = _correction_engine.apply(store, correction)
    set_temporal_store(store)

    return {
        "batch_id": result.batch_id,
        "target_node_id": result.target_node_id,
        "nodes_updated": result.nodes_updated,
        "pending_resolved": result.pending_resolved,
        "change_records": len(result.change_records),
        "skipped": result.skipped,
        "summary": result.summary(),
    }


@router.post("/graphs/{graph_id}/pending/{pending_id}/dismiss")
async def dismiss_pending(graph_id: str, pending_id: str, req: DismissRequest):
    """Mark a pending item as dismissed (won't trigger cascade)."""
    store = _get_store(graph_id)
    pending = store.get_pending(pending_id)
    if not pending:
        raise HTTPException(status_code=404, detail=f"Pending item {pending_id} not found")

    pending.status = PendingStatus.DISMISSED
    pending.resolution = req.reason or "Dismissed by user"
    pending.resolved_at = datetime.now(timezone.utc)
    pending.resolved_by = "human"

    node = store.get_node(pending.node_id)
    if node:
        node.is_pending = False
    set_temporal_store(store)

    return {"message": f"Pending item {pending_id} dismissed.", "node_id": pending.node_id}


# ─── 7. Node version history ─────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/nodes/{node_id}/history")
async def node_history(graph_id: str, node_id: str):
    """Full version history of a node (all versions, oldest→newest)."""
    store = _get_store(graph_id)
    versions = store.get_node_history(node_id)
    if not versions:
        raise HTTPException(status_code=404, detail=f"Node {node_id} not found")

    return {
        "node_id": node_id,
        "version_count": len(versions),
        "versions": [
            {
                "version": v.version,
                "version_id": v.version_id,
                "label": v.label,
                "is_current": v.is_current,
                "is_pending": v.is_pending,
                "confidence": v.confidence,
                "valid_from": v.valid_from.isoformat(),
                "valid_until": v.valid_until.isoformat() if v.valid_until else None,
                "change_history": [cr.to_display() for cr in v.change_history],
            }
            for v in versions
        ],
    }


# ─── 8. Correction batches ───────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/corrections")
async def list_corrections(graph_id: str, limit: int = Query(50)):
    """List all human corrections (batches) applied to this graph."""
    store = _get_store(graph_id)
    batches: dict[str, list] = {}

    for node in store.nodes:
        for cr in node.change_history:
            if cr.change_type == "corrected" and cr.correction_batch_id:
                batches.setdefault(cr.correction_batch_id, []).append({
                    "node_id": node.id,
                    "node_label": node.label,
                    "field": cr.field,
                    "old_value": cr.old_value,
                    "new_value": cr.new_value,
                    "changed_at": cr.changed_at.isoformat(),
                    "source": cr.source,
                    "note": cr.note,
                })

    result = [
        {"batch_id": bid, "affected_nodes": len(records), "records": records}
        for bid, records in batches.items()
    ]
    result.sort(key=lambda x: x["records"][0]["changed_at"] if x["records"] else "", reverse=True)

    return {"total_batches": len(result), "batches": result[:limit]}


# ─── 9. Temporal stats for all graphs ────────────────────────────────────────

@router.get("/stats")
async def temporal_stats():
    """High-level stats across all temporal stores."""
    stores = all_temporal_stores()
    return {
        "total_graphs": len(stores),
        "graphs": [
            {
                "graph_id": gid,
                "title": s.title,
                "current_nodes": len(s.current_nodes()),
                "pending_open": len(s.open_pending()),
                "scan_count": len(s.scan_history),
                "last_scanned_at": s.last_scanned_at.isoformat() if s.last_scanned_at else None,
            }
            for gid, s in stores.items()
        ],
    }
