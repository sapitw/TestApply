import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 60000,
})

export interface GraphNode {
  id: string
  label: string
  type: string
  depth: number
  token?: string
  url?: string
  content_preview?: string
  metadata: Record<string, unknown>
  color?: string
  size: number
  icon?: string
  expandable: boolean
  children_count: number
}

export interface GraphEdge {
  id: string
  source: string
  target: string
  type: string
  label?: string
  weight: number
}

export interface KnowledgeGraph {
  id: string
  title: string
  root_id: string
  nodes: GraphNode[]
  edges: GraphEdge[]
  metadata: Record<string, unknown>
}

export interface GraphOverview {
  graph_id: string
  title: string
  root: GraphNode
  total_nodes: number
  total_edges: number
  top_level_children: Array<{
    id: string
    label: string
    type: string
    children_count: number
  }>
}

export const graphApi = {
  index: (url: string, recursive = false) =>
    api.post('/documents/index', { url, recursive }).then(r => r.data),

  list: () => api.get('/graphs').then(r => r.data),

  get: (id: string): Promise<KnowledgeGraph> =>
    api.get(`/graphs/${id}`).then(r => r.data),

  overview: (id: string): Promise<GraphOverview> =>
    api.get(`/graphs/${id}/overview`).then(r => r.data),

  subtree: (graphId: string, nodeId: string, maxDepth = 3) =>
    api.get(`/graphs/${graphId}/nodes/${nodeId}/subtree`, { params: { max_depth: maxDepth } }).then(r => r.data),

  search: (graphId: string, query: string, nodeTypes?: string[]) =>
    api.post('/graphs/search', { graph_id: graphId, query, node_types: nodeTypes }).then(r => r.data),

  codeDeps: (graphId: string, nodeId?: string, direction = 'both') =>
    api.get(`/graphs/${graphId}/code-dependencies`, { params: { node_id: nodeId, direction } }).then(r => r.data),

  trainingPlan: (graphId: string, audience = 'general', format = 'markdown') =>
    api.post('/plans/training', { graph_id: graphId, audience, format }).then(r => r.data),

  maintenancePlan: (graphId: string, scope = 'all') =>
    api.post('/plans/maintenance', { graph_id: graphId, scope }).then(r => r.data),

  export: (graphId: string, format: 'json' | 'markdown' | 'mermaid') =>
    api.post('/graphs/export', { graph_id: graphId, format }).then(r => r.data),

  delete: (id: string) => api.delete(`/graphs/${id}`).then(r => r.data),
}

export default graphApi
