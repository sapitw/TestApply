/**
 * Sidebar tree view component showing the hierarchical document structure.
 */
import React, { useState } from 'react'
import type { KnowledgeGraph, GraphNode } from '../utils/api'
import { useGraphStore } from '../stores/graphStore'
import { ChevronRight, ChevronDown } from 'lucide-react'

const TYPE_ICONS: Record<string, string> = {
  wiki_space: '📚', wiki_page: '📄', document: '📝',
  spreadsheet: '📊', sheet_tab: '📋', bitable: '🗃️',
  bitable_table: '📑', folder: '📁', file: '📎',
  heading: '🔖', section: '📌', code_block: '💻',
  table: '📐', todo: '✅', link: '🔗',
}

interface TreeNodeProps {
  nodeId: string
  graph: KnowledgeGraph
  depth: number
}

function TreeNode({ nodeId, graph, depth }: TreeNodeProps) {
  const [expanded, setExpanded] = useState(depth < 2)
  const { selectedNode, setSelectedNode } = useGraphStore()

  const node = graph.nodes.find(n => n.id === nodeId)
  if (!node) return null

  const children = graph.edges
    .filter(e => e.source === nodeId && e.type === 'contains')
    .map(e => graph.nodes.find(n => n.id === e.target))
    .filter(Boolean) as GraphNode[]

  const hasChildren = children.length > 0
  const isSelected = selectedNode?.id === nodeId
  const icon = TYPE_ICONS[node.type] || '●'

  return (
    <div>
      <div
        className={`flex items-center gap-1 py-1 px-2 rounded cursor-pointer text-sm transition-colors
          ${isSelected ? 'bg-brand-500/20 text-blue-300' : 'hover:bg-slate-700/50 text-slate-300'}`}
        style={{ paddingLeft: `${depth * 14 + 8}px` }}
        onClick={() => setSelectedNode(node)}
      >
        {hasChildren ? (
          <button
            className="flex-shrink-0 w-4 h-4 flex items-center justify-center text-slate-400 hover:text-slate-200"
            onClick={(e) => { e.stopPropagation(); setExpanded(!expanded) }}
          >
            {expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          </button>
        ) : (
          <span className="w-4 flex-shrink-0" />
        )}
        <span className="text-xs flex-shrink-0">{icon}</span>
        <span className="truncate text-xs leading-relaxed">{node.label}</span>
        {node.children_count > 0 && (
          <span className="flex-shrink-0 text-[10px] text-slate-500 ml-auto">{node.children_count}</span>
        )}
      </div>
      {expanded && hasChildren && (
        <div>
          {children.map(child => (
            <TreeNode key={child.id} nodeId={child.id} graph={graph} depth={depth + 1} />
          ))}
        </div>
      )}
    </div>
  )
}

interface Props {
  graph: KnowledgeGraph
}

export default function TreeView({ graph }: Props) {
  return (
    <div className="overflow-y-auto h-full py-2">
      <TreeNode nodeId={graph.root_id} graph={graph} depth={0} />
    </div>
  )
}
