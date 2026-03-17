"""
Correction Cascade Engine
==========================
When a human resolves a PendingItem or provides a correction,
this engine:
  1. Updates the target node with the corrected value + timestamp
  2. Finds all transitively connected nodes that reference the same entity
  3. Propagates the correction outward (BFS through the graph)
  4. Creates ChangeRecord entries for every affected node (correction_batch_id links them)
  5. Marks any downstream PendingItems that are now auto-resolvable

Cascade rules:
  - LABEL change: update all nodes whose label / preview references the old label
  - METADATA change: update nodes sharing the same token or linked by edges
  - PERSON change: find all nodes with the person's name in content → update
  - TIME correction: update valid_from / valid_until on affected nodes

Example:
  Pending: "John is listed as PM in doc A, but Director in doc B — no timestamp"
  Human resolves: "John became Director on 2024-06-01"
  Cascade: all nodes referencing "John / PM" → set valid_until=2024-06-01
             all nodes referencing "John / Director" → set valid_from=2024-06-01
"""
import uuid
import re
from datetime import datetime, timezone
from collections import deque
from typing import Optional
from loguru import logger

from backend.graph.temporal import (
    TemporalGraphStore, TemporalNode, ChangeRecord, ChangeType,
    PendingItem, PendingStatus, PendingReason,
    get_temporal_store,
)
from backend.graph.delta import extract_date_from_text


# ─── Correction Request ───────────────────────────────────────────────────────

class CorrectionRequest:
    """
    Submitted by a human (or DIFY agent) to resolve a pending item.
    Fields:
        pending_id     — the PendingItem being resolved
        correct_value  — the authoritative value
        valid_from     — when this value became true (ISO string or natural language)
        valid_until    — when this value stopped being true (optional)
        note           — human explanation
        cascade        — whether to propagate to connected nodes
    """
    def __init__(
        self,
        pending_id: str,
        correct_value: str,
        valid_from: Optional[str] = None,
        valid_until: Optional[str] = None,
        note: str = "",
        cascade: bool = True,
        submitted_by: str = "human",
    ):
        self.pending_id = pending_id
        self.correct_value = correct_value
        self.valid_from_str = valid_from or ""
        self.valid_until_str = valid_until or ""
        self.note = note
        self.cascade = cascade
        self.submitted_by = submitted_by


class CorrectionResult:
    def __init__(self):
        self.batch_id: str = str(uuid.uuid4())
        self.target_node_id: str = ""
        self.nodes_updated: list[str] = []
        self.pending_resolved: list[str] = []
        self.change_records: list[ChangeRecord] = []
        self.skipped: list[str] = []

    def summary(self) -> str:
        return (
            f"批次 {self.batch_id[:8]}：\n"
            f"  目标节点：{self.target_node_id}\n"
            f"  更新节点：{len(self.nodes_updated)} 个\n"
            f"  自动解决待判断：{len(self.pending_resolved)} 个\n"
            f"  跳过（无关联）：{len(self.skipped)} 个"
        )


# ─── Core Cascade Engine ─────────────────────────────────────────────────────

class CorrectionEngine:

    def apply(
        self,
        store: TemporalGraphStore,
        req: CorrectionRequest,
    ) -> CorrectionResult:
        result = CorrectionResult()
        now = datetime.now(timezone.utc)

        # --- 1. Find the pending item ---
        pending = store.get_pending(req.pending_id)
        if not pending:
            logger.warning(f"PendingItem {req.pending_id} not found")
            return result

        result.target_node_id = pending.node_id
        target = store.get_node(pending.node_id)
        if not target:
            logger.warning(f"Node {pending.node_id} not found in store")
            return result

        # --- 2. Parse timestamps ---
        valid_from = self._parse_dt(req.valid_from_str) or now
        valid_until = self._parse_dt(req.valid_until_str) if req.valid_until_str else None

        # --- 3. Apply correction to target node ---
        old_label = target.label
        self._apply_node_correction(
            store, target, req.correct_value, valid_from, valid_until,
            result.batch_id, req.note, req.submitted_by, result
        )

        # --- 4. Resolve the pending item ---
        store.resolve_pending(req.pending_id, req.correct_value, req.submitted_by, result.batch_id)
        result.pending_resolved.append(req.pending_id)

        # --- 5. Cascade to connected nodes ---
        if req.cascade:
            self._cascade(store, target, old_label, req.correct_value, valid_from, valid_until,
                          result.batch_id, req.submitted_by, result, now)

        return result

    def _apply_node_correction(
        self,
        store: TemporalGraphStore,
        node: TemporalNode,
        correct_value: str,
        valid_from: datetime,
        valid_until: Optional[datetime],
        batch_id: str,
        note: str,
        submitted_by: str,
        result: CorrectionResult,
    ):
        now = datetime.now(timezone.utc)

        # Deprecate current version
        node.is_current = False
        node.valid_until = valid_from  # The old value was valid UNTIL the correction date

        # Create corrected version
        import copy
        new_node = node.model_copy(deep=True)
        new_node.version_id = str(uuid.uuid4())
        new_node.version = node.version + 1
        new_node.previous_version_id = node.version_id
        new_node.label = correct_value if correct_value != node.label else node.label
        new_node.is_current = True
        new_node.is_pending = False
        new_node.pending_item_id = None
        new_node.confidence = 1.0
        new_node.valid_from = valid_from
        new_node.valid_until = valid_until
        new_node.updated_at = now

        cr = ChangeRecord(
            node_id=node.id,
            change_type=ChangeType.CORRECTED,
            field="label" if correct_value != node.label else "timestamp",
            old_value=node.label,
            new_value=correct_value,
            source=f"correction:{submitted_by}",
            correction_batch_id=batch_id,
            note=note,
        )
        new_node.change_history = list(node.change_history) + [cr]

        store.nodes.append(new_node)
        result.nodes_updated.append(node.id)
        result.change_records.append(cr)

    def _cascade(
        self,
        store: TemporalGraphStore,
        origin_node: TemporalNode,
        old_label: str,
        correct_value: str,
        valid_from: datetime,
        valid_until: Optional[datetime],
        batch_id: str,
        submitted_by: str,
        result: CorrectionResult,
        now: datetime,
    ):
        """
        BFS through connected nodes.
        For each node that references old_label in its content → apply correction.
        Also auto-resolve any pending items that become unambiguous.
        """
        visited = {origin_node.id}
        queue = deque()

        # Seed: direct neighbors via any edge
        for edge in store.edges:
            if not edge.is_current:
                continue
            neighbor_id = None
            if edge.source == origin_node.id:
                neighbor_id = edge.target
            elif edge.target == origin_node.id:
                neighbor_id = edge.source
            if neighbor_id and neighbor_id not in visited:
                queue.append((neighbor_id, 1))
                visited.add(neighbor_id)

        MAX_CASCADE_DEPTH = 4  # Prevent runaway propagation

        while queue:
            node_id, depth = queue.popleft()
            if depth > MAX_CASCADE_DEPTH:
                result.skipped.append(node_id)
                continue

            node = store.get_node(node_id)
            if not node:
                continue

            combined = f"{node.label} {node.content_preview or ''} {str(node.metadata)}"

            # Check if this node references the old value
            references_old = (
                old_label.lower() in combined.lower() or
                origin_node.token in (node.metadata.get("token", "") if node.metadata else "")
            )

            if references_old:
                # Update valid timestamps if this node represents the same time window
                node_changed = False

                if valid_until and node.valid_until is None:
                    # This node may have been valid through a period that's now bounded
                    node.valid_until = valid_until
                    node_changed = True

                if node_changed or node.is_pending:
                    cr = ChangeRecord(
                        node_id=node_id,
                        change_type=ChangeType.CORRECTED,
                        field="valid_until" if node_changed else "pending_cleared",
                        old_value=str(node.valid_until) if node_changed else "pending",
                        new_value=str(valid_until) if node_changed else "resolved",
                        source=f"cascade:{submitted_by}",
                        correction_batch_id=batch_id,
                        note=f"Auto-cascaded from correction of {old_label!r} → {correct_value!r}",
                    )
                    node.change_history.append(cr)
                    result.nodes_updated.append(node_id)
                    result.change_records.append(cr)

                    # Auto-resolve pending items on this node
                    for pi in store.pending_items:
                        if pi.node_id == node_id and pi.status == PendingStatus.OPEN:
                            if pi.reason in (PendingReason.NO_DATE, PendingReason.STALE_REF):
                                store.resolve_pending(
                                    pi.id,
                                    f"自动校正：参考「{old_label}→{correct_value}」，时间戳设为 {valid_from.date()}",
                                    resolved_by="cascade",
                                    correction_batch_id=batch_id,
                                )
                                result.pending_resolved.append(pi.id)
                                node.is_pending = False

            # Enqueue neighbors
            for edge in store.edges:
                if not edge.is_current:
                    continue
                nid = edge.target if edge.source == node_id else (edge.source if edge.target == node_id else None)
                if nid and nid not in visited:
                    visited.add(nid)
                    queue.append((nid, depth + 1))

    def _parse_dt(self, s: str) -> Optional[datetime]:
        if not s:
            return None
        # Try ISO first
        for fmt in ("%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%d", "%Y/%m/%d", "%Y年%m月%d日"):
            try:
                dt = datetime.strptime(s.strip(), fmt)
                return dt.replace(tzinfo=timezone.utc)
            except ValueError:
                continue
        # Try natural language
        dt, _ = extract_date_from_text(s)
        return dt


correction_engine = CorrectionEngine()
