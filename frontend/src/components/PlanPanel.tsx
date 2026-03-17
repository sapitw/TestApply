/**
 * Plan generation panel - Training and Maintenance plan generation.
 */
import React, { useState } from 'react'
import ReactMarkdown from 'react-markdown'
import { BookOpen, Wrench, Download, Loader2 } from 'lucide-react'
import { graphApi } from '../utils/api'
import { useGraphStore } from '../stores/graphStore'

interface Props {
  graphId: string
}

export default function PlanPanel({ graphId }: Props) {
  const [activeTab, setActiveTab] = useState<'training' | 'maintenance'>('training')
  const [audience, setAudience] = useState('general')
  const [scope, setScope] = useState('all')
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const generateTraining = async () => {
    setLoading(true)
    setError('')
    try {
      const result = await graphApi.trainingPlan(graphId, audience, 'markdown')
      setContent(result.content)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to generate plan')
    } finally {
      setLoading(false)
    }
  }

  const generateMaintenance = async () => {
    setLoading(true)
    setError('')
    try {
      const result = await graphApi.maintenancePlan(graphId, scope)
      setContent(result.content)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to generate plan')
    } finally {
      setLoading(false)
    }
  }

  const downloadMarkdown = () => {
    const blob = new Blob([content], { type: 'text/markdown' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${activeTab}-plan.md`
    a.click()
  }

  return (
    <div className="flex flex-col h-full">
      {/* Tab */}
      <div className="flex border-b border-slate-700">
        <button
          className={`flex items-center gap-1.5 px-4 py-2.5 text-xs font-medium transition-colors
            ${activeTab === 'training'
              ? 'border-b-2 border-blue-500 text-blue-400'
              : 'text-slate-400 hover:text-slate-200'}`}
          onClick={() => { setActiveTab('training'); setContent('') }}
        >
          <BookOpen size={12} />
          Training Plan
        </button>
        <button
          className={`flex items-center gap-1.5 px-4 py-2.5 text-xs font-medium transition-colors
            ${activeTab === 'maintenance'
              ? 'border-b-2 border-blue-500 text-blue-400'
              : 'text-slate-400 hover:text-slate-200'}`}
          onClick={() => { setActiveTab('maintenance'); setContent('') }}
        >
          <Wrench size={12} />
          Maintenance Plan
        </button>
      </div>

      {/* Controls */}
      <div className="p-3 border-b border-slate-700 space-y-2">
        {activeTab === 'training' ? (
          <div className="flex items-center gap-2">
            <label className="text-[11px] text-slate-400 flex-shrink-0">Audience:</label>
            <select
              value={audience}
              onChange={e => setAudience(e.target.value)}
              className="flex-1 bg-slate-800 border border-slate-600 rounded px-2 py-1 text-xs text-slate-200 focus:outline-none focus:border-blue-500"
            >
              <option value="general">General</option>
              <option value="new engineers">New Engineers</option>
              <option value="operations team">Operations Team</option>
              <option value="management">Management</option>
              <option value="developers">Developers</option>
            </select>
            <button
              onClick={generateTraining}
              disabled={loading}
              className="flex items-center gap-1.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-3 py-1.5 rounded text-xs font-medium transition-colors"
            >
              {loading ? <Loader2 size={12} className="animate-spin" /> : <BookOpen size={12} />}
              Generate
            </button>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <label className="text-[11px] text-slate-400 flex-shrink-0">Scope:</label>
            <input
              value={scope}
              onChange={e => setScope(e.target.value)}
              placeholder="e.g. database, deployment, all"
              className="flex-1 bg-slate-800 border border-slate-600 rounded px-2 py-1 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-blue-500"
            />
            <button
              onClick={generateMaintenance}
              disabled={loading}
              className="flex items-center gap-1.5 bg-amber-600 hover:bg-amber-700 disabled:opacity-50 text-white px-3 py-1.5 rounded text-xs font-medium transition-colors"
            >
              {loading ? <Loader2 size={12} className="animate-spin" /> : <Wrench size={12} />}
              Generate
            </button>
          </div>
        )}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto">
        {error && (
          <div className="m-3 p-3 bg-red-900/30 border border-red-700 rounded text-red-300 text-xs">
            {error}
          </div>
        )}

        {content ? (
          <div>
            <div className="flex justify-end px-3 pt-2">
              <button
                onClick={downloadMarkdown}
                className="flex items-center gap-1 text-xs text-slate-400 hover:text-slate-200 transition-colors"
              >
                <Download size={11} />
                Download .md
              </button>
            </div>
            <div className="px-4 pb-4 markdown-content text-sm">
              <ReactMarkdown>{content}</ReactMarkdown>
            </div>
          </div>
        ) : (
          !loading && (
            <div className="flex flex-col items-center justify-center h-32 text-slate-500 text-xs gap-2">
              <span className="text-2xl">
                {activeTab === 'training' ? '📚' : '🔧'}
              </span>
              Configure options above and click Generate
            </div>
          )
        )}

        {loading && (
          <div className="flex items-center justify-center h-32 gap-2 text-slate-400 text-xs">
            <Loader2 size={16} className="animate-spin" />
            Generating plan…
          </div>
        )}
      </div>
    </div>
  )
}
