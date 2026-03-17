/**
 * MCP integration panel - shows available MCP tools and allows manual invocation.
 */
import React, { useState } from 'react'
import ReactMarkdown from 'react-markdown'
import { Terminal, Play, Copy, Check } from 'lucide-react'
import axios from 'axios'

const MCP_TOOLS = [
  {
    name: 'index_feishu_document',
    desc: 'Index a Feishu URL and build knowledge graph',
    example: { url: 'https://xxx.feishu.cn/docx/TOKEN' },
  },
  {
    name: 'get_graph_overview',
    desc: 'Get high-level overview of a knowledge graph',
    example: { graph_id: '<graph_id>' },
  },
  {
    name: 'search_graph',
    desc: 'Search nodes by keyword',
    example: { graph_id: '<graph_id>', query: 'deployment' },
  },
  {
    name: 'get_code_dependencies',
    desc: 'Get upstream/downstream code dependencies',
    example: { graph_id: '<graph_id>', direction: 'both' },
  },
  {
    name: 'generate_training_plan',
    desc: 'Generate training plan from knowledge graph',
    example: { graph_id: '<graph_id>', audience: 'new engineers', format: 'markdown' },
  },
  {
    name: 'generate_maintenance_plan',
    desc: 'Generate maintenance/ops plan',
    example: { graph_id: '<graph_id>', scope: 'all' },
  },
  {
    name: 'export_graph',
    desc: 'Export graph as JSON/Markdown/Mermaid',
    example: { graph_id: '<graph_id>', format: 'mermaid' },
  },
  {
    name: 'list_graphs',
    desc: 'List all indexed graphs',
    example: {},
  },
]

export default function MCPPanel() {
  const [selectedTool, setSelectedTool] = useState(MCP_TOOLS[0])
  const [args, setArgs] = useState(JSON.stringify(MCP_TOOLS[0].example, null, 2))
  const [result, setResult] = useState('')
  const [loading, setLoading] = useState(false)
  const [copied, setCopied] = useState(false)

  const invoke = async () => {
    setLoading(true)
    try {
      const parsed = JSON.parse(args)
      const resp = await axios.post('/api/mcp/call', { tool: selectedTool.name, arguments: parsed })
      const r = resp.data.result
      setResult(typeof r === 'string' ? r : JSON.stringify(r, null, 2))
    } catch (e: unknown) {
      setResult(`Error: ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setLoading(false)
    }
  }

  const copyConfig = () => {
    const config = {
      mcpServers: {
        'feishu-knowledge-graph': {
          command: 'python',
          args: ['-m', 'backend.mcp.server'],
          env: {
            FEISHU_APP_ID: 'YOUR_APP_ID',
            FEISHU_APP_SECRET: 'YOUR_APP_SECRET',
          },
        },
      },
    }
    navigator.clipboard.writeText(JSON.stringify(config, null, 2))
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div className="flex flex-col h-full text-xs">
      {/* Header */}
      <div className="p-3 border-b border-slate-700">
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-1.5 text-slate-300 font-medium">
            <Terminal size={12} />
            MCP Tools
          </div>
          <button
            onClick={copyConfig}
            className="flex items-center gap-1 text-slate-400 hover:text-slate-200 transition-colors"
          >
            {copied ? <Check size={11} className="text-green-400" /> : <Copy size={11} />}
            {copied ? 'Copied!' : 'Copy Config'}
          </button>
        </div>
        <p className="text-slate-500 text-[10px]">
          Add to <code className="text-slate-400">~/.claude/claude_desktop_config.json</code> for Claude Desktop integration.
        </p>
      </div>

      {/* Tool selector */}
      <div className="p-3 border-b border-slate-700">
        <label className="text-[10px] uppercase font-semibold text-slate-500 mb-1.5 block">Tool</label>
        <select
          value={selectedTool.name}
          onChange={e => {
            const t = MCP_TOOLS.find(t => t.name === e.target.value)!
            setSelectedTool(t)
            setArgs(JSON.stringify(t.example, null, 2))
            setResult('')
          }}
          className="w-full bg-slate-800 border border-slate-600 rounded px-2 py-1.5 text-slate-200 focus:outline-none focus:border-blue-500"
        >
          {MCP_TOOLS.map(t => (
            <option key={t.name} value={t.name}>{t.name}</option>
          ))}
        </select>
        <p className="text-slate-500 text-[10px] mt-1">{selectedTool.desc}</p>
      </div>

      {/* Arguments */}
      <div className="p-3 border-b border-slate-700">
        <label className="text-[10px] uppercase font-semibold text-slate-500 mb-1.5 block">Arguments (JSON)</label>
        <textarea
          value={args}
          onChange={e => setArgs(e.target.value)}
          rows={5}
          className="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1.5 font-mono text-slate-200 focus:outline-none focus:border-blue-500 resize-none"
        />
        <button
          onClick={invoke}
          disabled={loading}
          className="mt-2 w-full flex items-center justify-center gap-1.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-3 py-1.5 rounded font-medium transition-colors"
        >
          <Play size={11} />
          {loading ? 'Running…' : 'Invoke Tool'}
        </button>
      </div>

      {/* Result */}
      <div className="flex-1 overflow-y-auto p-3">
        {result && (
          <>
            <div className="text-[10px] uppercase font-semibold text-slate-500 mb-1.5">Result</div>
            <pre className="bg-slate-900 border border-slate-700 rounded p-2 text-slate-300 overflow-x-auto text-[10px] leading-relaxed whitespace-pre-wrap break-words">
              {result}
            </pre>
          </>
        )}
      </div>
    </div>
  )
}
