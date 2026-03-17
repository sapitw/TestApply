"""
Entity Resolution
=================
Detects, deduplicates, and merges nodes that represent the same real-world entity.

Industry techniques used:
  1. Name normalization + blocking (Soundex-like, CJK token overlap)
  2. Edit distance (Levenshtein) for near-duplicate label detection
  3. Metadata similarity (token overlap on content_preview / metadata values)
  4. URL identity (same Feishu token → same entity)
  5. Merge operation: consolidate two nodes into one canonical node,
     redirect all edges, preserve version history
  6. Alias tracking: multiple labels point to same canonical ID
  7. Split operation: separate a node that was incorrectly merged

All operations produce ChangeRecords and integrate with the temporal store.
"""
from __future__ import annotations

import re
import uuid
from dataclasses import dataclass, field
from typing import Optional
from loguru import logger

from backend.graph.models import KnowledgeGraph, GraphNode, GraphEdge
from backend.graph.temporal import (
    TemporalGraphStore, ChangeRecord, ChangeType,
    get_temporal_store,
)


# ─── String Utilities (zero-dependency) ──────────────────────────────────────

def _normalize(s: str) -> str:
    """Lowercase, collapse whitespace, strip punctuation."""
    s = s.lower().strip()
    s = re.sub(r'[\s\-_/\\\.]+', ' ', s)
    return s


def _tokenize(s: str) -> set[str]:
    return set(re.findall(r'[\w\u4e00-\u9fff]+', s.lower()))


def levenshtein(a: str, b: str) -> int:
    """Standard Levenshtein edit distance."""
    if a == b:
        return 0
    if len(a) < len(b):
        a, b = b, a
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a):
        curr = [i + 1]
        for j, cb in enumerate(b):
            curr.append(min(prev[j + 1] + 1, curr[j] + 1, prev[j] + (ca != cb)))
        prev = curr
    return prev[-1]


def edit_similarity(a: str, b: str) -> float:
    """1 - normalized edit distance."""
    a, b = _normalize(a), _normalize(b)
    max_len = max(len(a), len(b), 1)
    return 1.0 - levenshtein(a, b) / max_len


def token_jaccard(a: str, b: str) -> float:
    ta, tb = _tokenize(a), _tokenize(b)
    union = ta | tb
    if not union:
        return 0.0
    return len(ta & tb) / len(union)


def combined_similarity(a: GraphNode, b: GraphNode) -> float:
    """
    Weighted combination of:
      - Label edit similarity  (40%)
      - Label token Jaccard    (30%)
      - Content token Jaccard  (20%)
      - Type match bonus       (10%)
    """
    label_edit   = edit_similarity(a.label, b.label)
    label_tok    = token_jaccard(a.label, b.label)
    content_tok  = token_jaccard(
        a.content_preview or "", b.content_preview or ""
    ) if (a.content_preview or b.content_preview) else 0.0
    type_bonus   = 0.1 if a.type == b.type else 0.0

    return 0.4 * label_edit + 0.3 * label_tok + 0.2 * content_tok + type_bonus


# ─── Candidate Pair ───────────────────────────────────────────────────────────

@dataclass
class DuplicateCandidate:
    node_a_id: str
    node_b_id: str
    node_a_label: str
    node_b_label: str
    similarity: float
    reason: str                      # "url_match" | "label_edit" | "token_overlap" | "metadata"
    is_confirmed: bool = False       # True if human confirmed as same entity
    canonical_id: Optional[str] = None  # Which node becomes the canonical one


# ─── Alias Registry ───────────────────────────────────────────────────────────

class AliasRegistry:
    """
    Maps any alias (label variant) → canonical node ID.
    Persists within the in-memory store; serialized as part of graph metadata.
    """
    def __init__(self):
        self._alias_to_id: dict[str, str] = {}   # alias → canonical_id
        self._id_to_aliases: dict[str, set[str]] = {}  # canonical_id → {aliases}

    def register(self, canonical_id: str, alias: str):
        norm = _normalize(alias)
        self._alias_to_id[norm] = canonical_id
        self._id_to_aliases.setdefault(canonical_id, set()).add(alias)

    def resolve(self, label: str) -> Optional[str]:
        return self._alias_to_id.get(_normalize(label))

    def get_aliases(self, canonical_id: str) -> list[str]:
        return sorted(self._id_to_aliases.get(canonical_id, []))

    def remove(self, canonical_id: str):
        aliases = self._id_to_aliases.pop(canonical_id, set())
        for alias in aliases:
            self._alias_to_id.pop(_normalize(alias), None)


# ─── Global alias registry per graph ─────────────────────────────────────────

_registries: dict[str, AliasRegistry] = {}

def get_alias_registry(graph_id: str) -> AliasRegistry:
    if graph_id not in _registries:
        _registries[graph_id] = AliasRegistry()
    return _registries[graph_id]


# ─── Entity Resolver ─────────────────────────────────────────────────────────

class EntityResolver:

    # ── Detection ──────────────────────────────────────────────────────────────

    def find_duplicates(
        self,
        kg: KnowledgeGraph,
        similarity_threshold: float = 0.82,
        check_url: bool = True,
        max_candidates: int = 200,
    ) -> list[DuplicateCandidate]:
        """
        Scan all node pairs and return likely duplicates.
        Uses blocking to avoid O(n²) comparisons:
          - Same first 3 characters after normalization (label block)
          - Same Feishu token (URL block)
        """
        candidates: list[DuplicateCandidate] = []
        seen: set[tuple[str, str]] = set()
        nodes = kg.nodes

        # URL / token identity check (fast O(n))
        if check_url:
            token_map: dict[str, list[GraphNode]] = {}
            for n in nodes:
                if n.token:
                    token_map.setdefault(n.token, []).append(n)
            for token, group in token_map.items():
                if len(group) < 2:
                    continue
                for i in range(len(group)):
                    for j in range(i + 1, len(group)):
                        a, b = group[i], group[j]
                        key = tuple(sorted([a.id, b.id]))
                        if key not in seen:
                            seen.add(key)
                            candidates.append(DuplicateCandidate(
                                node_a_id=a.id, node_b_id=b.id,
                                node_a_label=a.label, node_b_label=b.label,
                                similarity=1.0, reason="token_match",
                            ))

        # Label-block similarity check
        block_map: dict[str, list[GraphNode]] = {}
        for n in nodes:
            block = _normalize(n.label)[:3]  # 3-char prefix block
            block_map.setdefault(block, []).append(n)

        for block, group in block_map.items():
            if len(group) < 2 or len(group) > 50:  # Skip huge blocks
                continue
            for i in range(len(group)):
                for j in range(i + 1, len(group)):
                    a, b = group[i], group[j]
                    key = tuple(sorted([a.id, b.id]))
                    if key in seen:
                        continue
                    seen.add(key)
                    sim = combined_similarity(a, b)
                    if sim >= similarity_threshold:
                        reason = "label_edit" if edit_similarity(a.label, b.label) > 0.9 else "token_overlap"
                        candidates.append(DuplicateCandidate(
                            node_a_id=a.id, node_b_id=b.id,
                            node_a_label=a.label, node_b_label=b.label,
                            similarity=round(sim, 4), reason=reason,
                        ))

        candidates.sort(key=lambda x: x.similarity, reverse=True)
        return candidates[:max_candidates]

    # ── Merge ─────────────────────────────────────────────────────────────────

    def merge_nodes(
        self,
        kg: KnowledgeGraph,
        canonical_id: str,
        duplicate_id: str,
        new_label: Optional[str] = None,
        note: str = "",
        initiated_by: str = "human",
    ) -> dict:
        """
        Merge duplicate_id into canonical_id.
        - All edges pointing to/from duplicate → redirected to canonical
        - duplicate node soft-deleted (removed from kg.nodes)
        - Alias registry updated
        - ChangeRecords created
        - Returns merge summary
        """
        canonical = kg.get_node(canonical_id)
        duplicate = kg.get_node(duplicate_id)
        if not canonical or not duplicate:
            return {"error": "One or both nodes not found"}

        merged_label = new_label or canonical.label
        old_canonical_label = canonical.label

        # Update canonical node
        canonical.label = merged_label
        # Merge metadata
        for k, v in (duplicate.metadata or {}).items():
            if k not in canonical.metadata:
                canonical.metadata[k] = v
        # Merge content
        if duplicate.content_preview and not canonical.content_preview:
            canonical.content_preview = duplicate.content_preview
        # Inherit URL if missing
        if duplicate.url and not canonical.url:
            canonical.url = duplicate.url
        if duplicate.token and not canonical.token:
            canonical.token = duplicate.token

        # Redirect edges
        redirected_edges = 0
        for edge in kg.edges:
            if edge.source == duplicate_id:
                edge.source = canonical_id
                redirected_edges += 1
            if edge.target == duplicate_id:
                edge.target = canonical_id
                redirected_edges += 1

        # Remove self-loops that may result from redirect
        kg.edges = [
            e for e in kg.edges
            if not (e.source == e.target == canonical_id)
        ]

        # Remove duplicate from kg
        kg.nodes = [n for n in kg.nodes if n.id != duplicate_id]

        # Register alias
        registry = get_alias_registry(kg.id)
        registry.register(canonical_id, duplicate.label)
        registry.register(canonical_id, canonical.label)

        # Temporal store update
        store = get_temporal_store(kg.id)
        if store:
            cr_canonical = ChangeRecord(
                node_id=canonical_id,
                change_type=ChangeType.MERGED,
                field="label",
                old_value=old_canonical_label,
                new_value=merged_label,
                source=f"merge:{initiated_by}",
                note=f"Merged from {duplicate.label!r} ({duplicate_id}). {note}",
            )
            cr_duplicate = ChangeRecord(
                node_id=duplicate_id,
                change_type=ChangeType.DEPRECATED,
                field="node",
                old_value=duplicate.label,
                new_value=f"merged_into:{canonical_id}",
                source=f"merge:{initiated_by}",
                note=f"Merged into {canonical.label!r} ({canonical_id}). {note}",
            )
            # Apply to temporal store
            tn_canonical = store.get_node(canonical_id)
            if tn_canonical:
                tn_canonical.change_history.append(cr_canonical)
            tn_duplicate = store.get_node(duplicate_id)
            if tn_duplicate:
                tn_duplicate.is_current = False
                from datetime import datetime, timezone
                tn_duplicate.valid_until = datetime.now(timezone.utc)
                tn_duplicate.change_history.append(cr_duplicate)

        logger.info(f"Merged {duplicate_id} ({duplicate.label!r}) → {canonical_id} ({canonical.label!r})")
        return {
            "canonical_id": canonical_id,
            "duplicate_id": duplicate_id,
            "merged_label": merged_label,
            "redirected_edges": redirected_edges,
            "aliases": registry.get_aliases(canonical_id),
        }

    # ── Split ─────────────────────────────────────────────────────────────────

    def split_node(
        self,
        kg: KnowledgeGraph,
        node_id: str,
        splits: list[dict],   # [{"label": str, "edge_ids": [str]}]
        note: str = "",
        initiated_by: str = "human",
    ) -> dict:
        """
        Split one node into multiple new nodes.
        Each split spec assigns a label and which edges should move to the new node.
        Original node is deprecated.

        splits: [
          {"label": "System A", "edge_ids": ["edge1", "edge2"]},
          {"label": "System B", "edge_ids": ["edge3"]},
        ]
        """
        original = kg.get_node(node_id)
        if not original:
            return {"error": f"Node {node_id} not found"}

        new_node_ids = []
        for spec in splits:
            new_id = str(uuid.uuid4())
            new_node = GraphNode(
                id=new_id,
                label=spec["label"],
                type=original.type,
                depth=original.depth,
                token=original.token,
                url=original.url,
                content_preview=original.content_preview,
                metadata=dict(original.metadata or {}),
                color=original.color,
                size=original.size,
                icon=original.icon,
            )
            kg.nodes.append(new_node)
            new_node_ids.append(new_id)

            # Move specified edges to new node
            edge_id_set = set(spec.get("edge_ids", []))
            for edge in kg.edges:
                if edge.id in edge_id_set:
                    if edge.source == node_id:
                        edge.source = new_id
                    if edge.target == node_id:
                        edge.target = new_id

        # Deprecate original
        kg.nodes = [n for n in kg.nodes if n.id != node_id]

        # Temporal
        store = get_temporal_store(kg.id)
        if store:
            tn = store.get_node(node_id)
            if tn:
                tn.is_current = False
                from datetime import datetime, timezone
                tn.valid_until = datetime.now(timezone.utc)
                tn.change_history.append(ChangeRecord(
                    node_id=node_id,
                    change_type=ChangeType.SPLIT,
                    field="node",
                    old_value=original.label,
                    new_value=str(new_node_ids),
                    source=f"split:{initiated_by}",
                    note=note,
                ))

        return {
            "original_id": node_id,
            "original_label": original.label,
            "new_nodes": new_node_ids,
            "splits": [{"id": nid, "label": s["label"]} for nid, s in zip(new_node_ids, splits)],
        }

    # ── Alias Lookup ──────────────────────────────────────────────────────────

    def resolve_alias(self, graph_id: str, label: str) -> Optional[str]:
        return get_alias_registry(graph_id).resolve(label)

    def register_alias(self, graph_id: str, canonical_id: str, alias: str):
        get_alias_registry(graph_id).register(canonical_id, alias)

    def get_aliases(self, graph_id: str, canonical_id: str) -> list[str]:
        return get_alias_registry(graph_id).get_aliases(canonical_id)


# ─── Singleton ────────────────────────────────────────────────────────────────

entity_resolver = EntityResolver()
