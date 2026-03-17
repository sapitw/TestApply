/**
 * Analytics Panel
 * ===============
 * Knowledge Graph health dashboard + advanced analysis tools.
 *
 * Tabs:
 *   📊 健康度    — structural health metrics (density, orphans, coverage, communities)
 *   🏆 重要度    — PageRank + HITS hub/authority rankings
 *   🔗 中心性    — Betweenness/Closeness centrality (bottleneck detection)
 *   🏘️ 社群     — Community detection clusters
 *   🔍 搜索     — BM25 full-text search
 *   🧩 实体      — Duplicate detection + merge/split
 *   ⚙️ 推断     — Inferred edges preview
 */
import React, { useState, useEffect, useCallback, useRef } from 'react'
import {
  BarChart3, Award, Network, Users, Search,
  Link2, Cpu, Loader2, RefreshCw, AlertCircle,
  ChevronDown, ChevronRight, Merge, Scissors,
  TrendingUp, Eye, ZapOff, CheckCircle2
} from 'lucide-react'
import axios from 'axios'

type AnalyticsTab = 'health' | 'pagerank' | 'centrality' | 'communities' | 'search' | 'entity' | 'inference'

interface AnalyticsPanelProps {
  graphId: string
  onNodeSelect?: (nodeId: string) => void
}

// ─── Shared UI primitives ─────────────────────────────────────────────────────

function StatCard({ label, value, sub, color = 'text-slate-200' }: {
  label: string; value: string | number; sub?: string; color?: string
}) {
  return (
    <div className="bg-slate-800 rounded-lg p-3 flex flex-col gap-0.5">
      <span className="text-[10px] text-slate-500 uppercase tracking-wide">{label}</span>
      <span className={`text-xl font-bold ${color}`}>{value}</span>
      {sub && <span className="text-[10px] text-slate-500">{sub}</span>}
    </div>
  )
}

function ScoreBar({ score, max = 1 }: { score: number; max?: number }) {
  const pct = Math.min((score / max) * 100, 100)
  const color = pct > 66 ? 'bg-emerald-500' : pct > 33 ? 'bg-amber-500' : 'bg-red-500'
  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 h-1.5 bg-slate-700 rounded-full overflow-hidden">
        <div className={`h-full ${color} transition-all`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-[10px] text-slate-400 w-8 text-right">{(score * 100).toFixed(0)}%</span>
    </div>
  )
}

function Section({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  const [open, setOpen] = useState(true)
  return (
    <div className="mb-3">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center gap-1.5 text-[11px] font-semibold text-slate-400 hover:text-slate-200 mb-2 transition-colors"
      >
        {icon}
        {title}
        <ChevronDown size={10} className={`ml-auto transition-transform ${open ? '' : '-rotate-90'}`} />
      </button>
      {open && children}
    </div>
  )
}

function NodeRow({ id, label, type, score, scoreLabel, onSelect }: {
  id: string; label: string; type: string; score: number; scoreLabel: string; onSelect?: (id: string) => void
}) {
  return (
    <div
      className="flex items-center gap-2 py-1.5 px-2 rounded hover:bg-slate-700/50 cursor-pointer group"
      onClick={() => onSelect?.(id)}
    >
      <div className="flex-1 min-w-0">
        <div className="text-[11px] text-slate-200 truncate font-medium">{label}</div>
        <div className="text-[9px] text-slate-500">{type}</div>
      </div>
      <span className="text-[10px] text-amber-400 font-mono flex-shrink-0">{scoreLabel}</span>
    </div>
  )
}

// ─── Health Tab ────────────────────────────────────────────────────────────────

function HealthTab({ graphId }: { graphId: string }) {
  const [data, setData] = useState<Record<string, unknown> | null>(null)
  const [loading, setLoading] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await axios.get(`/analytics/graphs/${graphId}/health`)
      setData(res.data.metrics)
    } catch {/* */} finally { setLoading(false) }
  }, [graphId])

  useEffect(() => { load() }, [load])

  if (loading) return <div className="flex justify-center py-8"><Loader2 size={18} className="animate-spin text-slate-500" /></div>
  if (!data) return null

  const m = data as Record<string, number | string | Record<string, number>>
  const coverage = m.coverage_score as number
  const connectivity = m.connectivity_score as number
  const density = m.density as number

  return (
    <div className="space-y-4">
      {/* Key metrics grid */}
      <div className="grid grid-cols-2 gap-2">
        <StatCard label="节点数" value={m.node_count as number} />
        <StatCard label="边数" value={m.edge_count as number} />
        <StatCard label="孤立节点" value={m.orphan_nodes as number} color={m.orphan_nodes as number > 0 ? 'text-amber-400' : 'text-emerald-400'} />
        <StatCard label="无内容节点" value={m.nodes_without_content as number} color={m.nodes_without_content as number > 0 ? 'text-amber-400' : 'text-emerald-400'} />
        <StatCard label="连通分量" value={m.weakly_connected_components as number} sub="weakly connected" />
        <StatCard label="图直径" value={(m.diameter as number) ?? '—'} sub="max path length" />
      </div>

      {/* Score bars */}
      <Section title="健康度评分" icon={<TrendingUp size={11} />}>
        <div className="space-y-2">
          <div>
            <div className="flex justify-between text-[10px] text-slate-400 mb-1">
              <span>内容覆盖率</span>
              <span>{((coverage) * 100).toFixed(1)}%</span>
            </div>
            <ScoreBar score={coverage} />
          </div>
          <div>
            <div className="flex justify-between text-[10px] text-slate-400 mb-1">
              <span>连通性</span>
              <span>{((connectivity) * 100).toFixed(1)}%</span>
            </div>
            <ScoreBar score={connectivity} />
          </div>
          <div>
            <div className="flex justify-between text-[10px] text-slate-400 mb-1">
              <span>图密度</span>
              <span>{(density * 100).toFixed(2)}%</span>
            </div>
            <ScoreBar score={Math.min(density * 20, 1)} />
          </div>
        </div>
      </Section>

      {/* Node type distribution */}
      <Section title="节点类型分布" icon={<BarChart3 size={11} />}>
        <div className="space-y-1">
          {Object.entries((m.node_type_distribution as Record<string, number>) || {}).map(([type, count]) => (
            <div key={type} className="flex items-center gap-2">
              <span className="text-[10px] text-slate-400 w-28 truncate">{type}</span>
              <div className="flex-1 h-1.5 bg-slate-700 rounded-full overflow-hidden">
                <div className="h-full bg-blue-600" style={{ width: `${Math.min(count / (m.node_count as number) * 100 * 5, 100)}%` }} />
              </div>
              <span className="text-[10px] text-slate-400 w-8 text-right">{count}</span>
            </div>
          ))}
        </div>
      </Section>

      <button onClick={load} className="w-full text-center text-[10px] text-slate-500 hover:text-slate-300 py-1 transition-colors">
        <RefreshCw size={10} className="inline mr-1" />刷新
      </button>
    </div>
  )
}

// ─── PageRank Tab ─────────────────────────────────────────────────────────────

function PagerankTab({ graphId, onNodeSelect }: { graphId: string; onNodeSelect?: (id: string) => void }) {
  const [data, setData] = useState<{ results: unknown[]; hubs: unknown[]; authorities: unknown[] } | null>(null)
  const [loading, setLoading] = useState(false)
  const [tab, setTab] = useState<'pagerank' | 'hubs' | 'authorities'>('pagerank')

  useEffect(() => {
    setLoading(true)
    Promise.all([
      axios.get(`/analytics/graphs/${graphId}/pagerank?top_k=15`),
      axios.get(`/analytics/graphs/${graphId}/hits?top_k=10`),
    ]).then(([pr, hits]) => {
      setData({ results: pr.data.results, hubs: hits.data.hubs, authorities: hits.data.authorities })
    }).catch(() => {}).finally(() => setLoading(false))
  }, [graphId])

  if (loading) return <div className="flex justify-center py-8"><Loader2 size={18} className="animate-spin text-slate-500" /></div>
  if (!data) return null

  const items = tab === 'pagerank' ? data.results : tab === 'hubs' ? data.hubs : data.authorities
  const scoreKey = tab === 'pagerank' ? 'pagerank' : 'score'

  return (
    <div>
      <div className="flex gap-1 mb-3">
        {(['pagerank', 'hubs', 'authorities'] as const).map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={`px-2 py-0.5 rounded text-[10px] transition-colors ${tab === t ? 'bg-amber-700/60 text-amber-200' : 'text-slate-500 hover:text-slate-300'}`}>
            {t === 'pagerank' ? '📊 PageRank' : t === 'hubs' ? '🔗 枢纽' : '⭐ 权威'}
          </button>
        ))}
      </div>
      <div className="space-y-0.5">
        {(items as Record<string, unknown>[]).map((node, i) => (
          <div key={node.id as string} className="flex items-center gap-2 py-1 px-2 hover:bg-slate-700/40 rounded cursor-pointer"
               onClick={() => onNodeSelect?.(node.id as string)}>
            <span className="text-[10px] text-slate-600 w-4">{i + 1}</span>
            <div className="flex-1 min-w-0">
              <div className="text-[11px] text-slate-200 truncate">{node.label as string}</div>
              <div className="text-[9px] text-slate-500">{node.type as string}</div>
            </div>
            <span className="text-[10px] font-mono text-amber-400">{((node[scoreKey] as number) * 1000).toFixed(2)}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

// ─── Centrality Tab ───────────────────────────────────────────────────────────

function CentralityTab({ graphId, onNodeSelect }: { graphId: string; onNodeSelect?: (id: string) => void }) {
  const [data, setData] = useState<{ betweenness: unknown[]; closeness: unknown[] } | null>(null)
  const [loading, setLoading] = useState(false)
  const [tab, setTab] = useState<'betweenness' | 'closeness'>('betweenness')

  useEffect(() => {
    setLoading(true)
    axios.get(`/analytics/graphs/${graphId}/centrality?type=both&top_k=15`)
      .then(r => setData(r.data))
      .catch(() => {}).finally(() => setLoading(false))
  }, [graphId])

  if (loading) return <div className="flex justify-center py-8"><Loader2 size={18} className="animate-spin text-slate-500" /></div>
  if (!data) return null

  const items = (tab === 'betweenness' ? data.betweenness : data.closeness) as Record<string, unknown>[]
  const scoreKey = tab === 'betweenness' ? 'betweenness' : 'closeness'

  return (
    <div>
      <div className="flex gap-1 mb-3">
        <button onClick={() => setTab('betweenness')}
          className={`px-2 py-0.5 rounded text-[10px] transition-colors ${tab === 'betweenness' ? 'bg-red-700/60 text-red-200' : 'text-slate-500 hover:text-slate-300'}`}>
          ⚠️ 关键桥梁
        </button>
        <button onClick={() => setTab('closeness')}
          className={`px-2 py-0.5 rounded text-[10px] transition-colors ${tab === 'closeness' ? 'bg-cyan-700/60 text-cyan-200' : 'text-slate-500 hover:text-slate-300'}`}>
          🌐 可及性
        </button>
      </div>
      <p className="text-[10px] text-slate-500 mb-2 italic">
        {tab === 'betweenness'
          ? '高中介中心性节点是图谱中的关键桥梁，删除将导致连通性下降'
          : '高接近中心性节点能快速到达图谱中所有其他节点'}
      </p>
      <div className="space-y-0.5">
        {items.map((node, i) => (
          <div key={node.id as string} className="flex items-center gap-2 py-1 px-2 hover:bg-slate-700/40 rounded cursor-pointer"
               onClick={() => onNodeSelect?.(node.id as string)}>
            <span className="text-[10px] text-slate-600 w-4">{i + 1}</span>
            <div className="flex-1 min-w-0">
              <div className="text-[11px] text-slate-200 truncate">{node.label as string}</div>
              <div className="text-[9px] text-slate-500">{node.type as string}</div>
            </div>
            <ScoreBar score={node[scoreKey] as number} />
          </div>
        ))}
      </div>
    </div>
  )
}

// ─── Communities Tab ──────────────────────────────────────────────────────────

function CommunitiesTab({ graphId, onNodeSelect }: { graphId: string; onNodeSelect?: (id: string) => void }) {
  const [communities, setCommunities] = useState<Record<string, unknown>[]>([])
  const [loading, setLoading] = useState(false)
  const [expanded, setExpanded] = useState<Set<number>>(new Set([0]))

  useEffect(() => {
    setLoading(true)
    axios.get(`/analytics/graphs/${graphId}/communities`)
      .then(r => setCommunities(r.data.communities || []))
      .catch(() => {}).finally(() => setLoading(false))
  }, [graphId])

  if (loading) return <div className="flex justify-center py-8"><Loader2 size={18} className="animate-spin text-slate-500" /></div>
  if (!communities.length) return <div className="text-center text-slate-500 text-xs py-8">节点数不足，无法检测社群</div>

  return (
    <div className="space-y-2">
      <p className="text-[10px] text-slate-500 italic">
        贪心模块度算法检测到 {communities.length} 个主题社群
      </p>
      {communities.map((c) => {
        const cid = c.community_id as number
        const isOpen = expanded.has(cid)
        const members = c.members as Record<string, string>[]
        return (
          <div key={cid} className="border border-slate-700 rounded-lg overflow-hidden">
            <button
              onClick={() => setExpanded(prev => { const s = new Set(prev); s.has(cid) ? s.delete(cid) : s.add(cid); return s })}
              className="w-full flex items-center gap-2 px-3 py-2 hover:bg-slate-700/40 transition-colors"
            >
              <span className="w-5 h-5 rounded-full bg-blue-700/60 text-[9px] text-blue-200 flex items-center justify-center font-bold">{cid + 1}</span>
              <span className="text-[11px] text-slate-300 flex-1 text-left truncate">
                {members.slice(0, 2).map(m => m.label).join(' · ')}{members.length > 2 ? ` +${members.length - 2}` : ''}
              </span>
              <span className="text-[10px] text-slate-500">{c.size as number} 个节点</span>
              <ChevronDown size={10} className={`text-slate-500 transition-transform ${isOpen ? '' : '-rotate-90'}`} />
            </button>
            {isOpen && (
              <div className="px-3 pb-2 space-y-0.5 border-t border-slate-700">
                {members.map(m => (
                  <div key={m.id} className="flex items-center gap-2 py-0.5 cursor-pointer hover:text-slate-200 text-slate-400 text-[10px]"
                       onClick={() => onNodeSelect?.(m.id)}>
                    <span className="truncate">{m.label}</span>
                    <span className="text-slate-600">[{m.type}]</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}

// ─── BM25 Search Tab ──────────────────────────────────────────────────────────

function SearchTab({ graphId, onNodeSelect }: { graphId: string; onNodeSelect?: (id: string) => void }) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<Record<string, unknown>[]>([])
  const [loading, setLoading] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout>>()

  const search = useCallback((q: string) => {
    if (!q.trim()) { setResults([]); return }
    setLoading(true)
    axios.post(`/analytics/graphs/${graphId}/search`, { query: q, top_k: 30 })
      .then(r => setResults(r.data.results || []))
      .catch(() => setResults([]))
      .finally(() => setLoading(false))
  }, [graphId])

  const onInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const q = e.target.value
    setQuery(q)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => search(q), 350)
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="relative">
        <Search size={12} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-500" />
        <input
          value={query}
          onChange={onInput}
          placeholder="BM25 全文搜索…"
          className="w-full bg-slate-800 border border-slate-600 rounded-lg pl-7 pr-3 py-2 text-[12px] text-slate-200 placeholder-slate-500 focus:outline-none focus:border-blue-500"
        />
        {loading && <Loader2 size={12} className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-500 animate-spin" />}
      </div>

      {results.length > 0 && (
        <div className="space-y-0.5">
          {results.map(r => (
            <div key={r.id as string}
              className="flex items-start gap-2 py-1.5 px-2 hover:bg-slate-700/40 rounded cursor-pointer"
              onClick={() => onNodeSelect?.(r.id as string)}>
              <div className="flex-1 min-w-0">
                <div className="text-[11px] text-slate-200 font-medium truncate">{r.label as string}</div>
                <div className="text-[10px] text-slate-500 truncate">{r.content_preview as string}</div>
                <div className="text-[9px] text-slate-600">{r.type as string}</div>
              </div>
              <span className="text-[10px] font-mono text-emerald-400 flex-shrink-0">{(r.score as number).toFixed(2)}</span>
            </div>
          ))}
        </div>
      )}

      {query && !loading && results.length === 0 && (
        <div className="text-center text-slate-500 text-xs py-4">无结果</div>
      )}
    </div>
  )
}

// ─── Entity Tab ───────────────────────────────────────────────────────────────

function EntityTab({ graphId }: { graphId: string }) {
  const [candidates, setCandidates] = useState<Record<string, unknown>[]>([])
  const [loading, setLoading] = useState(false)
  const [merging, setMerging] = useState<string | null>(null)
  const [message, setMessage] = useState('')

  const findDuplicates = async () => {
    setLoading(true)
    try {
      const r = await axios.post(`/analytics/graphs/${graphId}/entity/find-duplicates`, { similarity_threshold: 0.80 })
      setCandidates(r.data.candidates || [])
    } catch { setMessage('扫描失败') } finally { setLoading(false) }
  }

  const merge = async (canonicalId: string, dupId: string) => {
    setMerging(dupId)
    try {
      await axios.post(`/analytics/graphs/${graphId}/entity/merge`, { canonical_id: canonicalId, duplicate_id: dupId })
      setCandidates(prev => prev.filter(c => c.node_a_id !== dupId && c.node_b_id !== dupId))
      setMessage(`✅ 已合并`)
    } catch (e: unknown) {
      const err = e as { response?: { data?: { detail?: string } } }
      setMessage('❌ ' + (err.response?.data?.detail || '合并失败'))
    } finally { setMerging(null) }
  }

  return (
    <div className="space-y-3">
      <button onClick={findDuplicates} disabled={loading}
        className="w-full py-2 bg-purple-800/60 hover:bg-purple-700/60 disabled:opacity-40 text-[11px] text-purple-200 rounded-lg flex items-center justify-center gap-2 transition-colors">
        {loading ? <Loader2 size={12} className="animate-spin" /> : <Eye size={12} />}
        扫描重复实体
      </button>

      {message && <div className="text-[11px] text-slate-300 bg-slate-800 rounded p-2">{message}</div>}

      {candidates.length > 0 && (
        <div className="space-y-2">
          <div className="text-[10px] text-slate-500">发现 {candidates.length} 对可能重复的节点：</div>
          {candidates.map((c, i) => (
            <div key={i} className="border border-purple-800/40 rounded-lg p-2">
              <div className="flex items-start gap-2 mb-2">
                <div className="flex-1 text-[11px]">
                  <div className="text-slate-200 font-medium">{c.node_a_label as string}</div>
                  <div className="text-slate-500 text-[9px]">{c.node_a_id as string}</div>
                </div>
                <span className="text-amber-400 font-mono text-[10px]">{((c.similarity as number) * 100).toFixed(0)}%</span>
                <div className="flex-1 text-right text-[11px]">
                  <div className="text-slate-200 font-medium">{c.node_b_label as string}</div>
                  <div className="text-slate-500 text-[9px]">{c.node_b_id as string}</div>
                </div>
              </div>
              <div className="text-[9px] text-slate-600 mb-2">相似原因：{c.reason as string}</div>
              <div className="flex gap-1">
                <button
                  onClick={() => merge(c.node_a_id as string, c.node_b_id as string)}
                  disabled={merging === c.node_b_id}
                  className="flex-1 text-[10px] bg-slate-700 hover:bg-slate-600 text-slate-200 py-1 rounded flex items-center justify-center gap-1 transition-colors"
                >
                  <Merge size={9} />
                  以左为主
                </button>
                <button
                  onClick={() => merge(c.node_b_id as string, c.node_a_id as string)}
                  disabled={merging === c.node_a_id}
                  className="flex-1 text-[10px] bg-slate-700 hover:bg-slate-600 text-slate-200 py-1 rounded flex items-center justify-center gap-1 transition-colors"
                >
                  <Merge size={9} />
                  以右为主
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {!loading && candidates.length === 0 && (
        <div className="text-center text-slate-500 text-xs py-6">
          <CheckCircle2 size={24} className="mx-auto mb-2 opacity-30 text-emerald-500" />
          点击「扫描重复实体」开始检测
        </div>
      )}
    </div>
  )
}

// ─── Inference Tab ────────────────────────────────────────────────────────────

function InferenceTab({ graphId }: { graphId: string }) {
  const [data, setData] = useState<Record<string, unknown> | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    setLoading(true)
    axios.get(`/analytics/graphs/${graphId}/inference`)
      .then(r => setData(r.data))
      .catch(() => {}).finally(() => setLoading(false))
  }, [graphId])

  if (loading) return <div className="flex justify-center py-8"><Loader2 size={18} className="animate-spin text-slate-500" /></div>
  if (!data) return null

  const byRule = data.by_rule as Record<string, { count: number; sample: Record<string, unknown>[] }>
  const total = data.total_inferred as number

  const RULE_LABELS: Record<string, string> = {
    transitive: '📐 传递闭包',
    inverse: '↔️ 逆向关系',
    symmetric: '⟺ 对称关系',
    co_occurrence: '👥 共现关系',
    path_inference: '🔗 路径推断',
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 bg-slate-800 rounded-lg px-3 py-2">
        <Cpu size={12} className="text-cyan-400" />
        <span className="text-[11px] text-slate-300">共推断出 <span className="font-bold text-cyan-400">{total}</span> 条隐含关系</span>
      </div>
      <p className="text-[10px] text-slate-500 italic">推断边不存储，每次实时计算。可用于自动补全知识图谱。</p>

      {Object.entries(byRule).map(([rule, info]) => (
        <Section key={rule} title={`${RULE_LABELS[rule] || rule}  (${info.count})`} icon={<Link2 size={10} />}>
          <div className="space-y-1">
            {info.sample.map((e, i) => (
              <div key={i} className="flex items-center gap-1 text-[10px] text-slate-400 py-0.5">
                <span className="truncate text-slate-300">{e.source_label as string}</span>
                <span className="text-slate-600 flex-shrink-0">—{e.type as string}→</span>
                <span className="truncate text-slate-300">{e.target_label as string}</span>
                <span className="text-slate-600 flex-shrink-0 ml-auto">{((e.weight as number)).toFixed(1)}</span>
              </div>
            ))}
          </div>
        </Section>
      ))}
    </div>
  )
}

// ─── Main Panel ───────────────────────────────────────────────────────────────

const TABS: { id: AnalyticsTab; icon: React.ReactNode; label: string }[] = [
  { id: 'health',      icon: <BarChart3 size={10} />,   label: '健康度' },
  { id: 'pagerank',    icon: <Award size={10} />,        label: '重要度' },
  { id: 'centrality',  icon: <Network size={10} />,      label: '中心性' },
  { id: 'communities', icon: <Users size={10} />,        label: '社群' },
  { id: 'search',      icon: <Search size={10} />,       label: '搜索' },
  { id: 'entity',      icon: <Merge size={10} />,        label: '实体' },
  { id: 'inference',   icon: <Cpu size={10} />,          label: '推断' },
]

export default function AnalyticsPanel({ graphId, onNodeSelect }: AnalyticsPanelProps) {
  const [activeTab, setActiveTab] = useState<AnalyticsTab>('health')

  return (
    <div className="flex flex-col h-full">
      {/* Tab bar — 2 rows of 4 + 3 */}
      <div className="flex flex-wrap gap-0.5 px-2 py-1.5 border-b border-slate-700 flex-shrink-0">
        {TABS.map(t => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className={`flex items-center gap-1 px-2 py-1 rounded text-[10px] transition-colors ${
              activeTab === t.id ? 'bg-slate-600 text-slate-100' : 'text-slate-500 hover:text-slate-300'
            }`}
          >
            {t.icon}{t.label}
          </button>
        ))}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-3">
        {activeTab === 'health'      && <HealthTab graphId={graphId} />}
        {activeTab === 'pagerank'    && <PagerankTab graphId={graphId} onNodeSelect={onNodeSelect} />}
        {activeTab === 'centrality'  && <CentralityTab graphId={graphId} onNodeSelect={onNodeSelect} />}
        {activeTab === 'communities' && <CommunitiesTab graphId={graphId} onNodeSelect={onNodeSelect} />}
        {activeTab === 'search'      && <SearchTab graphId={graphId} onNodeSelect={onNodeSelect} />}
        {activeTab === 'entity'      && <EntityTab graphId={graphId} />}
        {activeTab === 'inference'   && <InferenceTab graphId={graphId} />}
      </div>
    </div>
  )
}
