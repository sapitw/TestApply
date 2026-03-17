/**
 * Main App component for Feishu Knowledge Graph system.
 *
 * Layout:
 * ┌─────────────────────────────────────────────────────────────────┐
 * │  Header: URL input + graph selector + stats                     │
 * ├──────────────┬──────────────────────────────────┬──────────────┤
 * │  Left Panel  │   Central Graph Canvas           │ Right Panel  │
 * │  (Tree/      │   (React Flow visualization)     │ (Node Detail)│
 * │   Search/    │                                  │              │
 * │   Plan/MCP)  │                                  │              │
 * └──────────────┴──────────────────────────────────┴──────────────┘
 */
import React, { useState, useCallback } from 'react'
import {
  Network, Search, BookOpen, Terminal,
  Plus, Loader2, AlertCircle, ChevronLeft, ChevronRight,
  LayoutDashboard, ScanSearch, X, Eye
} from 'lucide-react'
import axios from 'axios'

import { graphApi, type KnowledgeGraph } from './utils/api'
import { useGraphStore } from './stores/graphStore'
import GraphCanvas from './components/GraphCanvas'
import TreeView from './components/TreeView'
import NodeDetailPanel from './components/NodeDetailPanel'
import PlanPanel from './components/PlanPanel'
import MCPPanel from './components/MCPPanel'
import ScanProgressPanel from './components/ScanProgressPanel'

// ─── Search Panel ─────────────────────────────────────────────────────────────

function SearchPanel({ graph }: { graph: KnowledgeGraph }) {
  const { setSelectedNode } = useGraphStore()
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<ReturnType<typeof Array>[]>([])
  const [loading, setLoading] = useState(false)

  const search = async () => {
    if (!query.trim()) return
    setLoading(true)
    try {
      const res = await graphApi.search(graph.id, query)
      setResults(res.results || [])
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex flex-col h-full">
      <div className="p-3">
        <div className="flex gap-2">
          <input
            value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && search()}
            placeholder="Search nodes…"
            className="flex-1 bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-blue-500"
          />
          <button
            onClick={search}
            className="bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded text-xs transition-colors"
          >
            {loading ? <Loader2 size={12} className="animate-spin" /> : <Search size={12} />}
          </button>
        </div>
      </div>
      <div className="flex-1 overflow-y-auto px-2">
        {results.map((r: unknown) => {
          const node = r as { id: string; label: string; type: string; content_preview?: string }
          return (
            <div
              key={node.id}
              onClick={() => {
                const gn = graph.nodes.find(n => n.id === node.id)
                if (gn) setSelectedNode(gn)
              }}
              className="p-2 mb-1 rounded border border-slate-700 hover:border-blue-500/50 cursor-pointer transition-colors"
            >
              <div className="text-xs font-medium text-slate-200 truncate">{node.label}</div>
              <div className="text-[10px] text-slate-500 mt-0.5">{node.type}</div>
              {node.content_preview && (
                <div className="text-[10px] text-slate-400 mt-0.5 line-clamp-2">{node.content_preview}</div>
              )}
            </div>
          )
        })}
        {results.length === 0 && query && !loading && (
          <p className="text-xs text-slate-500 text-center pt-8">No results found</p>
        )}
      </div>
    </div>
  )
}

// ─── Main App ─────────────────────────────────────────────────────────────────

export default function App() {
  const {
    graphs, activeGraphId, selectedNode,
    sidebarTab, setSidebarTab,
    setActiveGraph, storeGraph, removeGraph,
    setLoading, loading, setError, error,
  } = useGraphStore()

  const [url, setUrl] = useState('')
  const [leftCollapsed, setLeftCollapsed] = useState(false)
  const [rightCollapsed, setRightCollapsed] = useState(false)
  // Deep scan state
  const [inputMode, setInputMode] = useState<'single' | 'scan'>('scan')
  const [activeScanJobId, setActiveScanJobId] = useState<string | null>(null)
  const [showScanOverlay, setShowScanOverlay] = useState(false)
  const [clearVisited, setClearVisited] = useState(false)
  const [visitedCount, setVisitedCount] = useState<number | null>(null)

  const activeGraph = activeGraphId ? graphs[activeGraphId] : null
  const graphList = Object.values(graphs)

  // Single document index
  const indexDocument = useCallback(async () => {
    if (!url.trim()) return
    setLoading(true)
    setError(null)
    try {
      const result = await graphApi.index(url.trim())
      const graph = await graphApi.get(result.graph_id)
      storeGraph(graph)
      setActiveGraph(graph.id)
      setUrl('')
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Failed to index document'
      setError(msg)
    } finally {
      setLoading(false)
    }
  }, [url, setLoading, setError, storeGraph, setActiveGraph])

  // Deep scan
  const startDeepScan = useCallback(async () => {
    if (!url.trim()) return
    setError(null)
    try {
      const resp = await axios.post('/api/scan', {
        url: url.trim(),
        root_type: 'auto',
        clear_visited: clearVisited,
      })
      setActiveScanJobId(resp.data.job_id)
      setShowScanOverlay(true)
      setUrl('')
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Failed to start scan'
      setError(msg)
    }
  }, [url, clearVisited, setError])

  // Called when scan finishes
  const onScanDone = useCallback(async (graphId: string) => {
    try {
      const graph = await graphApi.get(graphId)
      storeGraph(graph)
      setActiveGraph(graph.id)
    } catch {/* ignore */}
    // Auto-close overlay after 1.5s
    setTimeout(() => setShowScanOverlay(false), 1500)
  }, [storeGraph, setActiveGraph])

  // Fetch visited count
  const fetchVisitedCount = useCallback(async () => {
    try {
      const resp = await axios.get('/api/visited')
      setVisitedCount(resp.data.count)
    } catch {/* ignore */}
  }, [])

  const clearVisitedCache = useCallback(async () => {
    await axios.delete('/api/visited')
    setVisitedCount(0)
  }, [])

  const SIDEBAR_TABS = [
    { id: 'tree', icon: LayoutDashboard, label: '结构树' },
    { id: 'search', icon: Search, label: '搜索' },
    { id: 'plan', icon: BookOpen, label: '计划' },
    { id: 'mcp', icon: Terminal, label: 'MCP' },
  ] as const

  return (
    <div className="flex flex-col h-screen bg-slate-900 text-slate-200">
      {/* ─── Header ─────────────────────────────────────────── */}
      <header className="flex items-center gap-3 px-4 py-2.5 border-b border-slate-700 bg-slate-900/95 backdrop-blur flex-shrink-0">
        <div className="flex items-center gap-2 mr-2">
          <Network size={18} className="text-blue-400" />
          <span className="font-semibold text-sm text-slate-100">飞书知识图谱</span>
        </div>

        {/* Mode toggle */}
        <div className="flex rounded-lg overflow-hidden border border-slate-600 flex-shrink-0">
          <button
            onClick={() => setInputMode('scan')}
            className={`flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium transition-colors
              ${inputMode === 'scan' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
          >
            <ScanSearch size={12} />
            深度扫描
          </button>
          <button
            onClick={() => setInputMode('single')}
            className={`flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium transition-colors
              ${inputMode === 'single' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
          >
            <Plus size={12} />
            单文档
          </button>
        </div>

        {/* URL Input + Action */}
        <div className="flex-1 flex gap-2 max-w-2xl">
          <input
            value={url}
            onChange={e => setUrl(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter') {
                inputMode === 'scan' ? startDeepScan() : indexDocument()
              }
            }}
            placeholder={
              inputMode === 'scan'
                ? '输入飞书根目录路径（Wiki 知识库、文件夹 URL 或 Token）'
                : '输入飞书文档 URL（文档、表格、多维表格…）'
            }
            className="flex-1 bg-slate-800 border border-slate-600 rounded-lg px-3 py-1.5 text-sm text-slate-200 placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30"
          />

          {inputMode === 'scan' ? (
            <>
              {/* Clear visited toggle */}
              <label className="flex items-center gap-1.5 text-xs text-slate-400 cursor-pointer flex-shrink-0">
                <input
                  type="checkbox"
                  checked={clearVisited}
                  onChange={e => setClearVisited(e.target.checked)}
                  className="accent-blue-500"
                />
                重新扫描
              </label>
              <button
                onClick={startDeepScan}
                disabled={!url.trim()}
                className="flex items-center gap-1.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-1.5 rounded-lg text-sm font-medium transition-colors flex-shrink-0"
              >
                <ScanSearch size={14} />
                开始扫描
              </button>
            </>
          ) : (
            <button
              onClick={indexDocument}
              disabled={loading || !url.trim()}
              className="flex items-center gap-1.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-1.5 rounded-lg text-sm font-medium transition-colors flex-shrink-0"
            >
              {loading ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />}
              {loading ? '处理中…' : '导入'}
            </button>
          )}
        </div>

        {/* Graph tabs */}
        {graphList.length > 0 && (
          <div className="flex gap-1 overflow-x-auto max-w-md">
            {graphList.map(g => (
              <button
                key={g.id}
                onClick={() => setActiveGraph(g.id)}
                className={`flex items-center gap-1.5 px-2.5 py-1 rounded text-xs whitespace-nowrap transition-colors
                  ${activeGraphId === g.id
                    ? 'bg-blue-600/30 border border-blue-500/50 text-blue-300'
                    : 'bg-slate-800 border border-slate-600 text-slate-400 hover:text-slate-200'
                  }`}
              >
                <Network size={10} />
                {g.title.substring(0, 24)}
                <span className="text-[10px] opacity-60">({g.nodes.length})</span>
                <span
                  className="ml-0.5 text-slate-500 hover:text-red-400 transition-colors"
                  onClick={e => { e.stopPropagation(); removeGraph(g.id) }}
                >
                  ×
                </span>
              </button>
            ))}
          </div>
        )}

        {/* Visited cache status */}
        <div className="ml-auto flex items-center gap-2 flex-shrink-0">
          {activeScanJobId && (
            <button
              onClick={() => setShowScanOverlay(true)}
              className="flex items-center gap-1.5 text-[11px] text-blue-400 hover:text-blue-300 transition-colors"
            >
              <Eye size={11} />
              查看进度
            </button>
          )}
          <button
            onClick={fetchVisitedCount}
            className="flex items-center gap-1 text-[11px] text-slate-500 hover:text-slate-300 transition-colors"
            title="查看已扫描页面数量"
          >
            <ScanSearch size={11} />
            {visitedCount !== null ? `${visitedCount} 已缓存` : '缓存'}
          </button>
          {visitedCount !== null && visitedCount > 0 && (
            <button
              onClick={clearVisitedCache}
              className="text-[11px] text-red-500/70 hover:text-red-400 transition-colors"
              title="清除已扫描缓存"
            >
              ✕ 清除
            </button>
          )}
        </div>

        {/* Stats */}
        {activeGraph && (
          <div className="flex gap-3 text-[11px] text-slate-500 flex-shrink-0">
            <span>{activeGraph.nodes.length} nodes</span>
            <span>{activeGraph.edges.length} edges</span>
          </div>
        )}
      </header>

      {/* ─── Scan Progress Overlay ───────────────────────────── */}
      {showScanOverlay && activeScanJobId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div
            className="bg-slate-900 border border-slate-700 rounded-xl shadow-2xl flex flex-col"
            style={{ width: 680, height: 520 }}
          >
            {/* Overlay header */}
            <div className="flex items-center justify-between px-4 py-3 border-b border-slate-700">
              <div className="flex items-center gap-2">
                <ScanSearch size={16} className="text-blue-400" />
                <span className="font-semibold text-sm text-slate-100">深度扫描进度</span>
                <code className="text-[10px] text-slate-500 ml-1">{activeScanJobId.slice(0, 8)}…</code>
              </div>
              <button
                onClick={() => setShowScanOverlay(false)}
                className="text-slate-400 hover:text-slate-200 transition-colors"
              >
                <X size={16} />
              </button>
            </div>
            {/* Panel */}
            <div className="flex-1 min-h-0">
              <ScanProgressPanel
                jobId={activeScanJobId}
                onDone={onScanDone}
              />
            </div>
          </div>
        </div>
      )}

      {/* ─── Error Bar ──────────────────────────────────────── */}
      {error && (
        <div className="flex items-center gap-2 px-4 py-2 bg-red-900/30 border-b border-red-800 text-red-300 text-xs flex-shrink-0">
          <AlertCircle size={12} />
          {error}
          <button onClick={() => setError(null)} className="ml-auto text-red-400 hover:text-red-200">×</button>
        </div>
      )}

      {/* ─── Main Layout ─────────────────────────────────────── */}
      <div className="flex flex-1 min-h-0">
        {/* Left Sidebar */}
        <div
          className="flex flex-col border-r border-slate-700 bg-slate-900 transition-all duration-200 flex-shrink-0"
          style={{ width: leftCollapsed ? 40 : 280 }}
        >
          {leftCollapsed ? (
            <div className="flex flex-col items-center py-3 gap-3">
              <button
                onClick={() => setLeftCollapsed(false)}
                className="text-slate-400 hover:text-slate-200"
              >
                <ChevronRight size={16} />
              </button>
              {SIDEBAR_TABS.map(({ id, icon: Icon }) => (
                <button
                  key={id}
                  onClick={() => { setSidebarTab(id); setLeftCollapsed(false) }}
                  className={`p-1.5 rounded transition-colors ${sidebarTab === id ? 'text-blue-400' : 'text-slate-500 hover:text-slate-300'}`}
                >
                  <Icon size={14} />
                </button>
              ))}
            </div>
          ) : (
            <>
              {/* Tab bar */}
              <div className="flex items-center border-b border-slate-700">
                {SIDEBAR_TABS.map(({ id, icon: Icon, label }) => (
                  <button
                    key={id}
                    onClick={() => setSidebarTab(id)}
                    className={`flex items-center gap-1 px-2 py-2 text-[11px] font-medium transition-colors flex-1 justify-center
                      ${sidebarTab === id
                        ? 'border-b-2 border-blue-500 text-blue-400'
                        : 'text-slate-500 hover:text-slate-300'
                      }`}
                  >
                    <Icon size={11} />
                    {label}
                  </button>
                ))}
                <button
                  onClick={() => setLeftCollapsed(true)}
                  className="p-2 text-slate-500 hover:text-slate-300 flex-shrink-0"
                >
                  <ChevronLeft size={14} />
                </button>
              </div>

              {/* Tab content */}
              <div className="flex-1 min-h-0 overflow-hidden">
                {!activeGraph ? (
                  <div className="flex flex-col items-center justify-center h-full text-slate-500 text-xs gap-3 p-4 text-center">
                    <ScanSearch size={32} className="opacity-30" />
                    <p>输入飞书根目录路径，点击「开始扫描」</p>
                    <p className="text-[10px] text-slate-600">
                      自动递归扫描所有层级，自动跳过已索引页面
                    </p>
                    <div className="flex flex-col gap-1 text-[10px] text-slate-600 mt-2 text-left">
                      <span>📚 Wiki 知识库 → 扫描所有子页面</span>
                      <span>📁 云文档文件夹 → 扫描所有子文件</span>
                      <span>📝 单一文档 → 跟随内部链接</span>
                    </div>
                  </div>
                ) : (
                  <>
                    {sidebarTab === 'tree' && <TreeView graph={activeGraph} />}
                    {sidebarTab === 'search' && <SearchPanel graph={activeGraph} />}
                    {sidebarTab === 'plan' && <PlanPanel graphId={activeGraph.id} />}
                    {sidebarTab === 'mcp' && <MCPPanel />}
                  </>
                )}
              </div>
            </>
          )}
        </div>

        {/* Center: Graph Canvas */}
        <div className="flex-1 min-w-0 relative">
          {!activeGraph ? (
            <div className="flex flex-col items-center justify-center h-full text-slate-600 gap-4">
              <ScanSearch size={64} className="opacity-20" />
              <div className="text-center">
                <p className="text-lg font-medium text-slate-500">飞书知识图谱</p>
                <p className="text-sm mt-1">输入根目录 URL，自动扫描全部层级文档</p>
              </div>
              <div className="flex flex-col gap-2 text-xs text-slate-600 max-w-xs mt-1 bg-slate-800/40 rounded-xl p-4 border border-slate-700">
                <div className="flex items-start gap-2">
                  <span>1️⃣</span>
                  <span>在上方输入飞书 Wiki 或文件夹根目录 URL</span>
                </div>
                <div className="flex items-start gap-2">
                  <span>2️⃣</span>
                  <span>点击「开始扫描」，系统自动递归所有层级</span>
                </div>
                <div className="flex items-start gap-2">
                  <span>3️⃣</span>
                  <span>已扫描过的页面自动跳过，增量更新</span>
                </div>
                <div className="flex items-start gap-2">
                  <span>4️⃣</span>
                  <span>扫描完成后自动生成知识图谱</span>
                </div>
              </div>
            </div>
          ) : (
            <GraphCanvas graph={activeGraph} />
          )}
        </div>

        {/* Right Panel: Node Detail */}
        {selectedNode && (
          <div
            className="flex-shrink-0 border-l border-slate-700 bg-slate-900 overflow-hidden transition-all duration-200"
            style={{ width: rightCollapsed ? 40 : 300 }}
          >
            {rightCollapsed ? (
              <div className="flex flex-col items-center py-3">
                <button onClick={() => setRightCollapsed(false)} className="text-slate-400 hover:text-slate-200">
                  <ChevronLeft size={16} />
                </button>
              </div>
            ) : (
              <div className="flex flex-col h-full">
                <div className="flex items-center justify-between px-3 py-2 border-b border-slate-700">
                  <span className="text-[10px] uppercase font-semibold text-slate-500">节点详情</span>
                  <button onClick={() => setRightCollapsed(true)} className="text-slate-400 hover:text-slate-200">
                    <ChevronRight size={14} />
                  </button>
                </div>
                <div className="flex-1 min-h-0">
                  <NodeDetailPanel node={selectedNode} />
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
