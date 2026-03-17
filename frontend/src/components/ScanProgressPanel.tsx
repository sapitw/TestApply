/**
 * Real-time scan progress panel.
 * Connects to SSE stream and shows live progress of deep recursive scan.
 */
import React, { useEffect, useRef, useState, useCallback } from 'react'
import {
  Loader2, CheckCircle, XCircle, SkipForward, FileSearch,
  AlertTriangle, ChevronDown, ChevronUp, Trash2, RefreshCw
} from 'lucide-react'

export interface ScanEvent {
  type: 'start' | 'found' | 'skip' | 'indexed' | 'error' | 'done'
  token: string
  title: string
  doc_type: string
  depth: number
  total_found: number
  total_indexed: number
  total_skipped: number
  message: string
  timestamp: string
}

interface Props {
  jobId: string
  onDone: (graphId: string) => void
}

const EVENT_COLORS: Record<string, string> = {
  start:   'text-blue-400',
  found:   'text-slate-400',
  skip:    'text-amber-500',
  indexed: 'text-emerald-400',
  error:   'text-red-400',
  done:    'text-blue-300',
}

const EVENT_ICONS: Record<string, React.ReactNode> = {
  start:   <FileSearch size={10} className="text-blue-400 flex-shrink-0" />,
  found:   <span className="text-[10px] flex-shrink-0">🔍</span>,
  skip:    <SkipForward size={10} className="text-amber-500 flex-shrink-0" />,
  indexed: <CheckCircle size={10} className="text-emerald-400 flex-shrink-0" />,
  error:   <XCircle size={10} className="text-red-400 flex-shrink-0" />,
  done:    <CheckCircle size={10} className="text-blue-300 flex-shrink-0" />,
}

const DOC_TYPE_ICON: Record<string, string> = {
  docx: '📝', doc: '📝', sheet: '📊', spreadsheet: '📊',
  bitable: '🗃️', wiki: '📄', folder: '📁', file: '📎',
}

export default function ScanProgressPanel({ jobId, onDone }: Props) {
  const [events, setEvents] = useState<ScanEvent[]>([])
  const [status, setStatus] = useState<'running' | 'done' | 'error'>('running')
  const [stats, setStats] = useState({ found: 0, indexed: 0, skipped: 0 })
  const [showAll, setShowAll] = useState(false)
  const [filterType, setFilterType] = useState<string>('all')
  const logEndRef = useRef<HTMLDivElement>(null)
  const esRef = useRef<EventSource | null>(null)

  useEffect(() => {
    const es = new EventSource(`/api/scan/${jobId}/events`)
    esRef.current = es

    es.onmessage = (e) => {
      try {
        const ev: ScanEvent = JSON.parse(e.data)
        setEvents(prev => [...prev, ev])
        setStats({
          found: ev.total_found,
          indexed: ev.total_indexed,
          skipped: ev.total_skipped,
        })

        if (ev.type === 'done') {
          setStatus('done')
          es.close()
          // Fetch the completed job to get graph_id
          fetch(`/api/scan/${jobId}`)
            .then(r => r.json())
            .then(data => {
              if (data.graph_id) onDone(data.graph_id)
            })
        }
      } catch {/* ignore */}
    }

    es.onerror = () => {
      setStatus('error')
      es.close()
    }

    return () => {
      es.close()
    }
  }, [jobId])

  // Auto-scroll to bottom
  useEffect(() => {
    if (showAll) {
      logEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }
  }, [events, showAll])

  const filteredEvents = filterType === 'all'
    ? events
    : events.filter(e => e.type === filterType)

  const displayEvents = showAll ? filteredEvents : filteredEvents.slice(-80)

  return (
    <div className="flex flex-col h-full bg-slate-900 text-xs">
      {/* Stats bar */}
      <div className="flex items-center gap-4 px-4 py-2.5 border-b border-slate-700 bg-slate-800/60">
        <div className="flex items-center gap-1.5">
          {status === 'running' && <Loader2 size={12} className="animate-spin text-blue-400" />}
          {status === 'done' && <CheckCircle size={12} className="text-emerald-400" />}
          {status === 'error' && <XCircle size={12} className="text-red-400" />}
          <span className={
            status === 'running' ? 'text-blue-300' :
            status === 'done' ? 'text-emerald-300' : 'text-red-300'
          }>
            {status === 'running' ? '扫描中…' : status === 'done' ? '扫描完成' : '扫描错误'}
          </span>
        </div>

        <div className="flex gap-3 ml-auto text-[11px]">
          <span className="text-slate-400">
            发现 <strong className="text-slate-200">{stats.found}</strong>
          </span>
          <span className="text-emerald-500">
            已建图 <strong className="text-emerald-300">{stats.indexed}</strong>
          </span>
          <span className="text-amber-500">
            已跳过 <strong className="text-amber-300">{stats.skipped}</strong>
          </span>
        </div>
      </div>

      {/* Progress bar */}
      {stats.found > 0 && (
        <div className="h-1 bg-slate-800 flex-shrink-0">
          <div
            className="h-full bg-gradient-to-r from-blue-600 to-emerald-500 transition-all duration-300"
            style={{
              width: status === 'done'
                ? '100%'
                : `${Math.min(95, ((stats.indexed + stats.skipped) / Math.max(stats.found, 1)) * 100)}%`
            }}
          />
        </div>
      )}

      {/* Filter tabs */}
      <div className="flex gap-1 px-3 py-1.5 border-b border-slate-700 overflow-x-auto">
        {['all', 'indexed', 'skip', 'found', 'error'].map(f => (
          <button
            key={f}
            onClick={() => setFilterType(f)}
            className={`px-2 py-0.5 rounded text-[10px] whitespace-nowrap transition-colors ${
              filterType === f
                ? 'bg-slate-600 text-slate-200'
                : 'text-slate-500 hover:text-slate-300'
            }`}
          >
            {f === 'all' ? `全部 (${events.length})`
             : f === 'indexed' ? `已建图 (${events.filter(e => e.type === 'indexed').length})`
             : f === 'skip' ? `已跳过 (${events.filter(e => e.type === 'skip').length})`
             : f === 'found' ? `发现 (${events.filter(e => e.type === 'found').length})`
             : `错误 (${events.filter(e => e.type === 'error').length})`}
          </button>
        ))}
      </div>

      {/* Event log */}
      <div className="flex-1 overflow-y-auto font-mono">
        {!showAll && filteredEvents.length > 80 && (
          <button
            onClick={() => setShowAll(true)}
            className="w-full py-1.5 text-slate-500 hover:text-slate-300 border-b border-slate-700 flex items-center justify-center gap-1"
          >
            <ChevronUp size={10} />
            显示所有 {filteredEvents.length} 条记录
          </button>
        )}

        {displayEvents.map((ev, i) => (
          <div
            key={i}
            className={`flex items-start gap-1.5 px-3 py-0.5 hover:bg-slate-800/40 ${
              ev.type === 'skip' ? 'opacity-50' : ''
            }`}
          >
            {/* Depth indent */}
            <span
              className="flex-shrink-0 text-slate-700"
              style={{ minWidth: `${ev.depth * 8 + 4}px` }}
            >
              {'│ '.repeat(ev.depth)}
            </span>

            {EVENT_ICONS[ev.type] || <span className="w-2.5 flex-shrink-0" />}

            <span className="flex-shrink-0 text-slate-600">
              {DOC_TYPE_ICON[ev.doc_type] || '📄'}
            </span>

            <span className={`flex-1 truncate ${EVENT_COLORS[ev.type] || 'text-slate-400'}`}>
              {ev.title || ev.token || ev.message}
            </span>

            {ev.type === 'skip' && (
              <span className="flex-shrink-0 text-amber-600 text-[9px]">SKIP</span>
            )}
            {ev.type === 'indexed' && ev.message && (
              <span className="flex-shrink-0 text-emerald-700 text-[9px]">
                {ev.message.replace('Indexed: ', '')}
              </span>
            )}
            {ev.type === 'error' && (
              <AlertTriangle size={9} className="flex-shrink-0 text-red-500" />
            )}
          </div>
        ))}

        <div ref={logEndRef} />
      </div>

      {/* Done summary */}
      {status === 'done' && (
        <div className="px-4 py-3 border-t border-slate-700 bg-emerald-900/20 text-emerald-300">
          <div className="flex items-center gap-2">
            <CheckCircle size={14} />
            <span className="font-medium">扫描完成</span>
          </div>
          <div className="mt-1 text-[11px] text-emerald-400/80">
            共发现 {stats.found} 份文档，
            建立图谱 {stats.indexed} 份，
            跳过已知 {stats.skipped} 份
          </div>
        </div>
      )}
    </div>
  )
}
