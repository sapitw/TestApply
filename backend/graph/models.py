"""
Knowledge graph data models.
"""
from pydantic import BaseModel, Field
from typing import Optional, Any
from enum import Enum


class NodeType(str, Enum):
    # Document types
    WIKI_SPACE = "wiki_space"
    WIKI_PAGE = "wiki_page"
    DOCUMENT = "document"
    SPREADSHEET = "spreadsheet"
    SHEET_TAB = "sheet_tab"
    BITABLE = "bitable"
    BITABLE_TABLE = "bitable_table"
    FOLDER = "folder"
    FILE = "file"

    # Content structure types
    HEADING = "heading"
    SECTION = "section"
    CODE_BLOCK = "code_block"
    TABLE = "table"
    TODO = "todo"
    LINK = "link"

    # Semantic types (AI-inferred)
    CONCEPT = "concept"
    PROCESS = "process"
    ENTITY = "entity"
    RELATION = "relation"


class EdgeType(str, Enum):
    CONTAINS = "contains"          # Parent contains child
    REFERENCES = "references"      # Document A references document B
    LINKS_TO = "links_to"          # Hyperlink
    DEPENDS_ON = "depends_on"      # Code / process dependency
    RELATED_TO = "related_to"      # Semantic similarity
    NEXT = "next"                  # Sequential ordering
    IMPLEMENTS = "implements"      # Implementation relationship
    CALLS = "calls"                # Function/API call


class GraphNode(BaseModel):
    id: str
    label: str
    type: NodeType
    depth: int = 0
    token: Optional[str] = None
    url: Optional[str] = None
    content_preview: Optional[str] = None
    metadata: dict[str, Any] = Field(default_factory=dict)
    # Visual properties
    color: Optional[str] = None
    size: int = 20
    icon: Optional[str] = None
    # Expandable flag (has children not yet loaded)
    expandable: bool = False
    children_count: int = 0


class GraphEdge(BaseModel):
    id: str
    source: str
    target: str
    type: EdgeType
    label: Optional[str] = None
    weight: float = 1.0
    metadata: dict[str, Any] = Field(default_factory=dict)


class KnowledgeGraph(BaseModel):
    id: str
    title: str
    root_id: str
    nodes: list[GraphNode] = Field(default_factory=list)
    edges: list[GraphEdge] = Field(default_factory=list)
    metadata: dict[str, Any] = Field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "title": self.title,
            "root_id": self.root_id,
            "nodes": [n.model_dump() for n in self.nodes],
            "edges": [e.model_dump() for e in self.edges],
            "metadata": self.metadata,
        }

    def get_node(self, node_id: str) -> Optional[GraphNode]:
        for n in self.nodes:
            if n.id == node_id:
                return n
        return None

    def get_children(self, node_id: str) -> list[GraphNode]:
        child_ids = {
            e.target for e in self.edges
            if e.source == node_id and e.type == EdgeType.CONTAINS
        }
        return [n for n in self.nodes if n.id in child_ids]

    def get_subtree(self, node_id: str) -> dict:
        """Get node + all descendants as nested dict."""
        node = self.get_node(node_id)
        if not node:
            return {}
        children = self.get_children(node_id)
        return {
            **node.model_dump(),
            "children": [self.get_subtree(c.id) for c in children],
        }
