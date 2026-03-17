"""
Inference Engine
================
Derives implicit knowledge from explicit graph facts.

Industry techniques:
  1. Transitive Closure  — if A contains B and B contains C → A transitively_contains C
  2. Inverse Relations   — if A manages B → B reports_to A
  3. Symmetric Relations — if A related_to B → B related_to A
  4. Confidence Propagation — child confidence ≤ parent confidence
  5. Inheritance         — children inherit type-level properties from parent
  6. Path Inference      — "A depends_on B and B implements C → A indirectly depends_on C"
  7. Co-occurrence       — nodes appearing in same document cluster → related_to edge

All inferred edges are marked with:
  metadata.inferred = True
  metadata.rule = "<rule_name>"
  weight < 1.0 (inferred are weaker than explicit)

Inferred edges are NOT persisted — they are re-derived on each query.
"""
from __future__ import annotations

import uuid
from collections import defaultdict
from typing import Optional

import networkx as nx

from backend.graph.models import KnowledgeGraph, GraphEdge, EdgeType


# ─── Inference Rules ──────────────────────────────────────────────────────────

# Transitive relations: if A -[r]→ B -[r]→ C then A -[r]→ C (marked inferred)
TRANSITIVE_RELATIONS = {EdgeType.CONTAINS, EdgeType.DEPENDS_ON, EdgeType.IMPLEMENTS}

# Inverse pairs: if A -[r]→ B then B -[r_inv]→ A
INVERSE_RELATIONS = {
    EdgeType.CONTAINS:    "contained_by",
    EdgeType.MANAGES:     "reports_to",
    EdgeType.CALLS:       "called_by",
    EdgeType.IMPLEMENTS:  "implemented_by",
    EdgeType.DEPENDS_ON:  "dependency_of",
}

# Symmetric relations: if A -[r]→ B then B -[r]→ A
SYMMETRIC_RELATIONS = {EdgeType.RELATED_TO}

# Inheritance rules: child node inherits these metadata keys from parent
INHERITABLE_FIELDS = {"owner", "team", "status", "version", "project", "负责人"}


def _make_edge(source: str, target: str, etype: str, rule: str, weight: float = 0.6) -> GraphEdge:
    return GraphEdge(
        id=f"inf-{uuid.uuid4().hex[:8]}",
        source=source,
        target=target,
        type=etype,
        label=f"[inferred:{rule}]",
        weight=weight,
        metadata={"inferred": True, "rule": rule},
    )


class InferenceEngine:

    def derive_all(self, kg: KnowledgeGraph) -> list[GraphEdge]:
        """
        Run all inference rules and return a list of derived edges.
        Does NOT modify kg — caller decides whether to augment.
        """
        derived: list[GraphEdge] = []
        explicit_pairs: set[tuple[str, str, str]] = {
            (e.source, e.target, e.type) for e in kg.edges
        }

        def add(edge: GraphEdge):
            key = (edge.source, edge.target, edge.type)
            if edge.source != edge.target and key not in explicit_pairs:
                explicit_pairs.add(key)
                derived.append(edge)

        # ── 1. Transitive closure ─────────────────────────────────────────────
        for rel in TRANSITIVE_RELATIONS:
            rel_str = rel if isinstance(rel, str) else rel.value if hasattr(rel, "value") else str(rel)
            G = nx.DiGraph()
            for e in kg.edges:
                e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
                if e_type == rel_str:
                    G.add_edge(e.source, e.target)
            for a in list(G.nodes()):
                for c in nx.descendants(G, a):
                    # Skip direct edges (they're already explicit)
                    if not G.has_edge(a, c):
                        add(_make_edge(a, c, rel_str, "transitive", weight=0.5))

        # ── 2. Inverse relations ──────────────────────────────────────────────
        for e in kg.edges:
            e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
            if e_type in INVERSE_RELATIONS:
                inv_type = INVERSE_RELATIONS[e_type]
                add(_make_edge(e.target, e.source, inv_type, "inverse", weight=0.8))

        # ── 3. Symmetric relations ────────────────────────────────────────────
        for e in kg.edges:
            e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
            if e_type in {r.value if hasattr(r, 'value') else str(r) for r in SYMMETRIC_RELATIONS}:
                add(_make_edge(e.target, e.source, e_type, "symmetric", weight=e.weight))

        # ── 4. Co-occurrence (siblings in same parent) ────────────────────────
        # Nodes sharing a parent via CONTAINS get a weak related_to edge
        parent_map: dict[str, list[str]] = defaultdict(list)
        for e in kg.edges:
            e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
            if e_type == (EdgeType.CONTAINS.value if hasattr(EdgeType.CONTAINS, 'value') else str(EdgeType.CONTAINS)):
                parent_map[e.source].append(e.target)
        for parent, children in parent_map.items():
            if len(children) > 1:
                for i in range(min(len(children), 8)):   # limit pairs per parent
                    for j in range(i + 1, min(len(children), 8)):
                        add(_make_edge(children[i], children[j], "sibling_of", "co_occurrence", weight=0.3))

        # ── 5. Dependency propagation ────────────────────────────────────────
        # A depends_on B and B depends_on C → A transitively_depends_on C
        # (covered by transitive above, but also derive cross-type:)
        # A implements B and B contains C → A indirectly_references C
        impl_map: dict[str, str] = {}
        for e in kg.edges:
            e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
            if e_type == (EdgeType.IMPLEMENTS.value if hasattr(EdgeType.IMPLEMENTS, 'value') else "implements"):
                impl_map[e.source] = e.target
        contains_map: dict[str, list[str]] = defaultdict(list)
        for e in kg.edges:
            e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
            if e_type == (EdgeType.CONTAINS.value if hasattr(EdgeType.CONTAINS, 'value') else "contains"):
                contains_map[e.source].append(e.target)

        for a, b in impl_map.items():
            for c in contains_map.get(b, []):
                add(_make_edge(a, c, "indirectly_references", "path_inference", weight=0.4))

        return derived

    def apply_confidence_propagation(self, kg: KnowledgeGraph) -> dict[str, float]:
        """
        Propagate confidence downward through CONTAINS edges.
        A child's confidence cannot exceed its parent's confidence.
        Returns updated confidence dict {node_id: confidence}.
        """
        confidence: dict[str, float] = {}
        for n in kg.nodes:
            meta_conf = n.metadata.get("confidence") if n.metadata else None
            confidence[n.id] = float(meta_conf) if meta_conf is not None else 1.0

        # BFS from roots (nodes with no incoming CONTAINS edges)
        parent_conf: dict[str, float] = {}
        for e in kg.edges:
            e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
            if e_type == (EdgeType.CONTAINS.value if hasattr(EdgeType.CONTAINS, 'value') else "contains"):
                child_conf = confidence.get(e.source, 1.0)
                parent_conf[e.target] = min(
                    parent_conf.get(e.target, 1.0),
                    child_conf,
                )

        for node_id, pc in parent_conf.items():
            confidence[node_id] = min(confidence.get(node_id, 1.0), pc)

        return confidence

    def inherit_metadata(self, kg: KnowledgeGraph) -> dict[str, dict]:
        """
        Propagate inheritable metadata (owner, team, status…) from parents to children.
        Returns {node_id: {inherited_key: value, ...}}.
        """
        inherited: dict[str, dict] = {}
        # BFS from root
        from collections import deque
        roots = [n for n in kg.nodes if n.depth == 0]
        queue = deque(roots)
        parent_meta: dict[str, dict] = {r.id: dict(r.metadata or {}) for r in roots}

        visited = set()
        while queue:
            node = queue.popleft()
            if node.id in visited:
                continue
            visited.add(node.id)
            p_meta = parent_meta.get(node.id, {})
            # What to inherit from parent
            inh = {k: v for k, v in p_meta.items() if k in INHERITABLE_FIELDS and k not in (node.metadata or {})}
            inherited[node.id] = inh

            # Pass down to children (merge node meta + inherited)
            for e in kg.edges:
                e_type = e.type if isinstance(e.type, str) else e.type.value if hasattr(e.type, 'value') else str(e.type)
                if e_type == (EdgeType.CONTAINS.value if hasattr(EdgeType.CONTAINS, 'value') else "contains") and e.source == node.id:
                    child = kg.get_node(e.target)
                    if child:
                        child_meta = dict(child.metadata or {})
                        child_meta.update({k: v for k, v in p_meta.items() if k in INHERITABLE_FIELDS and k not in child_meta})
                        parent_meta[child.id] = child_meta
                        queue.append(child)

        return inherited

    def get_derived_graph(self, kg: KnowledgeGraph) -> KnowledgeGraph:
        """
        Return a copy of kg augmented with all inferred edges.
        The original kg is NOT modified.
        """
        from copy import deepcopy
        augmented = deepcopy(kg)
        derived = self.derive_all(kg)
        augmented.edges.extend(derived)
        augmented.metadata["inferred_edge_count"] = len(derived)
        return augmented


# ─── Singleton ────────────────────────────────────────────────────────────────

inference_engine = InferenceEngine()
