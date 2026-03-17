"""
Temporal Knowledge Graph — versioned nodes, edges and change tracking.

Design principles:
  - Every node/edge is immutable once created; changes produce a new VERSION
  - Each version has valid_from / valid_until timestamps
  - When a value cannot be time-determined → PendingItem (待判断)
  - Corrections cascade through all transitively connected nodes via CorrectionEngine
  - Full audit trail in ChangeRecord

Entity lifecycle:
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Scan v1 (2024-01-01)        Scan v2 (2024-06-01)                  │
  │  Node: "John, PM"            Node: "John, Director"                │
  │  valid_from=2024-01-01       valid_from=2024-06-01                  │
  │  valid_until=2024-06-01      valid_until=None  ← still active       │
  │                                                                     │
  │  Change detected: role       → ChangeRecord created                 │
  │  Ambiguous date?             → PendingItem created                  │
  │  Correct date provided?      → cascade_correct() propagates         │
  └─────────────────────────────────────────────────────────────────────┘
"""
import uuid
from datetime import datetime, timezone
from typing import Optional, Any
from enum import Enum
from pydantic import BaseModel, Field


# ─── Enums ────────────────────────────────────────────────────────────────────

class ChangeType(str, Enum):
    CREATED    = "created"      # First appearance
    UPDATED    = "updated"      # Field value changed
    DEPRECATED = "deprecated"   # No longer current (soft delete)
    RESTORED   = "restored"     # Re-activated after deprecation
    CORRECTED  = "corrected"    # Human-confirmed correction
    MERGED     = "merged"       # Two nodes identified as same entity
    SPLIT      = "split"        # One node split into multiple entities


class PendingReason(str, Enum):
    NO_DATE       = "no_date"           # Cannot determine when change occurred
    CONFLICTING   = "conflicting"       # Multiple contradictory values found
    AMBIGUOUS_ID  = "ambiguous_id"      # Cannot tell if same or different entity
    PARTIAL_INFO  = "partial_info"      # Incomplete information
    STALE_REF     = "stale_ref"         # References an entity that may be outdated
    UNVERIFIED    = "unverified"        # AI-extracted but not confirmed


class PendingStatus(str, Enum):
    OPEN      = "open"        # Awaiting human review
    RESOLVED  = "resolved"    # Human provided correct info
    DISMISSED = "dismissed"   # Determined to be irrelevant
    AUTO      = "auto"        # Auto-resolved via cascade


# ─── Change Record (full audit trail) ────────────────────────────────────────

class ChangeRecord(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    node_id: str
    change_type: ChangeType
    changed_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    field: str = ""                    # Which field changed
    old_value: Optional[Any] = None
    new_value: Optional[Any] = None
    source: str = ""                   # "scan" | "manual" | "cascade"
    source_url: str = ""               # Feishu doc URL that triggered change
    correction_batch_id: Optional[str] = None  # Links cascade changes together
    note: str = ""

    def to_display(self) -> str:
        dt = self.changed_at.strftime("%Y-%m-%d %H:%M")
        if self.change_type == ChangeType.CREATED:
            return f"[{dt}] 新增：{self.field or self.node_id}"
        if self.change_type == ChangeType.UPDATED:
            return f"[{dt}] 变更：{self.field}  {self.old_value!r} → {self.new_value!r}"
        if self.change_type == ChangeType.DEPRECATED:
            return f"[{dt}] 废弃：{self.field or self.node_id}"
        if self.change_type == ChangeType.CORRECTED:
            return f"[{dt}] 校正：{self.field}  {self.old_value!r} → {self.new_value!r}"
        return f"[{dt}] {self.change_type}: {self.field}"


# ─── Pending Item (待判断) ───────────────────────────────────────────────────

class PendingItem(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    node_id: str                        # The node in question
    graph_id: str
    reason: PendingReason
    description: str                    # Human-readable explanation
    context: str = ""                   # Surrounding text / evidence
    conflicting_values: list[Any] = Field(default_factory=list)
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    status: PendingStatus = PendingStatus.OPEN
    resolution: Optional[str] = None   # Human-provided correct value
    resolved_at: Optional[datetime] = None
    resolved_by: str = ""               # "human" | "cascade" | "auto"
    affects_nodes: list[str] = Field(default_factory=list)  # downstream nodes

    def to_display(self) -> str:
        ts = self.created_at.strftime("%Y-%m-%d %H:%M")
        status_icon = {"open": "⏳", "resolved": "✅", "dismissed": "🚫", "auto": "🤖"}.get(self.status, "?")
        return (
            f"{status_icon} [{ts}] {self.reason.value.upper()}\n"
            f"   节点：{self.node_id}\n"
            f"   说明：{self.description}\n"
            + (f"   冲突值：{self.conflicting_values}\n" if self.conflicting_values else "")
            + (f"   解决：{self.resolution}\n" if self.resolution else "")
        )


# ─── Temporal Node Version ───────────────────────────────────────────────────

class TemporalNode(BaseModel):
    """
    Versioned knowledge graph node.
    Each version is an immutable snapshot; changes produce new versions.
    """
    id: str                             # Stable entity ID (shared across versions)
    version: int = 1
    version_id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    previous_version_id: Optional[str] = None

    # Core fields (same as GraphNode)
    label: str
    type: str
    depth: int = 0
    token: Optional[str] = None
    url: Optional[str] = None
    content_preview: Optional[str] = None
    metadata: dict[str, Any] = Field(default_factory=dict)
    color: Optional[str] = None
    size: int = 20
    icon: Optional[str] = None
    children_count: int = 0
    expandable: bool = False

    # Temporal fields
    valid_from: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    valid_until: Optional[datetime] = None  # None = currently active
    is_current: bool = True

    # Confidence & review
    confidence: float = 1.0            # 0.0–1.0; < 0.6 → auto-create PendingItem
    is_pending: bool = False           # Flagged for review
    pending_item_id: Optional[str] = None

    # Audit
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    updated_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    source_scan_id: str = ""
    change_history: list[ChangeRecord] = Field(default_factory=list)

    def is_active_at(self, dt: datetime) -> bool:
        if self.valid_from > dt:
            return False
        if self.valid_until and self.valid_until <= dt:
            return False
        return True

    def snapshot(self) -> dict:
        """Return the key fields for diffing."""
        return {
            "label": self.label,
            "type": self.type,
            "content_preview": self.content_preview,
            "metadata": self.metadata,
        }


# ─── Temporal Edge Version ───────────────────────────────────────────────────

class TemporalEdge(BaseModel):
    id: str
    version: int = 1
    version_id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    source: str
    target: str
    type: str
    label: Optional[str] = None
    weight: float = 1.0
    metadata: dict[str, Any] = Field(default_factory=dict)

    valid_from: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    valid_until: Optional[datetime] = None
    is_current: bool = True
    confidence: float = 1.0
    is_pending: bool = False

    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    source_scan_id: str = ""
    change_history: list[ChangeRecord] = Field(default_factory=list)


# ─── Temporal Graph Store ────────────────────────────────────────────────────

class TemporalGraphStore(BaseModel):
    """
    Stores all versions of nodes and edges for a single knowledge graph.
    Supports point-in-time queries and pending item management.
    """
    graph_id: str
    title: str
    root_id: str
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    last_scanned_at: Optional[datetime] = None

    # All versions (including deprecated)
    nodes: list[TemporalNode] = Field(default_factory=list)
    edges: list[TemporalEdge] = Field(default_factory=list)

    # Pending review queue
    pending_items: list[PendingItem] = Field(default_factory=list)

    # Scan history
    scan_history: list[dict] = Field(default_factory=list)

    # ─── Queries ──────────────────────────────────────────────────────────────

    def current_nodes(self) -> list[TemporalNode]:
        """All currently active nodes."""
        return [n for n in self.nodes if n.is_current]

    def current_edges(self) -> list[TemporalEdge]:
        return [e for e in self.edges if e.is_current]

    def nodes_at(self, dt: datetime) -> list[TemporalNode]:
        """Nodes valid at a specific point in time."""
        return [n for n in self.nodes if n.is_active_at(dt)]

    def get_node(self, node_id: str) -> Optional[TemporalNode]:
        """Current version of a node."""
        return next((n for n in self.nodes if n.id == node_id and n.is_current), None)

    def get_node_history(self, node_id: str) -> list[TemporalNode]:
        """All versions of a node, sorted oldest→newest."""
        versions = [n for n in self.nodes if n.id == node_id]
        return sorted(versions, key=lambda n: n.valid_from)

    def open_pending(self) -> list[PendingItem]:
        return [p for p in self.pending_items if p.status == PendingStatus.OPEN]

    def get_pending(self, pending_id: str) -> Optional[PendingItem]:
        return next((p for p in self.pending_items if p.id == pending_id), None)

    # ─── Mutations ────────────────────────────────────────────────────────────

    def upsert_node(self, node: TemporalNode) -> tuple[TemporalNode, Optional[ChangeRecord]]:
        """
        Insert or update a node. Returns (final_node, change_record_or_None).
        If the node already exists (same id, is_current), diff and version it.
        """
        existing = self.get_node(node.id)
        if not existing:
            self.nodes.append(node)
            cr = ChangeRecord(
                node_id=node.id, change_type=ChangeType.CREATED,
                field="node", new_value=node.label, source=node.source_scan_id
            )
            node.change_history.append(cr)
            return node, cr

        # Diff
        old_snap = existing.snapshot()
        new_snap = node.snapshot()
        diffs = {k: (old_snap[k], new_snap[k]) for k in old_snap if old_snap[k] != new_snap[k]}

        if not diffs:
            # No change — just update scan id
            existing.source_scan_id = node.source_scan_id
            existing.updated_at = datetime.now(timezone.utc)
            return existing, None

        # Deprecate old version
        now = datetime.now(timezone.utc)
        existing.is_current = False
        existing.valid_until = now

        # Create new version
        new_version = node.model_copy()
        new_version.version_id = str(uuid.uuid4())
        new_version.version = existing.version + 1
        new_version.previous_version_id = existing.version_id
        new_version.valid_from = now
        new_version.is_current = True

        crs = []
        for field, (old_val, new_val) in diffs.items():
            cr = ChangeRecord(
                node_id=node.id, change_type=ChangeType.UPDATED,
                field=field, old_value=old_val, new_value=new_val,
                source=node.source_scan_id
            )
            crs.append(cr)
        new_version.change_history = existing.change_history + crs

        self.nodes.append(new_version)
        return new_version, crs[0] if crs else None

    def add_pending(self, item: PendingItem):
        self.pending_items.append(item)
        # Mark node as pending
        node = self.get_node(item.node_id)
        if node:
            node.is_pending = True
            node.pending_item_id = item.id

    def resolve_pending(
        self,
        pending_id: str,
        resolution: str,
        resolved_by: str = "human",
        correction_batch_id: str = None,
    ) -> Optional[PendingItem]:
        item = self.get_pending(pending_id)
        if not item:
            return None
        item.status = PendingStatus.RESOLVED
        item.resolution = resolution
        item.resolved_at = datetime.now(timezone.utc)
        item.resolved_by = resolved_by
        # Unmark node
        node = self.get_node(item.node_id)
        if node:
            node.is_pending = False
        return item

    def to_graph_dict(self, as_of: datetime = None) -> dict:
        """Export as a plain graph dict (for backward compatibility)."""
        nodes = self.nodes_at(as_of) if as_of else self.current_nodes()
        edges = self.edges if not as_of else [
            e for e in self.edges if e.is_active_at(as_of)
            if hasattr(e, 'is_active_at')
        ]
        return {
            "id": self.graph_id,
            "title": self.title,
            "root_id": self.root_id,
            "nodes": [self._node_to_dict(n) for n in nodes],
            "edges": [{"id": e.id, "source": e.source, "target": e.target,
                       "type": e.type, "label": e.label, "weight": e.weight}
                      for e in edges],
            "metadata": {
                "created_at": self.created_at.isoformat(),
                "last_scanned_at": self.last_scanned_at.isoformat() if self.last_scanned_at else None,
                "pending_count": len(self.open_pending()),
            }
        }

    def _node_to_dict(self, node: TemporalNode) -> dict:
        return {
            "id": node.id, "label": node.label, "type": node.type,
            "depth": node.depth, "token": node.token, "url": node.url,
            "content_preview": node.content_preview, "metadata": node.metadata,
            "color": node.color, "size": node.size, "icon": node.icon,
            "expandable": node.expandable, "children_count": node.children_count,
            "is_pending": node.is_pending, "confidence": node.confidence,
            "valid_from": node.valid_from.isoformat(),
            "valid_until": node.valid_until.isoformat() if node.valid_until else None,
            "version": node.version,
        }


# ─── Global temporal store ───────────────────────────────────────────────────

_temporal_stores: dict[str, TemporalGraphStore] = {}


def get_temporal_store(graph_id: str) -> Optional[TemporalGraphStore]:
    return _temporal_stores.get(graph_id)


def set_temporal_store(store: TemporalGraphStore):
    _temporal_stores[store.graph_id] = store


def all_temporal_stores() -> dict[str, TemporalGraphStore]:
    return _temporal_stores
