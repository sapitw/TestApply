/**
 * Right panel showing details of the selected node.
 */
import React from 'react'
import { X, ExternalLink, Code2, FileText, Hash } from 'lucide-react'
import type { GraphNode } from '../utils/api'
import { useGraphStore } from '../stores/graphStore'

interface Props {
  node: GraphNode
}

export default function NodeDetailPanel({ node }: Props) {
  const { setSelectedNode } = useGraphStore()

  const metaEntries = Object.entries(node.metadata || {}).filter(([, v]) => v !== null && v !== undefined)

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-start justify-between p-4 border-b border-slate-700">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span
              className="text-[10px] uppercase font-semibold px-2 py-0.5 rounded"
              style={{ background: '#4F46E522', color: '#818cf8' }}
            >
              {node.type.replace(/_/g, ' ')}
            </span>
            {node.depth !== undefined && (
              <span className="text-[10px] text-slate-500">depth {node.depth}</span>
            )}
          </div>
          <h3 className="text-sm font-semibold text-slate-100 leading-tight">{node.label}</h3>
        </div>
        <button
          onClick={() => setSelectedNode(null)}
          className="flex-shrink-0 ml-2 text-slate-400 hover:text-slate-200 transition-colors"
        >
          <X size={16} />
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4 text-sm">
        {/* Preview */}
        {node.content_preview && (
          <div>
            <div className="text-[10px] uppercase font-semibold text-slate-500 mb-2 flex items-center gap-1">
              <FileText size={10} />
              Content Preview
            </div>
            <div className="bg-slate-800/60 rounded p-3 text-slate-300 text-xs leading-relaxed border border-slate-700">
              {node.content_preview}
            </div>
          </div>
        )}

        {/* Node ID / Token */}
        <div>
          <div className="text-[10px] uppercase font-semibold text-slate-500 mb-2 flex items-center gap-1">
            <Hash size={10} />
            Identifiers
          </div>
          <div className="space-y-1">
            <div className="text-[11px] text-slate-400">
              <span className="text-slate-500">ID: </span>
              <code className="text-slate-300 font-mono">{node.id}</code>
            </div>
            {node.token && (
              <div className="text-[11px] text-slate-400">
                <span className="text-slate-500">Token: </span>
                <code className="text-slate-300 font-mono">{node.token}</code>
              </div>
            )}
          </div>
        </div>

        {/* External URL */}
        {node.url && (
          <div>
            <div className="text-[10px] uppercase font-semibold text-slate-500 mb-2">Source</div>
            <a
              href={node.url}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center gap-1.5 text-blue-400 hover:text-blue-300 text-xs break-all"
            >
              <ExternalLink size={10} className="flex-shrink-0" />
              {node.url}
            </a>
          </div>
        )}

        {/* Metadata */}
        {metaEntries.length > 0 && (
          <div>
            <div className="text-[10px] uppercase font-semibold text-slate-500 mb-2 flex items-center gap-1">
              <Code2 size={10} />
              Metadata
            </div>
            <div className="space-y-1">
              {metaEntries.map(([k, v]) => (
                <div key={k} className="flex gap-2 text-[11px]">
                  <span className="text-slate-500 flex-shrink-0">{k}:</span>
                  <span className="text-slate-300 break-all">{String(v)}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Children count */}
        {node.children_count > 0 && (
          <div className="text-xs text-slate-400 bg-slate-800/40 rounded p-2 border border-slate-700">
            This node has <strong className="text-slate-200">{node.children_count}</strong> direct children
            {node.expandable && ' (expandable)'}
          </div>
        )}
      </div>
    </div>
  )
}
