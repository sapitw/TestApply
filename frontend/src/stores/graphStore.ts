import { create } from 'zustand'
import type { KnowledgeGraph, GraphNode } from '../utils/api'

interface GraphStore {
  graphs: Record<string, KnowledgeGraph>
  activeGraphId: string | null
  selectedNode: GraphNode | null
  searchQuery: string
  sidebarTab: 'tree' | 'search' | 'plan' | 'mcp'
  loading: boolean
  error: string | null

  setActiveGraph: (id: string | null) => void
  storeGraph: (graph: KnowledgeGraph) => void
  removeGraph: (id: string) => void
  setSelectedNode: (node: GraphNode | null) => void
  setSearchQuery: (q: string) => void
  setSidebarTab: (tab: 'tree' | 'search' | 'plan' | 'mcp') => void
  setLoading: (v: boolean) => void
  setError: (e: string | null) => void
}

export const useGraphStore = create<GraphStore>((set) => ({
  graphs: {},
  activeGraphId: null,
  selectedNode: null,
  searchQuery: '',
  sidebarTab: 'tree',
  loading: false,
  error: null,

  setActiveGraph: (id) => set({ activeGraphId: id, selectedNode: null }),
  storeGraph: (graph) => set((s) => ({ graphs: { ...s.graphs, [graph.id]: graph } })),
  removeGraph: (id) => set((s) => {
    const next = { ...s.graphs }
    delete next[id]
    return { graphs: next, activeGraphId: s.activeGraphId === id ? null : s.activeGraphId }
  }),
  setSelectedNode: (node) => set({ selectedNode: node }),
  setSearchQuery: (q) => set({ searchQuery: q }),
  setSidebarTab: (tab) => set({ sidebarTab: tab }),
  setLoading: (v) => set({ loading: v }),
  setError: (e) => set({ error: e }),
}))
