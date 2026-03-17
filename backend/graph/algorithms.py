"""
Graph Algorithms Engine
=======================
Industry-standard graph analysis algorithms built on networkx.

Capabilities:
  1. BM25 full-text ranking (no external deps, pure Python)
  2. PageRank — node importance scoring
  3. HITS (Hub/Authority)
  4. Betweenness Centrality — bottleneck detection
  5. Closeness Centrality — reachability scoring
  6. Community Detection — Louvain-like greedy modularity (networkx built-in)
  7. K-shortest paths between nodes
  8. Orphan detection (disconnected nodes)
  9. Similarity search (Jaccard on metadata tokens)
 10. Structural health metrics

All results are cached per graph_id and invalidated on scan/update.
"""
from __future__ import annotations

import math
import re
import string
from collections import defaultdict, Counter
from typing import Optional
from functools import lru_cache

import networkx as nx

from backend.graph.models import KnowledgeGraph, GraphNode, EdgeType


# ─── BM25 Implementation (zero-dependency) ───────────────────────────────────

class BM25Index:
    """
    Okapi BM25 full-text index over KnowledgeGraph nodes.
    Indexes: label, content_preview, metadata values.
    """
    K1 = 1.5
    B  = 0.75

    def __init__(self, nodes: list[GraphNode]):
        self._nodes = nodes
        self._ids = [n.id for n in nodes]
        self._docs = [self._tokenize(self._node_text(n)) for n in nodes]
        self._idf = self._build_idf()
        self._avg_dl = sum(len(d) for d in self._docs) / max(len(self._docs), 1)

    @staticmethod
    def _node_text(node: GraphNode) -> str:
        parts = [node.label, node.content_preview or ""]
        for v in (node.metadata or {}).values():
            if isinstance(v, str):
                parts.append(v)
        return " ".join(parts)

    @staticmethod
    def _tokenize(text: str) -> list[str]:
        text = text.lower()
        # Split on whitespace + punctuation, keep CJK chars as unigrams
        tokens = re.findall(r'[\w\u4e00-\u9fff]+', text)
        return tokens

    def _build_idf(self) -> dict[str, float]:
        n = len(self._docs)
        df: dict[str, int] = defaultdict(int)
        for doc in self._docs:
            for t in set(doc):
                df[t] += 1
        return {t: math.log((n - f + 0.5) / (f + 0.5) + 1) for t, f in df.items()}

    def search(self, query: str, top_k: int = 30, node_types: list = None) -> list[dict]:
        q_tokens = self._tokenize(query)
        if not q_tokens:
            return []
        scores = []
        for idx, (doc, node) in enumerate(zip(self._docs, self._nodes)):
            if node_types and node.type not in node_types:
                continue
            dl = len(doc)
            tf_map = Counter(doc)
            score = 0.0
            for t in q_tokens:
                if t not in self._idf:
                    continue
                tf = tf_map.get(t, 0)
                score += self._idf[t] * (
                    tf * (self.K1 + 1) /
                    (tf + self.K1 * (1 - self.B + self.B * dl / self._avg_dl))
                )
            if score > 0:
                scores.append((score, idx))

        scores.sort(reverse=True)
        results = []
        for score, idx in scores[:top_k]:
            n = self._nodes[idx]
            results.append({
                "id": n.id,
                "label": n.label,
                "type": n.type,
                "depth": n.depth,
                "score": round(score, 4),
                "url": n.url,
                "content_preview": (n.content_preview or "")[:200],
                "metadata": n.metadata,
            })
        return results


# ─── Graph Builder Helper ─────────────────────────────────────────────────────

def _build_nx(kg: KnowledgeGraph, edge_types: list[str] = None) -> nx.DiGraph:
    G = nx.DiGraph()
    for n in kg.nodes:
        G.add_node(n.id, label=n.label, type=n.type, depth=n.depth)
    for e in kg.edges:
        if edge_types and e.type not in edge_types:
            continue
        G.add_edge(e.source, e.target, type=e.type, weight=e.weight, label=e.label)
    return G


def _build_undirected(kg: KnowledgeGraph) -> nx.Graph:
    G = nx.Graph()
    for n in kg.nodes:
        G.add_node(n.id, label=n.label, type=n.type)
    for e in kg.edges:
        if G.has_edge(e.source, e.target):
            G[e.source][e.target]['weight'] += e.weight
        else:
            G.add_edge(e.source, e.target, weight=e.weight)
    return G


# ─── Cache (invalidated per graph_id version) ────────────────────────────────

_algo_cache: dict[str, dict] = {}  # graph_id → {algo_name → result}
_bm25_cache: dict[str, BM25Index] = {}


def invalidate_cache(graph_id: str):
    _algo_cache.pop(graph_id, None)
    _bm25_cache.pop(graph_id, None)


def get_bm25(graph_id: str, kg: KnowledgeGraph) -> BM25Index:
    if graph_id not in _bm25_cache:
        _bm25_cache[graph_id] = BM25Index(kg.nodes)
    return _bm25_cache[graph_id]


# ─── Core Algorithms ──────────────────────────────────────────────────────────

class GraphAlgorithms:

    # ── 1. PageRank ────────────────────────────────────────────────────────────
    def pagerank(
        self,
        kg: KnowledgeGraph,
        alpha: float = 0.85,
        max_iter: int = 100,
        top_k: int = 20,
    ) -> list[dict]:
        """
        Rank nodes by importance using PageRank.
        Useful for finding central concepts in the knowledge graph.
        """
        G = _build_nx(kg)
        if len(G) == 0:
            return []
        pr = nx.pagerank(G, alpha=alpha, max_iter=max_iter, weight="weight")
        sorted_pr = sorted(pr.items(), key=lambda x: x[1], reverse=True)
        results = []
        for node_id, score in sorted_pr[:top_k]:
            n = kg.get_node(node_id)
            if n:
                results.append({
                    "id": node_id,
                    "label": n.label,
                    "type": n.type,
                    "pagerank": round(score, 6),
                    "in_degree": G.in_degree(node_id),
                    "out_degree": G.out_degree(node_id),
                })
        return results

    # ── 2. HITS (Hub / Authority) ─────────────────────────────────────────────
    def hits(self, kg: KnowledgeGraph, top_k: int = 20) -> dict:
        """
        HITS algorithm: hub_score (node links to many) + authority_score (many link to node).
        """
        G = _build_nx(kg)
        if len(G) < 2:
            return {"hubs": [], "authorities": []}
        try:
            hubs, auths = nx.hits(G, max_iter=100)
        except nx.PowerIterationFailedConvergence:
            return {"hubs": [], "authorities": []}

        def top_nodes(scores: dict) -> list[dict]:
            return [
                {"id": nid, "label": (kg.get_node(nid) or type("", (), {"label": nid})()).label,
                 "score": round(s, 6)}
                for nid, s in sorted(scores.items(), key=lambda x: x[1], reverse=True)[:top_k]
            ]

        return {"hubs": top_nodes(hubs), "authorities": top_nodes(auths)}

    # ── 3. Betweenness Centrality ─────────────────────────────────────────────
    def betweenness_centrality(self, kg: KnowledgeGraph, top_k: int = 20) -> list[dict]:
        """
        Nodes with high betweenness are bottlenecks / critical bridges.
        If these nodes are removed, graph connectivity drops significantly.
        """
        G = _build_nx(kg)
        if len(G) < 3:
            return []
        bc = nx.betweenness_centrality(G, normalized=True, weight="weight")
        results = []
        for node_id, score in sorted(bc.items(), key=lambda x: x[1], reverse=True)[:top_k]:
            n = kg.get_node(node_id)
            if n:
                results.append({
                    "id": node_id,
                    "label": n.label,
                    "type": n.type,
                    "betweenness": round(score, 6),
                    "degree": G.degree(node_id),
                })
        return results

    # ── 4. Closeness Centrality ───────────────────────────────────────────────
    def closeness_centrality(self, kg: KnowledgeGraph, top_k: int = 20) -> list[dict]:
        """
        High closeness = reachable from many other nodes quickly.
        Good for finding 'hub' documents / concepts.
        """
        G = _build_undirected(kg)
        if len(G) < 2:
            return []
        # Use largest component for meaningful scores
        largest_cc = max(nx.connected_components(G), key=len)
        sub = G.subgraph(largest_cc)
        cc = nx.closeness_centrality(sub)
        results = []
        for node_id, score in sorted(cc.items(), key=lambda x: x[1], reverse=True)[:top_k]:
            n = kg.get_node(node_id)
            if n:
                results.append({
                    "id": node_id,
                    "label": n.label,
                    "type": n.type,
                    "closeness": round(score, 6),
                })
        return results

    # ── 5. Community Detection (Greedy Modularity) ────────────────────────────
    def community_detection(self, kg: KnowledgeGraph) -> list[dict]:
        """
        Group nodes into communities using greedy modularity optimization.
        Each community is a cluster of closely related concepts.
        """
        G = _build_undirected(kg)
        if len(G) < 4:
            return []
        communities = list(nx.community.greedy_modularity_communities(G, weight="weight"))
        result = []
        for i, community in enumerate(communities):
            members = []
            for node_id in community:
                n = kg.get_node(node_id)
                if n:
                    members.append({
                        "id": node_id,
                        "label": n.label,
                        "type": n.type,
                    })
            result.append({
                "community_id": i,
                "size": len(community),
                "members": sorted(members, key=lambda x: x["label"]),
            })
        return sorted(result, key=lambda x: x["size"], reverse=True)

    # ── 6. K-Shortest Paths ───────────────────────────────────────────────────
    def shortest_paths(
        self,
        kg: KnowledgeGraph,
        source_id: str,
        target_id: str,
        k: int = 3,
    ) -> list[dict]:
        """
        Find up to k shortest paths between two nodes.
        Useful for understanding how concepts are related.
        """
        G = _build_nx(kg)
        if source_id not in G or target_id not in G:
            return []
        try:
            paths = list(nx.shortest_simple_paths(G, source_id, target_id))[:k]
        except (nx.NetworkXNoPath, nx.NodeNotFound):
            return []

        results = []
        for path in paths:
            nodes_in_path = []
            for nid in path:
                n = kg.get_node(nid)
                nodes_in_path.append({
                    "id": nid,
                    "label": n.label if n else nid,
                    "type": n.type if n else "",
                })
            # Collect edges along path
            edge_labels = []
            for i in range(len(path) - 1):
                e = next((e for e in kg.edges if e.source == path[i] and e.target == path[i+1]), None)
                edge_labels.append(e.type if e else "→")
            results.append({"length": len(path) - 1, "nodes": nodes_in_path, "edges": edge_labels})
        return results

    # ── 7. Orphan Detection ───────────────────────────────────────────────────
    def find_orphans(self, kg: KnowledgeGraph) -> list[dict]:
        """
        Nodes with no edges (completely disconnected).
        Often indicates indexing errors or stub articles.
        """
        G = _build_undirected(kg)
        orphans = []
        for n in kg.nodes:
            if n.id not in G or G.degree(n.id) == 0:
                orphans.append({
                    "id": n.id,
                    "label": n.label,
                    "type": n.type,
                    "url": n.url,
                })
        return orphans

    # ── 8. Jaccard Similarity (similar nodes) ─────────────────────────────────
    def find_similar(
        self,
        kg: KnowledgeGraph,
        node_id: str,
        top_k: int = 10,
        min_score: float = 0.05,
    ) -> list[dict]:
        """
        Find nodes similar to a given node using Jaccard similarity
        on tokenized label + content_preview.
        """
        target = kg.get_node(node_id)
        if not target:
            return []
        target_tokens = set(re.findall(r'[\w\u4e00-\u9fff]+',
                                        f"{target.label} {target.content_preview or ''}".lower()))
        results = []
        for n in kg.nodes:
            if n.id == node_id:
                continue
            n_tokens = set(re.findall(r'[\w\u4e00-\u9fff]+',
                                       f"{n.label} {n.content_preview or ''}".lower()))
            union = target_tokens | n_tokens
            if not union:
                continue
            jaccard = len(target_tokens & n_tokens) / len(union)
            if jaccard >= min_score:
                results.append({
                    "id": n.id,
                    "label": n.label,
                    "type": n.type,
                    "similarity": round(jaccard, 4),
                })
        results.sort(key=lambda x: x["similarity"], reverse=True)
        return results[:top_k]

    # ── 9. Structural Health Metrics ──────────────────────────────────────────
    def health_metrics(self, kg: KnowledgeGraph) -> dict:
        """
        Comprehensive graph health report for quality monitoring.
        """
        G_dir  = _build_nx(kg)
        G_undir = _build_undirected(kg)
        n_nodes = len(kg.nodes)
        n_edges = len(kg.edges)

        # Density
        density = nx.density(G_dir) if n_nodes > 1 else 0.0

        # Weakly connected components
        wccs = list(nx.weakly_connected_components(G_dir))
        scc_count = nx.number_strongly_connected_components(G_dir)

        # Degree stats
        degrees = [G_dir.degree(n) for n in G_dir.nodes()]
        avg_degree = sum(degrees) / max(len(degrees), 1)
        max_degree_node_id = max(G_dir.nodes(), key=lambda x: G_dir.degree(x)) if G_dir.nodes() else None
        max_degree_node = kg.get_node(max_degree_node_id) if max_degree_node_id else None

        # Node type distribution
        type_dist: dict[str, int] = defaultdict(int)
        for n in kg.nodes:
            type_dist[n.type] += 1

        # Edge type distribution
        edge_dist: dict[str, int] = defaultdict(int)
        for e in kg.edges:
            edge_dist[e.type] += 1

        # Depth distribution
        depth_dist: dict[int, int] = defaultdict(int)
        for n in kg.nodes:
            depth_dist[n.depth] += 1

        # Orphans (no edges at all)
        orphan_count = sum(1 for n in kg.nodes if G_undir.degree(n.id) == 0 if n.id in G_undir)

        # Nodes with no content
        no_content_count = sum(1 for n in kg.nodes if not n.content_preview)

        # Diameter of largest component (expensive, cap at 500 nodes)
        diameter = None
        if n_nodes <= 500 and wccs:
            largest = max(wccs, key=len)
            sub = G_dir.subgraph(largest)
            try:
                diameter = nx.diameter(sub.to_undirected())
            except Exception:
                diameter = None

        return {
            "node_count": n_nodes,
            "edge_count": n_edges,
            "density": round(density, 6),
            "avg_degree": round(avg_degree, 2),
            "max_degree_node": {"id": max_degree_node_id, "label": max_degree_node.label if max_degree_node else None, "degree": G_dir.degree(max_degree_node_id) if max_degree_node_id else 0},
            "weakly_connected_components": len(wccs),
            "strongly_connected_components": scc_count,
            "orphan_nodes": orphan_count,
            "nodes_without_content": no_content_count,
            "diameter": diameter,
            "node_type_distribution": dict(sorted(type_dist.items(), key=lambda x: x[1], reverse=True)),
            "edge_type_distribution": dict(sorted(edge_dist.items(), key=lambda x: x[1], reverse=True)),
            "depth_distribution": dict(sorted(depth_dist.items())),
            "largest_component_size": len(max(wccs, key=len)) if wccs else 0,
            "coverage_score": round(1 - no_content_count / max(n_nodes, 1), 3),
            "connectivity_score": round(len(max(wccs, key=len)) / max(n_nodes, 1), 3) if wccs else 0,
        }

    # ── 10. Subgraph Extraction with Filters ──────────────────────────────────
    def filtered_subgraph(
        self,
        kg: KnowledgeGraph,
        node_types: list[str] = None,
        edge_types: list[str] = None,
        min_depth: int = None,
        max_depth: int = None,
        contains_text: str = None,
        has_url: bool = None,
        min_confidence: float = None,
        max_nodes: int = 500,
    ) -> dict:
        """
        Extract a subgraph matching multiple filter criteria.
        Returns filtered nodes + edges as a plain dict (graph format).
        """
        filtered_nodes = []
        for n in kg.nodes:
            if node_types and n.type not in node_types:
                continue
            if min_depth is not None and n.depth < min_depth:
                continue
            if max_depth is not None and n.depth > max_depth:
                continue
            if contains_text:
                haystack = f"{n.label} {n.content_preview or ''}".lower()
                if contains_text.lower() not in haystack:
                    continue
            if has_url is not None:
                if has_url and not n.url:
                    continue
                if not has_url and n.url:
                    continue
            filtered_nodes.append(n)
            if len(filtered_nodes) >= max_nodes:
                break

        node_ids = {n.id for n in filtered_nodes}
        filtered_edges = [
            e for e in kg.edges
            if e.source in node_ids and e.target in node_ids
            and (not edge_types or e.type in edge_types)
        ]

        return {
            "node_count": len(filtered_nodes),
            "edge_count": len(filtered_edges),
            "nodes": [
                {
                    "id": n.id, "label": n.label, "type": n.type,
                    "depth": n.depth, "url": n.url,
                    "content_preview": (n.content_preview or "")[:150],
                    "metadata": n.metadata,
                }
                for n in filtered_nodes
            ],
            "edges": [
                {"id": e.id, "source": e.source, "target": e.target,
                 "type": e.type, "label": e.label, "weight": e.weight}
                for e in filtered_edges
            ],
        }


# ─── Singleton ────────────────────────────────────────────────────────────────

graph_algorithms = GraphAlgorithms()
