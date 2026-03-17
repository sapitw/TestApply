"""
Analytics & Advanced Query API
================================
Exposes industry-standard graph analysis to frontend and agents.

Routes:
  GET  /analytics/graphs/{id}/health          — structural health metrics
  GET  /analytics/graphs/{id}/pagerank        — importance ranking
  GET  /analytics/graphs/{id}/hits            — hub / authority scores
  GET  /analytics/graphs/{id}/centrality      — betweenness + closeness
  GET  /analytics/graphs/{id}/communities     — community clusters
  POST /analytics/graphs/{id}/paths           — k-shortest paths
  GET  /analytics/graphs/{id}/orphans         — disconnected nodes
  GET  /analytics/graphs/{id}/similar/{nid}   — similar nodes (Jaccard)
  POST /analytics/graphs/{id}/search          — BM25 full-text search
  POST /analytics/graphs/{id}/filter          — subgraph by criteria
  GET  /analytics/graphs/{id}/inference       — inferred edges preview
  GET  /analytics/graphs/{id}/confidence      — confidence propagation scores
  GET  /analytics/graphs/{id}/inherited-meta  — inherited metadata map

  POST /analytics/graphs/{id}/entity/find-duplicates   — candidate pairs
  POST /analytics/graphs/{id}/entity/merge             — merge two nodes
  POST /analytics/graphs/{id}/entity/split             — split one node
  POST /analytics/graphs/{id}/entity/alias             — register alias
  GET  /analytics/graphs/{id}/entity/{nid}/aliases     — list aliases
"""
from typing import Optional
from fastapi import APIRouter, HTTPException, Query
from pydantic import BaseModel
from loguru import logger

from backend.mcp.server import _graph_store
from backend.graph.algorithms import graph_algorithms, get_bm25, invalidate_cache
from backend.graph.entity_resolver import entity_resolver
from backend.graph.inference import inference_engine
from backend.graph.persistence import save_graph, save_temporal
from backend.graph.temporal import get_temporal_store

router = APIRouter(prefix="/analytics", tags=["Analytics & Advanced Query"])


def _get_kg(graph_id: str):
    kg = _graph_store.get(graph_id)
    if not kg:
        raise HTTPException(status_code=404, detail=f"Graph {graph_id!r} not found")
    return kg


# ─── Request models ───────────────────────────────────────────────────────────

class PathRequest(BaseModel):
    source_id: str
    target_id: str
    k: int = 3

class SearchRequest(BaseModel):
    query: str
    top_k: int = 30
    node_types: Optional[list[str]] = None

class FilterRequest(BaseModel):
    node_types: Optional[list[str]] = None
    edge_types: Optional[list[str]] = None
    min_depth: Optional[int] = None
    max_depth: Optional[int] = None
    contains_text: Optional[str] = None
    has_url: Optional[bool] = None
    max_nodes: int = 500

class MergeRequest(BaseModel):
    canonical_id: str
    duplicate_id: str
    new_label: Optional[str] = None
    note: str = ""

class SplitRequest(BaseModel):
    splits: list[dict]  # [{"label": str, "edge_ids": [str]}]
    note: str = ""

class AliasRequest(BaseModel):
    canonical_id: str
    alias: str

class DuplicateRequest(BaseModel):
    similarity_threshold: float = 0.82
    max_candidates: int = 100


# ─── Health & Stats ───────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/health")
async def graph_health(graph_id: str):
    """Comprehensive structural health report."""
    kg = _get_kg(graph_id)
    return {"graph_id": graph_id, "metrics": graph_algorithms.health_metrics(kg)}


# ─── PageRank ────────────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/pagerank")
async def pagerank(
    graph_id: str,
    alpha: float = Query(0.85, ge=0.5, le=0.99),
    top_k: int = Query(20, ge=1, le=100),
):
    """Rank nodes by importance (PageRank). Higher = more central concept."""
    kg = _get_kg(graph_id)
    return {
        "graph_id": graph_id,
        "alpha": alpha,
        "results": graph_algorithms.pagerank(kg, alpha=alpha, top_k=top_k),
    }


# ─── HITS ─────────────────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/hits")
async def hits(graph_id: str, top_k: int = Query(20, ge=1, le=100)):
    """
    HITS algorithm:
    - Hubs: nodes that link to many good authorities (index pages, TOC)
    - Authorities: nodes linked to by many hubs (core concepts)
    """
    kg = _get_kg(graph_id)
    return {"graph_id": graph_id, **graph_algorithms.hits(kg, top_k=top_k)}


# ─── Centrality ──────────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/centrality")
async def centrality(
    graph_id: str,
    type: str = Query("betweenness", enum=["betweenness", "closeness", "both"]),
    top_k: int = Query(20, ge=1, le=100),
):
    """
    Centrality analysis:
    - betweenness: critical bridges (high = bottleneck, removal disconnects graph)
    - closeness: well-connected nodes (high = reachable from everywhere quickly)
    """
    kg = _get_kg(graph_id)
    result: dict = {"graph_id": graph_id, "type": type}
    if type in ("betweenness", "both"):
        result["betweenness"] = graph_algorithms.betweenness_centrality(kg, top_k=top_k)
    if type in ("closeness", "both"):
        result["closeness"] = graph_algorithms.closeness_centrality(kg, top_k=top_k)
    return result


# ─── Communities ─────────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/communities")
async def communities(graph_id: str):
    """
    Community detection (greedy modularity).
    Groups related nodes into clusters — useful for navigation and topic modeling.
    """
    kg = _get_kg(graph_id)
    communities = graph_algorithms.community_detection(kg)
    return {
        "graph_id": graph_id,
        "community_count": len(communities),
        "communities": communities,
    }


# ─── Shortest Paths ──────────────────────────────────────────────────────────

@router.post("/graphs/{graph_id}/paths")
async def shortest_paths(graph_id: str, req: PathRequest):
    """Find up to k shortest paths between two nodes."""
    kg = _get_kg(graph_id)
    paths = graph_algorithms.shortest_paths(kg, req.source_id, req.target_id, k=req.k)
    if not paths:
        return {"graph_id": graph_id, "paths": [], "message": "No path found between the two nodes"}
    return {"graph_id": graph_id, "source_id": req.source_id, "target_id": req.target_id, "paths": paths}


# ─── Orphans ─────────────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/orphans")
async def orphan_nodes(graph_id: str):
    """Find nodes with no edges — likely indexing errors or stubs."""
    kg = _get_kg(graph_id)
    orphans = graph_algorithms.find_orphans(kg)
    return {"graph_id": graph_id, "count": len(orphans), "orphans": orphans}


# ─── Similar Nodes ───────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/similar/{node_id}")
async def similar_nodes(
    graph_id: str,
    node_id: str,
    top_k: int = Query(10, ge=1, le=50),
    min_score: float = Query(0.05, ge=0.01, le=1.0),
):
    """Find nodes similar to a given node using Jaccard similarity on text tokens."""
    kg = _get_kg(graph_id)
    results = graph_algorithms.find_similar(kg, node_id, top_k=top_k, min_score=min_score)
    return {"graph_id": graph_id, "node_id": node_id, "results": results}


# ─── BM25 Full-Text Search ───────────────────────────────────────────────────

@router.post("/graphs/{graph_id}/search")
async def bm25_search(graph_id: str, req: SearchRequest):
    """
    BM25 full-text ranked search — much more accurate than substring matching.
    Searches: label, content_preview, metadata values.
    """
    kg = _get_kg(graph_id)
    bm25 = get_bm25(graph_id, kg)
    results = bm25.search(req.query, top_k=req.top_k, node_types=req.node_types)
    return {
        "graph_id": graph_id,
        "query": req.query,
        "total": len(results),
        "results": results,
    }


# ─── Subgraph Filter ─────────────────────────────────────────────────────────

@router.post("/graphs/{graph_id}/filter")
async def filter_subgraph(graph_id: str, req: FilterRequest):
    """
    Extract a subgraph matching multiple criteria simultaneously.
    All criteria are ANDed.
    """
    kg = _get_kg(graph_id)
    subgraph = graph_algorithms.filtered_subgraph(
        kg,
        node_types=req.node_types,
        edge_types=req.edge_types,
        min_depth=req.min_depth,
        max_depth=req.max_depth,
        contains_text=req.contains_text,
        has_url=req.has_url,
        max_nodes=req.max_nodes,
    )
    return {"graph_id": graph_id, "filter": req.model_dump(), **subgraph}


# ─── Inference Engine ─────────────────────────────────────────────────────────

@router.get("/graphs/{graph_id}/inference")
async def derived_edges(
    graph_id: str,
    limit: int = Query(200, ge=1, le=1000),
):
    """
    Preview inferred edges (transitive, inverse, symmetric, co-occurrence).
    These are NOT stored — re-derived on demand.
    """
    kg = _get_kg(graph_id)
    derived = inference_engine.derive_all(kg)
    # Group by rule
    by_rule: dict[str, list] = {}
    for e in derived:
        rule = (e.metadata or {}).get("rule", "unknown")
        by_rule.setdefault(rule, []).append({
            "source": e.source,
            "source_label": (kg.get_node(e.source) or type("", (), {"label": e.source})()).label,
            "target": e.target,
            "target_label": (kg.get_node(e.target) or type("", (), {"label": e.target})()).label,
            "type": e.type,
            "weight": e.weight,
        })
    return {
        "graph_id": graph_id,
        "total_inferred": len(derived),
        "by_rule": {rule: {"count": len(edges), "sample": edges[:10]} for rule, edges in by_rule.items()},
        "all_inferred": [
            {"source": e.source, "target": e.target, "type": e.type, "rule": (e.metadata or {}).get("rule"), "weight": e.weight}
            for e in derived[:limit]
        ],
    }


@router.get("/graphs/{graph_id}/confidence")
async def confidence_scores(graph_id: str):
    """Propagated confidence scores for all nodes."""
    kg = _get_kg(graph_id)
    scores = inference_engine.apply_confidence_propagation(kg)
    low_confidence = [(nid, s) for nid, s in scores.items() if s < 0.7]
    low_confidence.sort(key=lambda x: x[1])
    return {
        "graph_id": graph_id,
        "total_nodes": len(scores),
        "low_confidence_count": len(low_confidence),
        "low_confidence_nodes": [
            {"id": nid, "label": (kg.get_node(nid) or type("", (), {"label": nid})()).label, "confidence": s}
            for nid, s in low_confidence[:50]
        ],
        "all_scores": {nid: round(s, 3) for nid, s in scores.items()},
    }


@router.get("/graphs/{graph_id}/inherited-meta")
async def inherited_metadata(graph_id: str):
    """Show which metadata fields are inherited from parent nodes."""
    kg = _get_kg(graph_id)
    inherited = inference_engine.inherit_metadata(kg)
    non_empty = {nid: fields for nid, fields in inherited.items() if fields}
    return {
        "graph_id": graph_id,
        "nodes_with_inherited": len(non_empty),
        "inherited": {
            nid: {
                "node_label": (kg.get_node(nid) or type("", (), {"label": nid})()).label,
                "fields": fields,
            }
            for nid, fields in list(non_empty.items())[:100]
        },
    }


# ─── Entity Resolution ───────────────────────────────────────────────────────

@router.post("/graphs/{graph_id}/entity/find-duplicates")
async def find_duplicates(graph_id: str, req: DuplicateRequest):
    """
    Detect likely duplicate nodes using label edit distance + token overlap + URL identity.
    Returns candidate pairs for human review / merge decision.
    """
    kg = _get_kg(graph_id)
    candidates = entity_resolver.find_duplicates(
        kg,
        similarity_threshold=req.similarity_threshold,
        max_candidates=req.max_candidates,
    )
    return {
        "graph_id": graph_id,
        "threshold": req.similarity_threshold,
        "count": len(candidates),
        "candidates": [
            {
                "node_a_id": c.node_a_id, "node_a_label": c.node_a_label,
                "node_b_id": c.node_b_id, "node_b_label": c.node_b_label,
                "similarity": c.similarity, "reason": c.reason,
            }
            for c in candidates
        ],
    }


@router.post("/graphs/{graph_id}/entity/merge")
async def merge_nodes(graph_id: str, req: MergeRequest):
    """
    Merge a duplicate node into a canonical node.
    All edges redirected; duplicate deprecated; alias registry updated.
    """
    kg = _get_kg(graph_id)
    result = entity_resolver.merge_nodes(
        kg,
        canonical_id=req.canonical_id,
        duplicate_id=req.duplicate_id,
        new_label=req.new_label,
        note=req.note,
    )
    if "error" in result:
        raise HTTPException(status_code=404, detail=result["error"])
    # Persist
    save_graph(kg)
    invalidate_cache(graph_id)
    store = get_temporal_store(graph_id)
    if store:
        save_temporal(store)
    return {"graph_id": graph_id, **result}


@router.post("/graphs/{graph_id}/entity/split")
async def split_node(graph_id: str, node_id: str, req: SplitRequest):
    """
    Split one node into multiple nodes, redistributing edges.
    Original node is deprecated.
    """
    kg = _get_kg(graph_id)
    result = entity_resolver.split_node(kg, node_id=node_id, splits=req.splits, note=req.note)
    if "error" in result:
        raise HTTPException(status_code=404, detail=result["error"])
    save_graph(kg)
    invalidate_cache(graph_id)
    return {"graph_id": graph_id, **result}


@router.post("/graphs/{graph_id}/entity/alias")
async def register_alias(graph_id: str, req: AliasRequest):
    """Register an alias (alternate name) for a node."""
    entity_resolver.register_alias(graph_id, req.canonical_id, req.alias)
    return {
        "graph_id": graph_id,
        "canonical_id": req.canonical_id,
        "alias": req.alias,
        "all_aliases": entity_resolver.get_aliases(graph_id, req.canonical_id),
    }


@router.get("/graphs/{graph_id}/entity/{node_id}/aliases")
async def list_aliases(graph_id: str, node_id: str):
    """List all registered aliases for a node."""
    aliases = entity_resolver.get_aliases(graph_id, node_id)
    kg = _get_kg(graph_id)
    n = kg.get_node(node_id)
    return {
        "graph_id": graph_id,
        "node_id": node_id,
        "canonical_label": n.label if n else None,
        "aliases": aliases,
    }


@router.get("/graphs/{graph_id}/entity/{node_id}/resolve")
async def resolve_alias(graph_id: str, node_id: str, label: str = Query(...)):
    """Look up a label in the alias registry → canonical node ID."""
    canonical_id = entity_resolver.resolve_alias(graph_id, label)
    return {"graph_id": graph_id, "label": label, "canonical_id": canonical_id}
