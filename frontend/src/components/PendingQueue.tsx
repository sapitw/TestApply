/**
 * Pending Review Queue (待判断面板)
 * Lists unresolved temporal ambiguities and lets users:
 *   - View context and conflicting values
 *   - Submit corrections with timestamps
 *   - Trigger cascade auto-correction
 *   - Dismiss irrelevant items
 */
import React, { useState, useEffect, useCallback } from 'react'
import {
  AlertTriangle, CheckCircle, X, ChevronDown, ChevronRight,
  Calendar, RefreshCw, Loader2, Clock, Zap, Info
} from 'lucide-react'
import axios from 'axios'

interface PendingItem {
  id: string
  node_id: string
  reason: string
  description: string
  context: string
  conflicting_values: unknown[]
  created_at: string
  status: string
  resolution: string | null
  resolved_at: string | null
  resolved_by: string
  display: string
}

interface PendingQueueProps {
  graphId: string
  onResolved?: (pendingId: string) => void
}

const REASON_LABELS: Record<string, { label: string; color: string; icon: React.ReactNode }> = {
  no_date:      { label: '无时间戳记', color: 'text-amber-400', icon: <Clock size={11} /> },
  conflicting:  { label: '资讯冲突', color: 'text-red-400', icon: <AlertTriangle size={11} /> },
  ambiguous_id: { label: '实体歧义', color: 'text-purple-400', icon: <Info size={11} /> },
  partial_info: { label: '资讯不完整', color: 'text-blue-400', icon: <Info size={11} /> },
  stale_ref:    { label: '过期引用', color: 'text-slate-400', icon: <Clock size={11} /> },
  unverified:   { label: '待人工确认', color: 'text-cyan-400', icon: <AlertTriangle size={11} /> },
}

function ResolveForm({
  item,
  onSubmit,
  onDismiss,
}: {
  item: PendingItem
  onSubmit: (data: {
    correct_value: string
    valid_from: string
    valid_until: string
    note: string
    cascade: boolean
  }) => void
  onDismiss: (reason: string) => void
}) {
  const [value, setValue] = useState(item.conflicting_values?.[0] ? String(item.conflicting_values[0]) : '')
  const [validFrom, setValidFrom] = useState('')
  const [validUntil, setValidUntil] = useState('')
  const [note, setNote] = useState('')
  const [cascade, setCascade] = useState(true)
  const [dismissReason, setDismissReason] = useState('')
  const [tab, setTab] = useState<'resolve' | 'dismiss'>('resolve')

  return (
    <div className="mt-2 bg-slate-900 border border-slate-600 rounded-lg p-3 space-y-2">
      {/* Tabs */}
      <div className="flex gap-1 mb-3">
        <button
          onClick={() => setTab('resolve')}
          className={`px-2 py-1 rounded text-[11px] font-medium transition-colors ${tab === 'resolve' ? 'bg-emerald-700 text-white' : 'text-slate-400 hover:text-slate-200'}`}
        >
          ✅ 提交校正
        </button>
        <button
          onClick={() => setTab('dismiss')}
          className={`px-2 py-1 rounded text-[11px] font-medium transition-colors ${tab === 'dismiss' ? 'bg-slate-700 text-slate-200' : 'text-slate-400 hover:text-slate-200'}`}
        >
          🚫 略过
        </button>
      </div>

      {tab === 'resolve' ? (
        <>
          <div>
            <label className="text-[10px] text-slate-500 block mb-1">正确值 <span className="text-red-400">*</span></label>
            <input
              value={value}
              onChange={e => setValue(e.target.value)}
              placeholder="输入正确的值（如：张三的职位是「总监」）"
              className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 placeholder-slate-500 focus:outline-none focus:border-emerald-500"
            />
          </div>

          <div className="flex gap-2">
            <div className="flex-1">
              <label className="text-[10px] text-slate-500 block mb-1">
                <Calendar size={9} className="inline mr-0.5" /> 生效时间
              </label>
              <input
                type="date"
                value={validFrom}
                onChange={e => setValidFrom(e.target.value)}
                className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 focus:outline-none focus:border-emerald-500"
              />
            </div>
            <div className="flex-1">
              <label className="text-[10px] text-slate-500 block mb-1">
                <Calendar size={9} className="inline mr-0.5" /> 失效时间（可留空）
              </label>
              <input
                type="date"
                value={validUntil}
                onChange={e => setValidUntil(e.target.value)}
                className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 focus:outline-none focus:border-emerald-500"
              />
            </div>
          </div>

          <div>
            <label className="text-[10px] text-slate-500 block mb-1">备注（可选）</label>
            <input
              value={note}
              onChange={e => setNote(e.target.value)}
              placeholder="校正依据、资料来源…"
              className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 placeholder-slate-500 focus:outline-none focus:border-emerald-500"
            />
          </div>

          <label className="flex items-center gap-2 text-[11px] text-slate-300 cursor-pointer">
            <input
              type="checkbox"
              checked={cascade}
              onChange={e => setCascade(e.target.checked)}
              className="accent-emerald-500"
            />
            <Zap size={11} className="text-amber-400" />
            自动级联校正相关联节点
          </label>

          <button
            onClick={() => value && onSubmit({ correct_value: value, valid_from: validFrom, valid_until: validUntil, note, cascade })}
            disabled={!value}
            className="w-full bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 text-white py-1.5 rounded text-[11px] font-medium transition-colors"
          >
            提交校正并自动传播
          </button>
        </>
      ) : (
        <>
          <div>
            <label className="text-[10px] text-slate-500 block mb-1">略过原因</label>
            <input
              value={dismissReason}
              onChange={e => setDismissReason(e.target.value)}
              placeholder="例如：此资讯已无关、属于正常历史变更…"
              className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 placeholder-slate-500 focus:outline-none focus:border-slate-500"
            />
          </div>
          <button
            onClick={() => onDismiss(dismissReason)}
            className="w-full bg-slate-700 hover:bg-slate-600 text-slate-200 py-1.5 rounded text-[11px] font-medium transition-colors"
          >
            略过此项
          </button>
        </>
      )}
    </div>
  )
}

function PendingCard({
  item,
  graphId,
  onResolved,
}: {
  item: PendingItem
  graphId: string
  onResolved: () => void
}) {
  const [expanded, setExpanded] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [resultMsg, setResultMsg] = useState('')

  const meta = REASON_LABELS[item.reason] || { label: item.reason, color: 'text-slate-400', icon: null }

  const submit = async (data: {
    correct_value: string
    valid_from: string
    valid_until: string
    note: string
    cascade: boolean
  }) => {
    setSubmitting(true)
    try {
      const res = await axios.post(`/temporal/graphs/${graphId}/pending/${item.id}/resolve`, {
        correct_value: data.correct_value,
        valid_from: data.valid_from || undefined,
        valid_until: data.valid_until || undefined,
        note: data.note,
        cascade: data.cascade,
        submitted_by: 'human',
      })
      const d = res.data
      setResultMsg(
        `✅ 已校正，更新 ${d.nodes_updated.length} 个节点，` +
        `自动解决 ${d.pending_resolved.length} 个待判断项目`
      )
      onResolved()
    } catch (e: unknown) {
      const err = e as { response?: { data?: { detail?: string } }; message?: string }
      setResultMsg('❌ ' + (err.response?.data?.detail || err.message || '提交失败'))
    } finally {
      setSubmitting(false)
    }
  }

  const dismiss = async (reason: string) => {
    setSubmitting(true)
    try {
      await axios.post(`/temporal/graphs/${graphId}/pending/${item.id}/dismiss`, { reason })
      setResultMsg('🚫 已略过')
      onResolved()
    } catch {
      setResultMsg('❌ 操作失败')
    } finally {
      setSubmitting(false)
    }
  }

  const isResolved = item.status !== 'open'

  return (
    <div className={`border rounded-lg overflow-hidden transition-all ${isResolved ? 'border-slate-800 opacity-60' : 'border-slate-700 hover:border-slate-600'}`}>
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-start gap-2 p-3 text-left"
      >
        <span className={`flex-shrink-0 mt-0.5 ${meta.color}`}>{meta.icon}</span>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-0.5">
            <span className={`text-[10px] font-semibold uppercase ${meta.color}`}>{meta.label}</span>
            {isResolved && (
              <span className="text-[9px] bg-slate-700 text-slate-400 px-1 rounded">
                {item.status === 'resolved' ? '已校正' : item.status === 'auto' ? '自动解决' : '已略过'}
              </span>
            )}
          </div>
          <p className="text-[11px] text-slate-300 leading-relaxed line-clamp-2">{item.description}</p>
          <div className="text-[9px] text-slate-600 mt-1">
            {item.created_at.slice(0, 16).replace('T', ' ')}
            {item.resolved_at && ` → 解决于 ${item.resolved_at.slice(0, 16).replace('T', ' ')}`}
          </div>
        </div>
        <ChevronDown size={12} className={`flex-shrink-0 text-slate-500 transition-transform mt-1 ${expanded ? '' : '-rotate-90'}`} />
      </button>

      {expanded && (
        <div className="px-3 pb-3 border-t border-slate-800">
          {/* Context */}
          {item.context && (
            <div className="mt-2 bg-slate-800/60 rounded p-2 text-[10px] text-slate-400 font-mono leading-relaxed">
              {item.context}
            </div>
          )}

          {/* Conflicting values */}
          {item.conflicting_values?.length > 0 && (
            <div className="mt-2">
              <div className="text-[10px] text-slate-500 mb-1">冲突值：</div>
              <div className="flex gap-2 flex-wrap">
                {item.conflicting_values.map((v, i) => (
                  <span key={i} className="text-[10px] bg-red-900/30 border border-red-800 text-red-300 px-2 py-0.5 rounded">
                    {String(v).slice(0, 60)}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Resolution info */}
          {item.resolution && (
            <div className="mt-2 bg-emerald-900/20 border border-emerald-800 rounded p-2 text-[10px] text-emerald-300">
              ✅ {item.resolution}
            </div>
          )}

          {/* Feedback message */}
          {resultMsg && (
            <div className="mt-2 text-[11px] text-slate-300 bg-slate-800 rounded p-2">{resultMsg}</div>
          )}

          {/* Action form (only for open items) */}
          {!isResolved && !resultMsg && !submitting && (
            <ResolveForm item={item} onSubmit={submit} onDismiss={dismiss} />
          )}

          {submitting && (
            <div className="mt-2 flex items-center gap-2 text-xs text-slate-400">
              <Loader2 size={12} className="animate-spin" /> 处理中，正在传播校正…
            </div>
          )}
        </div>
      )}
    </div>
  )
}

export default function PendingQueue({ graphId, onResolved }: PendingQueueProps) {
  const [items, setItems] = useState<PendingItem[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(false)
  const [filter, setFilter] = useState<'open' | 'resolved' | 'all'>('open')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const param = filter === 'all' ? '' : `?status=${filter}`
      const res = await axios.get(`/temporal/graphs/${graphId}/pending${param}`)
      setItems(res.data.items || [])
      setTotal(res.data.total || 0)
    } catch {/* ignore */} finally {
      setLoading(false)
    }
  }, [graphId, filter])

  useEffect(() => { load() }, [load])

  const onItemResolved = () => { load(); onResolved?.('') }

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center gap-2 px-3 py-2 border-b border-slate-700 flex-shrink-0">
        <AlertTriangle size={12} className="text-amber-400" />
        <span className="text-[11px] font-semibold text-slate-300">待判断队列</span>
        <span className="text-[10px] bg-amber-900/40 text-amber-400 px-1.5 py-0.5 rounded-full ml-auto">
          {items.filter(i => i.status === 'open').length} 待处理
        </span>
        <button onClick={load} className="text-slate-500 hover:text-slate-300">
          <RefreshCw size={12} />
        </button>
      </div>

      {/* Filter */}
      <div className="flex gap-1 px-3 py-1.5 border-b border-slate-700">
        {(['open', 'resolved', 'all'] as const).map(f => (
          <button
            key={f}
            onClick={() => setFilter(f)}
            className={`px-2 py-0.5 rounded text-[10px] transition-colors ${filter === f ? 'bg-slate-600 text-slate-200' : 'text-slate-500 hover:text-slate-300'}`}
          >
            {f === 'open' ? `⏳ 待处理` : f === 'resolved' ? `✅ 已解决` : `📋 全部`}
            {` (${items.filter(i => f === 'all' || i.status === f).length})`}
          </button>
        ))}
      </div>

      {/* List */}
      <div className="flex-1 overflow-y-auto p-3 space-y-2">
        {loading && (
          <div className="flex items-center justify-center h-16 gap-2 text-slate-400 text-xs">
            <Loader2 size={14} className="animate-spin" />
          </div>
        )}

        {!loading && items.length === 0 && (
          <div className="flex flex-col items-center justify-center h-32 text-slate-500 text-xs gap-2">
            <CheckCircle size={24} className="opacity-30 text-emerald-500" />
            {filter === 'open' ? '没有待判断项目 👍' : '没有记录'}
          </div>
        )}

        {!loading && items.map(item => (
          <PendingCard
            key={item.id}
            item={item}
            graphId={graphId}
            onResolved={onItemResolved}
          />
        ))}
      </div>
    </div>
  )
}
