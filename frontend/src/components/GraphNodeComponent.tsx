/**
 * Custom React Flow node component for knowledge graph nodes.
 */
import React from 'react'
import { Handle, Position } from 'reactflow'
import type { GraphNode } from '../utils/api'

interface Props {
  data: {
    node: GraphNode
    isSelected: boolean
    color: string
  }
}

const TYPE_ICONS: Record<string, string> = {
  wiki_space: '📚', wiki_page: '📄', document: '📝',
  spreadsheet: '📊', sheet_tab: '📋', bitable: '🗃️',
  bitable_table: '📑', folder: '📁', file: '📎',
  heading: '🔖', section: '📌', code_block: '💻',
  table: '📐', todo: '✅', link: '🔗',
  concept: '💡', process: '⚙️', entity: '🏷️',
}

export default function GraphNodeComponent({ data }: Props) {
  const { node, isSelected, color } = data

  const icon = TYPE_ICONS[node.type] || '●'
  const isRoot = node.depth === 0
  const isLeaf = node.children_count === 0

  return (
    <div
      className="group relative"
      style={{
        minWidth: isRoot ? 180 : 140,
        maxWidth: 240,
      }}
    >
      <Handle type="target" position={Position.Top} style={{ background: color, opacity: 0.6 }} />

      <div
        className="rounded-lg px-3 py-2 cursor-pointer transition-all duration-150"
        style={{
          background: isSelected
            ? `${color}33`
            : isRoot
            ? `${color}22`
            : '#1e293b',
          border: `${isSelected ? 2 : 1}px solid ${isSelected ? color : '#2d3e50'}`,
          boxShadow: isSelected ? `0 0 0 2px ${color}55` : 'none',
        }}
      >
        {/* Type badge */}
        <div className="flex items-center gap-1.5 mb-0.5">
          <span className="text-sm leading-none">{icon}</span>
          <span
            className="text-[9px] uppercase font-semibold tracking-wide px-1.5 py-0.5 rounded"
            style={{ background: `${color}33`, color }}
          >
            {node.type.replace(/_/g, ' ')}
          </span>
          {node.expandable && (
            <span className="text-[9px] text-slate-400 ml-auto">+{node.children_count}</span>
          )}
        </div>

        {/* Label */}
        <div
          className="text-sm font-medium leading-tight"
          style={{
            color: isRoot ? '#e2e8f0' : '#cbd5e1',
            fontSize: isRoot ? '13px' : '12px',
          }}
        >
          {node.label}
        </div>

        {/* Preview (hover) */}
        {node.content_preview && (
          <div
            className="hidden group-hover:block text-[10px] text-slate-400 mt-1 leading-relaxed border-t border-slate-700 pt-1"
            style={{ maxWidth: 220 }}
          >
            {node.content_preview.substring(0, 100)}
            {node.content_preview.length > 100 ? '…' : ''}
          </div>
        )}
      </div>

      <Handle type="source" position={Position.Bottom} style={{ background: color, opacity: 0.6 }} />
    </div>
  )
}
