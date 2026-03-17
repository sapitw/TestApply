/**
 * Main knowledge graph visualization canvas using React Flow.
 * Renders nodes and edges, supports interactive pan/zoom and node selection.
 */
import React, { useCallback, useMemo, useEffect } from 'react'
import ReactFlow, {
  Background,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  BackgroundVariant,
  type Node,
  type Edge,
  type NodeMouseHandler,
} from 'reactflow'
import 'reactflow/dist/style.css'

import type { KnowledgeGraph, GraphNode } from '../utils/api'
import { useGraphStore } from '../stores/graphStore'
import GraphNodeComponent from './GraphNodeComponent'

const nodeTypes = { graphNode: GraphNodeComponent }

interface Props {
  graph: KnowledgeGraph
}

// Color coded by type
const NODE_TYPE_COLORS: Record<string, string> = {
  wiki_space: '#4F46E5',
  wiki_page: '#7C3AED',
  document: '#2563EB',
  spreadsheet: '#059669',
  sheet_tab: '#10B981',
  bitable: '#D97706',
  bitable_table: '#F59E0B',
  folder: '#6B7280',
  file: '#9CA3AF',
  heading: '#1D4ED8',
  section: '#3B82F6',
  code_block: '#7C3AED',
  table: '#0891B2',
  todo: '#DC2626',
  link: '#0284C7',
}

function layoutNodes(nodes: GraphNode[], edges: { source: string; target: string }[]): Map<string, { x: number; y: number }> {
  // Simple hierarchical layout using BFS
  const positions = new Map<string, { x: number; y: number }>()
  const childrenMap = new Map<string, string[]>()
  const parentMap = new Map<string, string>()

  for (const e of edges) {
    if (!childrenMap.has(e.source)) childrenMap.set(e.source, [])
    childrenMap.get(e.source)!.push(e.target)
    parentMap.set(e.target, e.source)
  }

  // Find root (node with no parent)
  const allTargets = new Set(edges.map(e => e.target))
  const roots = nodes.filter(n => !allTargets.has(n.id))

  const H_GAP = 280
  const V_GAP = 120

  function assignPositions(nodeId: string, depth: number, siblingIndex: number, siblingCount: number) {
    const children = childrenMap.get(nodeId) || []
    const totalWidth = Math.max(siblingCount * H_GAP, children.length * H_GAP)
    const startX = siblingIndex * H_GAP - (siblingCount - 1) * H_GAP / 2

    positions.set(nodeId, { x: startX, y: depth * V_GAP })

    children.forEach((childId, i) => {
      assignPositions(childId, depth + 1, i, children.length)
    })
  }

  roots.forEach((root, i) => {
    assignPositions(root.id, 0, i, roots.length)
  })

  // Fill in any nodes not reached
  nodes.forEach(n => {
    if (!positions.has(n.id)) {
      positions.set(n.id, { x: Math.random() * 800, y: Math.random() * 600 })
    }
  })

  return positions
}

export default function GraphCanvas({ graph }: Props) {
  const { setSelectedNode, selectedNode } = useGraphStore()

  const positions = useMemo(() => layoutNodes(graph.nodes, graph.edges), [graph])

  const rfNodes: Node[] = useMemo(() => graph.nodes.map(gn => ({
    id: gn.id,
    type: 'graphNode',
    position: positions.get(gn.id) || { x: 0, y: 0 },
    data: {
      node: gn,
      isSelected: selectedNode?.id === gn.id,
      color: NODE_TYPE_COLORS[gn.type] || '#6B7280',
    },
    style: { width: 'auto' },
  })), [graph.nodes, positions, selectedNode])

  const rfEdges: Edge[] = useMemo(() => graph.edges.map(ge => ({
    id: ge.id,
    source: ge.source,
    target: ge.target,
    type: 'smoothstep',
    animated: ge.type === 'links_to' || ge.type === 'references',
    style: {
      stroke: ge.type === 'contains' ? '#3d5066'
            : ge.type === 'links_to' ? '#0891B2'
            : ge.type === 'depends_on' ? '#DC2626'
            : '#4d6078',
      strokeWidth: ge.type === 'contains' ? 1.5 : 1,
      opacity: 0.7,
    },
    markerEnd: {
      type: 'arrowclosed' as const,
      color: ge.type === 'contains' ? '#3d5066' : '#4d6078',
      width: 10,
      height: 10,
    },
    label: ge.type !== 'contains' ? ge.type : undefined,
    labelStyle: { fill: '#64748b', fontSize: 10 },
  })), [graph.edges])

  const [nodes, setNodes, onNodesChange] = useNodesState(rfNodes)
  const [edges, setEdges, onEdgesChange] = useEdgesState(rfEdges)

  useEffect(() => {
    setNodes(rfNodes)
  }, [rfNodes])

  useEffect(() => {
    setEdges(rfEdges)
  }, [rfEdges])

  const onNodeClick: NodeMouseHandler = useCallback((_evt, node) => {
    const gn = graph.nodes.find(n => n.id === node.id)
    if (gn) setSelectedNode(gn)
  }, [graph.nodes, setSelectedNode])

  return (
    <div className="w-full h-full">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={onNodeClick}
        nodeTypes={nodeTypes}
        fitView
        fitViewOptions={{ padding: 0.2, maxZoom: 1.2 }}
        minZoom={0.05}
        maxZoom={3}
        attributionPosition="bottom-right"
      >
        <Background variant={BackgroundVariant.Dots} gap={24} size={1} color="#1e293b" />
        <Controls />
        <MiniMap
          nodeColor={(n) => {
            const gn = graph.nodes.find(x => x.id === n.id)
            return gn ? (NODE_TYPE_COLORS[gn.type] || '#6B7280') : '#6B7280'
          }}
          maskColor="rgba(15, 23, 42, 0.7)"
        />
      </ReactFlow>
    </div>
  )
}
