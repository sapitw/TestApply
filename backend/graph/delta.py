"""
Change Delta Detector
=====================
Compares two knowledge graph snapshots and produces:
  - NodeDelta: per-node diff (created / updated / deprecated)
  - PendingItem: for any change that cannot be time-resolved

Time extraction heuristics:
  - Look for date patterns in content_preview and metadata
  - Parse relative terms ("as of Q1 2024", "since January", "updated last week")
  - If no date found → PendingReason.NO_DATE
  - If multiple conflicting values → PendingReason.CONFLICTING
"""
import re
import uuid
from datetime import datetime, timezone, timedelta
from typing import Optional
from loguru import logger

from backend.graph.models import GraphNode, KnowledgeGraph
from backend.graph.temporal import (
    TemporalNode, TemporalEdge, TemporalGraphStore,
    ChangeRecord, ChangeType, PendingItem, PendingReason, PendingStatus,
    set_temporal_store, get_temporal_store,
)


# ─── Date Extraction ──────────────────────────────────────────────────────────

# Patterns to extract dates from text
_DATE_PATTERNS = [
    (r'\b(\d{4})[/-](\d{1,2})[/-](\d{1,2})\b',  "%Y %m %d"),   # 2024-03-15
    (r'\b(\d{1,2})[/-](\d{1,2})[/-](\d{4})\b',  "%d %m %Y"),   # 15-03-2024
    (r'\b(20\d{2})[年/](\d{1,2})月\b',           "%Y %m"),       # 2024年3月
    (r'\bQ([1-4])\s*(20\d{2})\b',               "quarter"),     # Q1 2024
    (r'\b(January|February|March|April|May|June|July|August|'
     r'September|October|November|December)\s+(20\d{2})\b', "month_year"),
    (r'\b(20\d{2})\b',                            "%Y"),          # Just year
]

_RELATIVE_TERMS = {
    "last week": timedelta(weeks=-1),
    "last month": timedelta(days=-30),
    "last quarter": timedelta(days=-90),
    "last year": timedelta(days=-365),
    "this year": timedelta(days=0),
    "recently": None,   # → NO_DATE
    "soon": None,
    "tbd": None,
    "pending": None,
}

_MONTH_MAP = {
    "January": 1, "February": 2, "March": 3, "April": 4,
    "May": 5, "June": 6, "July": 7, "August": 8,
    "September": 9, "October": 10, "November": 11, "December": 12,
}


def extract_date_from_text(text: str) -> tuple[Optional[datetime], bool]:
    """
    Try to extract a date from text.
    Returns (datetime_or_None, is_ambiguous).
    is_ambiguous=True means a relative/vague term was found but no firm date.
    """
    if not text:
        return None, False

    text_lower = text.lower()

    # Relative terms
    for term, delta in _RELATIVE_TERMS.items():
        if term in text_lower:
            if delta is None:
                return None, True
            return datetime.now(timezone.utc) + delta, False

    # Absolute patterns
    for pattern, fmt in _DATE_PATTERNS:
        m = re.search(pattern, text, re.IGNORECASE)
        if not m:
            continue
        try:
            if fmt == "quarter":
                q, year = int(m.group(1)), int(m.group(2))
                month = (q - 1) * 3 + 1
                return datetime(int(year), month, 1, tzinfo=timezone.utc), False
            if fmt == "month_year":
                month_name, year = m.group(1), int(m.group(2))
                month = _MONTH_MAP.get(month_name, 1)
                return datetime(year, month, 1, tzinfo=timezone.utc), False
            if fmt == "%Y":
                return datetime(int(m.group(1)), 1, 1, tzinfo=timezone.utc), False
            if fmt == "%Y %m %d":
                return datetime(int(m.group(1)), int(m.group(2)), int(m.group(3)), tzinfo=timezone.utc), False
            if fmt == "%d %m %Y":
                return datetime(int(m.group(3)), int(m.group(2)), int(m.group(1)), tzinfo=timezone.utc), False
            if fmt == "%Y %m":
                return datetime(int(m.group(1)), int(m.group(2)), 1, tzinfo=timezone.utc), False
        except (ValueError, IndexError):
            continue

    return None, False


# ─── Node-level diff ─────────────────────────────────────────────────────────

SENSITIVE_FIELDS = {"label", "content_preview", "metadata"}
PERSON_KEYWORDS  = {"owner", "author", "lead", "manager", "contact", "负责人", "作者", "主管"}
INFO_KEYWORDS    = {"version", "status", "address", "url", "email", "phone",
                    "版本", "状态", "地址", "联系方式", "负责人"}


def _is_person_node(node: GraphNode) -> bool:
    label_lower = node.label.lower()
    meta_str = str(node.metadata).lower()
    return any(kw in label_lower or kw in meta_str for kw in PERSON_KEYWORDS)


def _is_info_node(node: GraphNode) -> bool:
    meta_str = str(node.metadata).lower()
    return any(kw in meta_str for kw in INFO_KEYWORDS)


def _node_fingerprint(node: GraphNode) -> dict:
    return {
        "label": node.label,
        "type": node.type,
        "preview": (node.content_preview or "")[:200],
        "meta": str(node.metadata),
    }


# ─── Delta Detector ───────────────────────────────────────────────────────────

class DeltaResult:
    def __init__(self):
        self.created: list[GraphNode] = []
        self.updated: list[tuple[GraphNode, GraphNode, list[str]]] = []  # (old, new, changed_fields)
        self.deprecated: list[GraphNode] = []
        self.pending_items: list[PendingItem] = []
        self.change_records: list[ChangeRecord] = []

    def summary(self) -> str:
        return (
            f"新增 {len(self.created)} 个节点，"
            f"变更 {len(self.updated)} 个，"
            f"废弃 {len(self.deprecated)} 个，"
            f"待判断 {len(self.pending_items)} 个"
        )


def detect_delta(
    old_graph: Optional[KnowledgeGraph],
    new_graph: KnowledgeGraph,
    scan_id: str = "",
) -> DeltaResult:
    """
    Compare old and new KnowledgeGraph snapshots.
    Returns DeltaResult with categorized changes and pending items.
    """
    result = DeltaResult()
    now = datetime.now(timezone.utc)

    if old_graph is None:
        # First scan — all nodes are "created"
        for node in new_graph.nodes:
            result.created.append(node)
            result.change_records.append(ChangeRecord(
                node_id=node.id, change_type=ChangeType.CREATED,
                field="node", new_value=node.label, source=scan_id,
            ))
        return result

    old_map = {n.id: n for n in old_graph.nodes}
    new_map = {n.id: n for n in new_graph.nodes}

    # Created
    for nid, node in new_map.items():
        if nid not in old_map:
            result.created.append(node)
            result.change_records.append(ChangeRecord(
                node_id=nid, change_type=ChangeType.CREATED,
                new_value=node.label, source=scan_id,
            ))

    # Deprecated
    for nid, node in old_map.items():
        if nid not in new_map:
            result.deprecated.append(node)
            result.change_records.append(ChangeRecord(
                node_id=nid, change_type=ChangeType.DEPRECATED,
                old_value=node.label, source=scan_id,
            ))

    # Updated
    for nid in set(old_map) & set(new_map):
        old_node = old_map[nid]
        new_node = new_map[nid]

        old_fp = _node_fingerprint(old_node)
        new_fp = _node_fingerprint(new_node)
        changed_fields = [k for k in old_fp if old_fp[k] != new_fp[k]]

        if not changed_fields:
            continue

        result.updated.append((old_node, new_node, changed_fields))

        for field in changed_fields:
            cr = ChangeRecord(
                node_id=nid, change_type=ChangeType.UPDATED,
                field=field, old_value=old_fp[field], new_value=new_fp[field],
                source=scan_id,
            )
            result.change_records.append(cr)

        # Attempt to determine when the change occurred
        combined_text = f"{new_node.label} {new_node.content_preview or ''} {str(new_node.metadata)}"
        date_found, is_ambiguous = extract_date_from_text(combined_text)

        needs_pending = False
        pending_reason = None
        pending_desc = ""

        if is_ambiguous:
            needs_pending = True
            pending_reason = PendingReason.NO_DATE
            pending_desc = f"节点「{new_node.label}」检测到变更，但内容含模糊时间表达，无法确认变更时间点。"

        elif date_found is None:
            if _is_person_node(new_node) or _is_info_node(new_node):
                needs_pending = True
                pending_reason = PendingReason.NO_DATE
                pending_desc = (
                    f"节点「{new_node.label}」（{'人员' if _is_person_node(new_node) else '资讯'}类）"
                    f"检测到以下字段变更：{changed_fields}，"
                    f"但无法从文档内容推断变更时间。"
                )

        # Check for conflicting values within the same scan
        if len(changed_fields) > 1 and "label" in changed_fields:
            needs_pending = True
            pending_reason = PendingReason.CONFLICTING
            pending_desc = (
                f"节点「{new_node.label}」（原：「{old_node.label}」）"
                f"名称与内容同时变更，可能是同一实体的不同版本，或已被不同人员取代。"
            )
            conflicting_values = [old_fp[f] for f in changed_fields] + [new_fp[f] for f in changed_fields]
        else:
            conflicting_values = []

        if needs_pending and pending_reason:
            pi = PendingItem(
                node_id=nid,
                graph_id=new_graph.id,
                reason=pending_reason,
                description=pending_desc,
                context=combined_text[:500],
                conflicting_values=conflicting_values,
            )
            result.pending_items.append(pi)

    return result


# ─── Merge delta into TemporalGraphStore ──────────────────────────────────────

def apply_delta_to_store(
    store: TemporalGraphStore,
    new_graph: KnowledgeGraph,
    delta: DeltaResult,
    scan_id: str = "",
) -> TemporalGraphStore:
    """
    Apply a DeltaResult to a TemporalGraphStore, creating new node versions.
    """
    now = datetime.now(timezone.utc)
    store.last_scanned_at = now
    store.scan_history.append({
        "scan_id": scan_id,
        "scanned_at": now.isoformat(),
        "created": len(delta.created),
        "updated": len(delta.updated),
        "deprecated": len(delta.deprecated),
        "pending": len(delta.pending_items),
    })

    # Map existing nodes by ID
    existing_ids = {n.id for n in store.nodes if n.is_current}

    # Apply created
    for node in delta.created:
        tn = _graph_node_to_temporal(node, scan_id, version=1)
        store.nodes.append(tn)

    # Apply updated
    for old_node, new_node, changed_fields in delta.updated:
        old_t = store.get_node(old_node.id)
        if old_t:
            old_t.is_current = False
            old_t.valid_until = now

        new_t = _graph_node_to_temporal(new_node, scan_id,
                                         version=(old_t.version + 1 if old_t else 1),
                                         prev_id=(old_t.version_id if old_t else None))
        new_t.valid_from = now
        if old_t:
            new_t.change_history = list(old_t.change_history)

        for cr in delta.change_records:
            if cr.node_id == new_node.id:
                new_t.change_history.append(cr)

        store.nodes.append(new_t)

    # Apply deprecated
    for node in delta.deprecated:
        old_t = store.get_node(node.id)
        if old_t:
            old_t.is_current = False
            old_t.valid_until = now
            old_t.change_history.append(ChangeRecord(
                node_id=node.id, change_type=ChangeType.DEPRECATED,
                source=scan_id,
            ))

    # Apply new edges (edges are not versioned as deeply — just current/deprecated)
    existing_edge_ids = {e.id for e in store.edges if e.is_current}
    new_edge_ids = {e.id for e in new_graph.edges}

    for edge in new_graph.edges:
        if edge.id not in existing_edge_ids:
            te = TemporalEdge(
                id=edge.id, source=edge.source, target=edge.target,
                type=edge.type, label=edge.label, weight=edge.weight,
                source_scan_id=scan_id,
            )
            store.edges.append(te)

    for edge in store.edges:
        if edge.is_current and edge.id not in new_edge_ids:
            edge.is_current = False
            edge.valid_until = now

    # Add pending items
    for pi in delta.pending_items:
        pi.graph_id = store.graph_id
        store.add_pending(pi)
        # Flag the node
        tn = store.get_node(pi.node_id)
        if tn:
            tn.is_pending = True
            tn.pending_item_id = pi.id
            tn.confidence = 0.5

    return store


def _graph_node_to_temporal(
    node: GraphNode,
    scan_id: str,
    version: int = 1,
    prev_id: str = None,
) -> TemporalNode:
    return TemporalNode(
        id=node.id,
        version=version,
        previous_version_id=prev_id,
        label=node.label,
        type=node.type,
        depth=node.depth,
        token=node.token,
        url=node.url,
        content_preview=node.content_preview,
        metadata=node.metadata,
        color=node.color,
        size=node.size,
        icon=node.icon,
        children_count=node.children_count,
        expandable=node.expandable,
        source_scan_id=scan_id,
    )
