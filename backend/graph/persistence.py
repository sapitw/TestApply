"""
Persistence Layer
=================
Saves/loads both KnowledgeGraph and TemporalGraphStore to/from disk (JSON).
Survives application restarts — data is never lost.

Storage layout:
  data/
    graphs/
      {graph_id}.json           ← KnowledgeGraph snapshot
    temporal/
      {graph_id}.temporal.json  ← TemporalGraphStore (all versions + pending)
    index.json                  ← list of all graph IDs + metadata

Auto-save strategy:
  - Triggered after every index/scan/correction
  - load_all() called once on startup to restore in-memory state
"""
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional
from loguru import logger

from backend.graph.models import KnowledgeGraph, GraphNode, GraphEdge, NodeType, EdgeType
from backend.graph.temporal import (
    TemporalGraphStore, TemporalNode, TemporalEdge,
    ChangeRecord, ChangeType, PendingItem, PendingReason, PendingStatus,
    set_temporal_store,
)

# ─── Data directory ───────────────────────────────────────────────────────────

_DATA_DIR = Path(os.environ.get("KG_DATA_DIR", "./data"))
_GRAPHS_DIR = _DATA_DIR / "graphs"
_TEMPORAL_DIR = _DATA_DIR / "temporal"
_INDEX_FILE = _DATA_DIR / "index.json"


def _ensure_dirs():
    _GRAPHS_DIR.mkdir(parents=True, exist_ok=True)
    _TEMPORAL_DIR.mkdir(parents=True, exist_ok=True)


# ─── KnowledgeGraph serialization ────────────────────────────────────────────

def save_graph(kg: KnowledgeGraph):
    _ensure_dirs()
    path = _GRAPHS_DIR / f"{kg.id}.json"
    data = {
        "id": kg.id,
        "title": kg.title,
        "root_id": kg.root_id,
        "metadata": kg.metadata,
        "nodes": [
            {
                "id": n.id, "label": n.label, "type": n.type,
                "depth": n.depth, "token": n.token, "url": n.url,
                "content_preview": n.content_preview,
                "metadata": n.metadata, "color": n.color,
                "size": n.size, "icon": n.icon,
                "expandable": n.expandable, "children_count": n.children_count,
            }
            for n in kg.nodes
        ],
        "edges": [
            {
                "id": e.id, "source": e.source, "target": e.target,
                "type": e.type, "label": e.label, "weight": e.weight,
                "metadata": e.metadata,
            }
            for e in kg.edges
        ],
        "_saved_at": datetime.now(timezone.utc).isoformat(),
    }
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    _update_index(kg.id, kg.title, len(kg.nodes), len(kg.edges))
    logger.debug(f"Graph saved: {path}")


def load_graph(graph_id: str) -> Optional[KnowledgeGraph]:
    path = _GRAPHS_DIR / f"{graph_id}.json"
    if not path.exists():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        nodes = [
            GraphNode(
                id=n["id"], label=n["label"], type=n["type"],
                depth=n.get("depth", 0), token=n.get("token"),
                url=n.get("url"), content_preview=n.get("content_preview"),
                metadata=n.get("metadata", {}), color=n.get("color"),
                size=n.get("size", 20), icon=n.get("icon"),
                expandable=n.get("expandable", False),
                children_count=n.get("children_count", 0),
            )
            for n in data.get("nodes", [])
        ]
        edges = [
            GraphEdge(
                id=e["id"], source=e["source"], target=e["target"],
                type=e["type"], label=e.get("label"), weight=e.get("weight", 1.0),
                metadata=e.get("metadata", {}),
            )
            for e in data.get("edges", [])
        ]
        return KnowledgeGraph(
            id=data["id"], title=data["title"], root_id=data["root_id"],
            nodes=nodes, edges=edges, metadata=data.get("metadata", {}),
        )
    except Exception as e:
        logger.error(f"Failed to load graph {graph_id}: {e}")
        return None


# ─── TemporalGraphStore serialization ────────────────────────────────────────

def _dt(s: Optional[str]) -> Optional[datetime]:
    if not s:
        return None
    try:
        return datetime.fromisoformat(s.replace("Z", "+00:00"))
    except Exception:
        return None


def save_temporal(store: TemporalGraphStore):
    _ensure_dirs()
    path = _TEMPORAL_DIR / f"{store.graph_id}.temporal.json"

    def cr_to_dict(cr: ChangeRecord) -> dict:
        return {
            "id": cr.id, "node_id": cr.node_id, "change_type": cr.change_type,
            "changed_at": cr.changed_at.isoformat(), "field": cr.field,
            "old_value": cr.old_value, "new_value": cr.new_value,
            "source": cr.source, "source_url": cr.source_url,
            "correction_batch_id": cr.correction_batch_id, "note": cr.note,
        }

    def tn_to_dict(n: TemporalNode) -> dict:
        return {
            "id": n.id, "version": n.version, "version_id": n.version_id,
            "previous_version_id": n.previous_version_id,
            "label": n.label, "type": n.type, "depth": n.depth,
            "token": n.token, "url": n.url, "content_preview": n.content_preview,
            "metadata": n.metadata, "color": n.color, "size": n.size,
            "icon": n.icon, "children_count": n.children_count,
            "expandable": n.expandable,
            "valid_from": n.valid_from.isoformat(),
            "valid_until": n.valid_until.isoformat() if n.valid_until else None,
            "is_current": n.is_current, "confidence": n.confidence,
            "is_pending": n.is_pending, "pending_item_id": n.pending_item_id,
            "created_at": n.created_at.isoformat(),
            "updated_at": n.updated_at.isoformat(),
            "source_scan_id": n.source_scan_id,
            "change_history": [cr_to_dict(cr) for cr in n.change_history],
        }

    def te_to_dict(e: TemporalEdge) -> dict:
        return {
            "id": e.id, "version": e.version, "version_id": e.version_id,
            "source": e.source, "target": e.target, "type": e.type,
            "label": e.label, "weight": e.weight, "metadata": e.metadata,
            "valid_from": e.valid_from.isoformat(),
            "valid_until": e.valid_until.isoformat() if e.valid_until else None,
            "is_current": e.is_current, "confidence": e.confidence,
            "is_pending": e.is_pending, "source_scan_id": e.source_scan_id,
            "change_history": [cr_to_dict(cr) for cr in e.change_history],
        }

    def pi_to_dict(pi: PendingItem) -> dict:
        return {
            "id": pi.id, "node_id": pi.node_id, "graph_id": pi.graph_id,
            "reason": pi.reason, "description": pi.description,
            "context": pi.context, "conflicting_values": pi.conflicting_values,
            "created_at": pi.created_at.isoformat(), "status": pi.status,
            "resolution": pi.resolution,
            "resolved_at": pi.resolved_at.isoformat() if pi.resolved_at else None,
            "resolved_by": pi.resolved_by, "affects_nodes": pi.affects_nodes,
        }

    data = {
        "graph_id": store.graph_id, "title": store.title, "root_id": store.root_id,
        "created_at": store.created_at.isoformat(),
        "last_scanned_at": store.last_scanned_at.isoformat() if store.last_scanned_at else None,
        "nodes": [tn_to_dict(n) for n in store.nodes],
        "edges": [te_to_dict(e) for e in store.edges],
        "pending_items": [pi_to_dict(pi) for pi in store.pending_items],
        "scan_history": store.scan_history,
        "_saved_at": datetime.now(timezone.utc).isoformat(),
    }
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    logger.debug(f"TemporalStore saved: {path}")


def load_temporal(graph_id: str) -> Optional[TemporalGraphStore]:
    path = _TEMPORAL_DIR / f"{graph_id}.temporal.json"
    if not path.exists():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))

        def dict_to_cr(d: dict) -> ChangeRecord:
            return ChangeRecord(
                id=d["id"], node_id=d["node_id"], change_type=d["change_type"],
                changed_at=_dt(d["changed_at"]) or datetime.now(timezone.utc),
                field=d.get("field", ""), old_value=d.get("old_value"),
                new_value=d.get("new_value"), source=d.get("source", ""),
                source_url=d.get("source_url", ""),
                correction_batch_id=d.get("correction_batch_id"),
                note=d.get("note", ""),
            )

        nodes = []
        for n in data.get("nodes", []):
            tn = TemporalNode(
                id=n["id"], version=n.get("version", 1),
                version_id=n.get("version_id", n["id"]),
                previous_version_id=n.get("previous_version_id"),
                label=n["label"], type=n["type"],
                depth=n.get("depth", 0), token=n.get("token"),
                url=n.get("url"), content_preview=n.get("content_preview"),
                metadata=n.get("metadata", {}), color=n.get("color"),
                size=n.get("size", 20), icon=n.get("icon"),
                children_count=n.get("children_count", 0),
                expandable=n.get("expandable", False),
                valid_from=_dt(n.get("valid_from")) or datetime.now(timezone.utc),
                valid_until=_dt(n.get("valid_until")),
                is_current=n.get("is_current", True),
                confidence=n.get("confidence", 1.0),
                is_pending=n.get("is_pending", False),
                pending_item_id=n.get("pending_item_id"),
                created_at=_dt(n.get("created_at")) or datetime.now(timezone.utc),
                updated_at=_dt(n.get("updated_at")) or datetime.now(timezone.utc),
                source_scan_id=n.get("source_scan_id", ""),
                change_history=[dict_to_cr(c) for c in n.get("change_history", [])],
            )
            nodes.append(tn)

        edges = []
        for e in data.get("edges", []):
            te = TemporalEdge(
                id=e["id"], version=e.get("version", 1),
                version_id=e.get("version_id", e["id"]),
                source=e["source"], target=e["target"], type=e["type"],
                label=e.get("label"), weight=e.get("weight", 1.0),
                metadata=e.get("metadata", {}),
                valid_from=_dt(e.get("valid_from")) or datetime.now(timezone.utc),
                valid_until=_dt(e.get("valid_until")),
                is_current=e.get("is_current", True),
                confidence=e.get("confidence", 1.0),
                is_pending=e.get("is_pending", False),
                source_scan_id=e.get("source_scan_id", ""),
                change_history=[dict_to_cr(c) for c in e.get("change_history", [])],
            )
            edges.append(te)

        pending_items = []
        for pi in data.get("pending_items", []):
            pending_items.append(PendingItem(
                id=pi["id"], node_id=pi["node_id"], graph_id=pi.get("graph_id", graph_id),
                reason=pi["reason"], description=pi["description"],
                context=pi.get("context", ""),
                conflicting_values=pi.get("conflicting_values", []),
                created_at=_dt(pi.get("created_at")) or datetime.now(timezone.utc),
                status=pi.get("status", "open"),
                resolution=pi.get("resolution"),
                resolved_at=_dt(pi.get("resolved_at")),
                resolved_by=pi.get("resolved_by", ""),
                affects_nodes=pi.get("affects_nodes", []),
            ))

        store = TemporalGraphStore(
            graph_id=data["graph_id"], title=data["title"], root_id=data["root_id"],
            created_at=_dt(data.get("created_at")) or datetime.now(timezone.utc),
            last_scanned_at=_dt(data.get("last_scanned_at")),
            nodes=nodes, edges=edges, pending_items=pending_items,
            scan_history=data.get("scan_history", []),
        )
        return store
    except Exception as e:
        logger.error(f"Failed to load temporal store {graph_id}: {e}")
        return None


# ─── Index file ───────────────────────────────────────────────────────────────

def _update_index(graph_id: str, title: str, node_count: int, edge_count: int):
    _ensure_dirs()
    index: dict = {}
    if _INDEX_FILE.exists():
        try:
            index = json.loads(_INDEX_FILE.read_text(encoding="utf-8"))
        except Exception:
            index = {}
    index[graph_id] = {
        "title": title,
        "node_count": node_count,
        "edge_count": edge_count,
        "saved_at": datetime.now(timezone.utc).isoformat(),
    }
    _INDEX_FILE.write_text(json.dumps(index, ensure_ascii=False, indent=2), encoding="utf-8")


def load_index() -> dict:
    if not _INDEX_FILE.exists():
        return {}
    try:
        return json.loads(_INDEX_FILE.read_text(encoding="utf-8"))
    except Exception:
        return {}


def delete_graph_files(graph_id: str):
    for path in [
        _GRAPHS_DIR / f"{graph_id}.json",
        _TEMPORAL_DIR / f"{graph_id}.temporal.json",
    ]:
        if path.exists():
            path.unlink()
    index = load_index()
    index.pop(graph_id, None)
    if _INDEX_FILE.exists():
        _INDEX_FILE.write_text(json.dumps(index, ensure_ascii=False, indent=2), encoding="utf-8")


# ─── Startup restore ─────────────────────────────────────────────────────────

def restore_all_on_startup(graph_store: dict) -> tuple[int, int]:
    """
    Load all persisted graphs + temporal stores into in-memory stores.
    Returns (graphs_loaded, temporal_loaded).
    """
    _ensure_dirs()
    index = load_index()
    graphs_loaded = 0
    temporal_loaded = 0

    for graph_id in index:
        if graph_id in graph_store:
            continue  # Already in memory
        kg = load_graph(graph_id)
        if kg:
            graph_store[graph_id] = kg
            graphs_loaded += 1
            logger.info(f"Restored graph {graph_id[:8]} ({kg.title}) — {len(kg.nodes)} nodes")

        ts = load_temporal(graph_id)
        if ts:
            set_temporal_store(ts)
            temporal_loaded += 1
            logger.info(f"Restored temporal store {graph_id[:8]} — {len(ts.nodes)} versioned nodes")

    return graphs_loaded, temporal_loaded
