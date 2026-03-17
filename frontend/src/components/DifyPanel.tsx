/**
 * DIFY Integration panel.
 * Shows setup instructions, API key management, tool testing,
 * and quick-copy URLs for DIFY Custom Tool import.
 */
import React, { useState, useEffect } from 'react'
import {
  Copy, Check, Key, Plus, Trash2, ExternalLink,
  Zap, Terminal, BookOpen, Wrench, Search, Network,
  ChevronDown, ChevronRight, Play, Loader2
} from 'lucide-react'
import axios from 'axios'

// ─── Copy button helper ───────────────────────────────────────────────────────

function CopyButton({ text, label = '' }: { text: string; label?: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <button
      onClick={() => { navigator.clipboard.writeText(text); setCopied(true); setTimeout(() => setCopied(false), 2000) }}
      className="flex items-center gap-1 text-[11px] text-slate-400 hover:text-slate-200 transition-colors flex-shrink-0"
    >
      {copied ? <Check size={11} className="text-emerald-400" /> : <Copy size={11} />}
      {label && <span>{copied ? '已复制' : label}</span>}
    </button>
  )
}

// ─── Collapsible section ──────────────────────────────────────────────────────

function Section({ title, icon, children, defaultOpen = true }: {
  title: string; icon: React.ReactNode; children: React.ReactNode; defaultOpen?: boolean
}) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="border border-slate-700 rounded-lg overflow-hidden mb-3">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-3 py-2 bg-slate-800/60 hover:bg-slate-700/40 transition-colors"
      >
        <div className="flex items-center gap-2 text-xs font-medium text-slate-300">
          {icon}
          {title}
        </div>
        {open ? <ChevronDown size={12} className="text-slate-500" /> : <ChevronRight size={12} className="text-slate-500" />}
      </button>
      {open && <div className="p-3">{children}</div>}
    </div>
  )
}

// ─── Main Panel ───────────────────────────────────────────────────────────────

export default function DifyPanel() {
  const [apiKey, setApiKey] = useState('')
  const [keyName, setKeyName] = useState('dify-agent')
  const [generatedKey, setGeneratedKey] = useState('')
  const [keys, setKeys] = useState<Array<{ name: string; hash_prefix: string }>>([])
  const [baseUrl, setBaseUrl] = useState(window.location.origin)
  const [testTool, setTestTool] = useState('list_all_graphs')
  const [testResult, setTestResult] = useState('')
  const [testLoading, setTestLoading] = useState(false)

  const schemaUrl = `${baseUrl}/dify/openapi.json`
  const manifestUrl = `${baseUrl}/dify/manifest.yaml`

  const loadKeys = async () => {
    try {
      const res = await axios.get('/dify/admin/keys')
      setKeys(res.data.keys || [])
    } catch { /* ignore */ }
  }

  useEffect(() => { loadKeys() }, [])

  const createKey = async () => {
    try {
      const res = await axios.post('/dify/admin/keys', { name: keyName })
      setGeneratedKey(res.data.api_key)
      await loadKeys()
    } catch (e: unknown) {
      alert('创建失败：' + (e instanceof Error ? e.message : String(e)))
    }
  }

  const revokeKey = async (name: string) => {
    try {
      await axios.delete(`/dify/admin/keys/${name}`)
      await loadKeys()
    } catch { /* ignore */ }
  }

  const testEndpoint = async () => {
    setTestLoading(true)
    setTestResult('')
    const headers = apiKey ? { Authorization: `Bearer ${apiKey}` } : {}
    try {
      let resp: { data: string }
      if (testTool === 'list_all_graphs') {
        resp = await axios.get('/dify/graphs', { headers })
      } else if (testTool === 'get_cache_status') {
        resp = await axios.get('/dify/cache-status', { headers })
      } else if (testTool === 'get_graph_overview') {
        const graphId = prompt('请输入 graph_id：')
        if (!graphId) { setTestLoading(false); return }
        resp = await axios.get(`/dify/overview?graph_id=${graphId}`, { headers })
      } else if (testTool === 'query_knowledge_graph') {
        const graphId = prompt('请输入 graph_id：')
        const q = prompt('请输入搜索关键字：')
        if (!graphId || !q) { setTestLoading(false); return }
        resp = await axios.get(`/dify/query?graph_id=${graphId}&q=${encodeURIComponent(q)}`, { headers })
      } else {
        resp = await axios.get(`/dify/${testTool}`, { headers })
      }
      setTestResult(typeof resp.data === 'string' ? resp.data : JSON.stringify(resp.data, null, 2))
    } catch (e: unknown) {
      const err = e as { response?: { data?: unknown }; message?: string }
      setTestResult('Error: ' + JSON.stringify(err.response?.data || err.message, null, 2))
    } finally {
      setTestLoading(false)
    }
  }

  const DIFY_STEPS = [
    { step: '1', text: '在 DIFY 后台前往「工具」→「自定义工具」→「创建工具」' },
    { step: '2', text: '选择「导入 OpenAPI Schema」，粘贴下方 Schema URL' },
    { step: '3', text: '填入认证方式：Bearer Token，输入上方生成的 API Key' },
    { step: '4', text: '点击「测试」验证连接，确认各工具可正常呼叫' },
    { step: '5', text: '在 DIFY 对话工作流中添加工具节点，选择飞书知识图谱工具' },
    { step: '6', text: 'LLM 将自动根据用户问题选择并呼叫相应工具' },
  ]

  const TOOLS_OVERVIEW = [
    { name: 'scan_feishu_root', icon: <Network size={11} />, desc: '深度扫描整个知识库，建立图谱' },
    { name: 'index_feishu_document', icon: <Plus size={11} />, desc: '索引单一文档' },
    { name: 'query_knowledge_graph', icon: <Search size={11} />, desc: '关键字查询知识内容' },
    { name: 'get_graph_overview', icon: <Network size={11} />, desc: '取得知识库顶层结构' },
    { name: 'get_node_subtree', icon: <ChevronRight size={11} />, desc: '展开章节子树内容' },
    { name: 'generate_training_plan', icon: <BookOpen size={11} />, desc: '生成培训计划' },
    { name: 'generate_maintenance_plan', icon: <Wrench size={11} />, desc: '生成运维计划' },
    { name: 'analyze_code_dependencies', icon: <Terminal size={11} />, desc: '分析代码上下游依赖' },
    { name: 'export_knowledge_graph', icon: <Copy size={11} />, desc: '导出 Markdown/Mermaid/JSON' },
    { name: 'list_all_graphs', icon: <Network size={11} />, desc: '列出所有已建知识图谱' },
    { name: 'get_cache_status', icon: <Zap size={11} />, desc: '查看已缓存页面' },
  ]

  return (
    <div className="flex flex-col h-full overflow-y-auto text-xs p-3 space-y-1">

      {/* ─── Schema URLs ──────────────────────────────────── */}
      <Section title="DIFY 导入 URL" icon={<Zap size={12} className="text-yellow-400" />}>
        <div className="space-y-2">
          <div className="bg-slate-900 border border-slate-700 rounded p-2">
            <div className="flex items-center justify-between mb-1">
              <span className="text-[10px] uppercase text-slate-500 font-semibold">OpenAPI Schema URL</span>
              <CopyButton text={schemaUrl} label="复制" />
            </div>
            <code className="text-blue-300 text-[11px] break-all">{schemaUrl}</code>
          </div>
          <p className="text-slate-500 text-[10px]">
            在 DIFY →「工具」→「自定义工具」→ 粘贴此 URL 即可导入全部 {TOOLS_OVERVIEW.length} 个工具。
          </p>
          <div className="bg-slate-900 border border-slate-700 rounded p-2">
            <div className="flex items-center justify-between mb-1">
              <span className="text-[10px] uppercase text-slate-500 font-semibold">Base URL（自定义）</span>
            </div>
            <input
              value={baseUrl}
              onChange={e => setBaseUrl(e.target.value)}
              className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1 text-[11px] text-slate-200 focus:outline-none focus:border-blue-500"
              placeholder="http://localhost:8000"
            />
            <p className="text-slate-600 text-[10px] mt-1">若使用 ngrok 或反代，请修改为对外 URL</p>
          </div>
        </div>
      </Section>

      {/* ─── Setup Steps ─────────────────────────────────── */}
      <Section title="接入步骤" icon={<ChevronRight size={12} className="text-blue-400" />} defaultOpen={false}>
        <div className="space-y-2">
          {DIFY_STEPS.map(s => (
            <div key={s.step} className="flex gap-2 items-start">
              <span className="flex-shrink-0 w-5 h-5 bg-blue-600/30 text-blue-400 rounded-full flex items-center justify-center text-[10px] font-bold mt-0.5">
                {s.step}
              </span>
              <span className="text-slate-300 text-[11px] leading-relaxed">{s.text}</span>
            </div>
          ))}
          <a
            href="https://docs.dify.ai/guides/tools/tool-configuration/custom-tool"
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1 text-blue-400 hover:text-blue-300 text-[11px] mt-2"
          >
            <ExternalLink size={11} />
            DIFY Custom Tool 官方文档
          </a>
        </div>
      </Section>

      {/* ─── API Key Management ───────────────────────────── */}
      <Section title="API Key 管理" icon={<Key size={12} className="text-emerald-400" />}>
        <div className="space-y-3">
          {/* Create key */}
          <div className="flex gap-2">
            <input
              value={keyName}
              onChange={e => setKeyName(e.target.value)}
              placeholder="Key 名称（如 dify-prod）"
              className="flex-1 bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 placeholder-slate-500 focus:outline-none focus:border-blue-500"
            />
            <button
              onClick={createKey}
              className="flex items-center gap-1 bg-emerald-700 hover:bg-emerald-600 text-white px-3 py-1.5 rounded text-[11px] font-medium transition-colors"
            >
              <Plus size={11} />
              生成
            </button>
          </div>

          {generatedKey && (
            <div className="bg-emerald-900/30 border border-emerald-700 rounded p-2">
              <div className="flex items-center justify-between mb-1">
                <span className="text-[10px] text-emerald-400 font-semibold">新 API Key（只显示一次）</span>
                <CopyButton text={generatedKey} label="复制" />
              </div>
              <code className="text-emerald-300 text-[10px] break-all">{generatedKey}</code>
              <p className="text-emerald-600 text-[10px] mt-1">请立即复制并保存，关闭后无法再次查看。</p>
            </div>
          )}

          {/* Existing keys */}
          {keys.length > 0 && (
            <div>
              <p className="text-[10px] uppercase text-slate-500 font-semibold mb-1">现有 Keys</p>
              <div className="space-y-1">
                {keys.map(k => (
                  <div key={k.hash_prefix} className="flex items-center justify-between bg-slate-800 rounded px-2 py-1.5">
                    <div>
                      <span className="text-slate-200 text-[11px]">{k.name}</span>
                      <span className="text-slate-500 text-[10px] ml-2">#{k.hash_prefix}…</span>
                    </div>
                    <button onClick={() => revokeKey(k.name)} className="text-red-500/60 hover:text-red-400 transition-colors">
                      <Trash2 size={11} />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="bg-slate-800/40 rounded p-2 text-[10px] text-slate-500">
            若 <code className="text-slate-400">DIFY_API_KEY</code> 环境变量未设定，
            且无任何已注册 Key，所有请求均被允许（开发模式）。
            生产环境请务必设定 API Key。
          </div>
        </div>
      </Section>

      {/* ─── Tool List ───────────────────────────────────── */}
      <Section title={`可用工具（${TOOLS_OVERVIEW.length} 个）`} icon={<Zap size={12} className="text-purple-400" />} defaultOpen={false}>
        <div className="space-y-1">
          {TOOLS_OVERVIEW.map(t => (
            <div key={t.name} className="flex items-center gap-2 px-2 py-1.5 rounded hover:bg-slate-800 transition-colors">
              <span className="text-purple-400 flex-shrink-0">{t.icon}</span>
              <code className="text-slate-300 text-[10px]">{t.name}</code>
              <span className="text-slate-500 text-[10px] ml-auto text-right">{t.desc}</span>
            </div>
          ))}
        </div>
      </Section>

      {/* ─── Test Tool ───────────────────────────────────── */}
      <Section title="在线测试工具" icon={<Play size={12} className="text-blue-400" />} defaultOpen={false}>
        <div className="space-y-2">
          <div>
            <label className="text-[10px] text-slate-500 block mb-1">API Key（留空为开发模式）</label>
            <input
              value={apiKey}
              onChange={e => setApiKey(e.target.value)}
              placeholder="Bearer token…"
              className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 placeholder-slate-500 focus:outline-none focus:border-blue-500"
            />
          </div>
          <div>
            <label className="text-[10px] text-slate-500 block mb-1">选择工具</label>
            <select
              value={testTool}
              onChange={e => setTestTool(e.target.value)}
              className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-[11px] text-slate-200 focus:outline-none focus:border-blue-500"
            >
              <option value="list_all_graphs">list_all_graphs — 列出所有图谱</option>
              <option value="cache-status">get_cache_status — 查看缓存</option>
              <option value="get_graph_overview">get_graph_overview — 图谱概览</option>
              <option value="query_knowledge_graph">query_knowledge_graph — 关键字查询</option>
            </select>
          </div>
          <button
            onClick={testEndpoint}
            disabled={testLoading}
            className="w-full flex items-center justify-center gap-1.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-3 py-1.5 rounded text-[11px] font-medium transition-colors"
          >
            {testLoading ? <Loader2 size={11} className="animate-spin" /> : <Play size={11} />}
            {testLoading ? '呼叫中…' : '执行'}
          </button>
          {testResult && (
            <pre className="bg-slate-900 border border-slate-700 rounded p-2 text-[10px] text-slate-300 overflow-x-auto whitespace-pre-wrap max-h-48 overflow-y-auto">
              {testResult}
            </pre>
          )}
        </div>
      </Section>

      {/* ─── Example Prompts ─────────────────────────────── */}
      <Section title="DIFY 对话示例指令" icon={<BookOpen size={12} className="text-amber-400" />} defaultOpen={false}>
        <div className="space-y-1.5">
          {[
            '帮我扫描这个飞书知识库：https://xxx.feishu.cn/wiki/TOKEN',
            '知识库里有没有关于「部署流程」的内容？',
            '给我展示「数据库设计」章节的详细内容',
            '根据这个知识库，帮我生成新进工程师的培训计划',
            '生成一份数据库运维计划',
            '分析一下这个知识库里代码的上下游依赖关系',
            '把整个知识图谱导出成 Mermaid 图表',
            '目前有哪些飞书知识库已经建立了图谱？',
          ].map((prompt, i) => (
            <div key={i} className="flex items-start gap-2 bg-slate-800/40 rounded p-2">
              <span className="text-amber-600 flex-shrink-0 text-[10px] mt-0.5">💬</span>
              <span className="text-slate-300 text-[10px] leading-relaxed">{prompt}</span>
              <CopyButton text={prompt} />
            </div>
          ))}
        </div>
      </Section>

    </div>
  )
}
