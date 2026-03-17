/**
 * Timeline Panel — full change history of the knowledge graph.
 * Shows node versions, timestamps, and correction batches in chronological order.
 */
import React, { useState, useEffect, useCallback } from 'react'
import {
  Clock, GitBranch, Plus, Trash2, Edit3,
  CheckCircle, RefreshCw, Loader2, AlertCircle, ChevronDown
} from 'lucide-react'
import axios from 'axios'

interface ChangeRecord {
  node_id: string
  node_label: string
  node_type: string
  change_type: string
  field: string
  old_value: unknown
  new_value: unknown
  changed_at: string
  source: string
  correction_batch_id: string | null
  note: string
  display: string
}

interface TimelineProps {
  graphId: string
  nodeId?: string
}

const CHANGE_COLORS: Record<string, string> = {
  created:    'text-emerald-400 border-emerald-700',
  updated:    'text-blue-400 border-blue-700',
  deprecated: 'text-slate-500 border-slate-700',
  corrected:  'text-amber-400 border-amber-700',
  merged:     'text-purple-400 border-purple-700',
  restored:   'text-cyan-400 border-cyan-700',
}

const CHANGE_ICONS: Record<string, React.ReactNode> = {
  created:    <Plus size={11} />,
  updated:    <Edit3 size={11} />,
  deprecated: <Trash2 size={11} />,
  corrected:  <CheckCircle size={11} />,
  merged:     <GitBranch size={11} />,
  restored:   <RefreshCw size={11} />,
}

function groupByDate(records: ChangeRecord[]): [string, ChangeRecord[]][] {
  const groups: Record<string, ChangeRecord[]> = {}
  for (const r of records) {
    const date = r.changed_at.slice(0, 10)
    ;(groups[date] ??= []).push(r)
  }
  return Object.entries(groups).sort(([a], [b]) => b.localeCompare(a))
}

export default function TimelinePanel({ graphId, nodeId }: TimelineProps) {
  const [records, setRecords] = useState<ChangeRecord[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const params = nodeId ? `?node_id=${nodeId}&limit=200` : '?limit=200'
      const res = await axios.get(`/temporal/graphs/${graphId}/timeline${params}`)
      setRecords(res.data.records || [])
      setTotal(res.data.total || 0)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to load timeline')
    } finally {
      setLoading(false)
    }
  }, [graphId, nodeId])

  useEffect(() => { load() }, [load])

  const groups = groupByDate(records)
  const toggleGroup = (date: string) => {
    setExpanded(prev => {
      const next = new Set(prev)
      next.has(date) ? next.delete(date) : next.add(date)
      return next
    })
  }

  if (loading) return (
    <div className="flex items-center justify-center h-32 gap-2 text-slate-400 text-xs">
      <Loader2 size={14} className="animate-spin" /> 加载时间线…
    </div>
  )

  if (error) return (
    <div className="m-3 p-3 bg-red-900/30 border border-red-700 rounded text-red-300 text-xs flex items-center gap-2">
      <AlertCircle size={12} /> {error}
    </div>
  )

  if (records.length === 0) return (
    <div className="flex flex-col items-center justify-center h-32 text-slate-500 text-xs gap-2">
      <Clock size={24} className="opacity-30" />
      尚无变更记录
    </div>
  )

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-2 border-b border-slate-700">
        <span className="text-[11px] text-slate-400">共 {total} 笔变更记录</span>
        <button onClick={load} className="text-slate-500 hover:text-slate-300">
          <RefreshCw size={12} />
        </button>
      </div>

      {/* Timeline */}
      <div className="flex-1 overflow-y-auto">
        {groups.map(([date, recs]) => (
          <div key={date}>
            {/* Date header */}
            <button
              onClick={() => toggleGroup(date)}
              className="w-full flex items-center gap-2 px-3 py-1.5 bg-slate-800/60 hover:bg-slate-700/40 border-b border-slate-700 transition-colors"
            >
              <Clock size={10} className="text-slate-500" />
              <span className="text-[11px] font-semibold text-slate-300">{date}</span>
              <span className="text-[10px] text-slate-500 ml-1">({recs.length} 笔)</span>
              <ChevronDown
                size={10}
                className={`ml-auto text-slate-500 transition-transform ${expanded.has(date) ? '' : '-rotate-90'}`}
              />
            </button>

            {/* Events — collapsed by default for dates > 3 days ago */}
            {!expanded.has(date) && (
              <div className="relative pl-8 pr-3 py-1">
                {/* Vertical line */}
                <div className="absolute left-4 top-0 bottom-0 w-px bg-slate-700" />

                {recs.map((rec, i) => {
                  const colorClass = CHANGE_COLORS[rec.change_type] || 'text-slate-400 border-slate-700'
                  const icon = CHANGE_ICONS[rec.change_type] || <Edit3 size={11} />
                  const isCorrected = !!rec.correction_batch_id

                  return (
                    <div key={i} className="relative flex gap-2 py-1.5">
                      {/* Dot on timeline */}
                      <div className={`absolute -left-4 w-3 h-3 rounded-full border flex items-center justify-center bg-slate-900 flex-shrink-0 ${colorClass}`}
                           style={{ top: '6px', left: '-18px' }}>
                        <span className="scale-75">{icon}</span>
                      </div>

                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-1.5 flex-wrap">
                          <span className={`text-[10px] uppercase font-semibold px-1 rounded ${colorClass.split(' ')[0]}`}>
                            {rec.change_type}
                          </span>
                          <span className="text-slate-300 text-[11px] truncate font-medium">
                            {rec.node_label}
                          </span>
                          <span className="text-slate-600 text-[10px]">[{rec.node_type}]</span>
                          {isCorrected && (
                            <span className="text-amber-600 text-[9px] bg-amber-900/30 px-1 rounded">人工校正</span>
                          )}
                        </div>

                        {rec.field && (
                          <div className="text-[10px] text-slate-500 mt-0.5">
                            {rec.field}:
                            {rec.old_value !== null && rec.old_value !== undefined && (
                              <span className="line-through text-red-400/60 mx-1">{String(rec.old_value).slice(0, 40)}</span>
                            )}
                            {rec.new_value !== null && rec.new_value !== undefined && (
                              <span className="text-emerald-400/70">{String(rec.new_value).slice(0, 40)}</span>
                            )}
                          </div>
                        )}

                        {rec.note && (
                          <div className="text-[10px] text-slate-500 italic mt-0.5">{rec.note}</div>
                        )}

                        <div className="text-[9px] text-slate-600 mt-0.5">
                          {rec.changed_at.slice(11, 19)} · {rec.source}
                        </div>
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
