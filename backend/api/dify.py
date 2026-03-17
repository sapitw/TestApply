"""
DIFY LLM Tool Integration Layer
================================
Exposes the Feishu Knowledge Graph as DIFY-compatible HTTP tools.

DIFY "Custom Tool" supports OpenAPI 3.0 schema import.
This module provides:
  1. LLM-friendly REST endpoints (text/markdown responses)
  2. /dify/openapi.json  – OpenAPI 3.0 spec DIFY imports
  3. /dify/manifest.yaml – DIFY plugin manifest
  4. API key auth via Authorization: Bearer <key>

How DIFY works:
  - You paste /dify/openapi.json URL into DIFY > Tools > Custom Tool
  - DIFY reads the schema and shows the tools to LLM
  - LLM decides which tool to call, DIFY calls HTTP, returns text to LLM
  - LLM synthesizes response for end user
"""
import json
import yaml
import uuid
from fastapi import APIRouter, Depends, HTTPException, Query
from fastapi.responses import JSONResponse, PlainTextResponse, Response
from pydantic import BaseModel, Field
from typing import Optional
from loguru import logger

from backend.config import settings
from backend.api.auth import require_api_key
from backend.mcp.server import (
    _graph_store,
    tool_search_graph,
    tool_get_graph_overview,
    tool_get_node_subtree,
    tool_generate_training_plan,
    tool_generate_maintenance_plan,
    tool_export_graph,
    tool_list_graphs,
    tool_index_feishu_document,
    tool_get_code_dependencies,
    tool_scan_feishu_root,
    store_graph,
)
from backend.feishu.scanner import get_tracker

router = APIRouter(prefix="/dify", tags=["DIFY Integration"])

# ─── Helper ───────────────────────────────────────────────────────────────────

def _text(content: str) -> PlainTextResponse:
    """Return plain text (LLMs consume this best)."""
    return PlainTextResponse(content, media_type="text/plain; charset=utf-8")


def _require_graph(graph_id: str):
    if graph_id not in _graph_store:
        raise HTTPException(
            status_code=404,
            detail=f"知识图谱 {graph_id!r} 不存在。请先呼叫 index_document 或 scan_root 索引文件。"
        )
    return _graph_store[graph_id]


# ─── 1. Index single document ─────────────────────────────────────────────────

class IndexRequest(BaseModel):
    url: str = Field(..., description="飞书文档完整 URL，支持文档/Wiki/表格/多维表格/文件夹")

@router.post(
    "/index",
    summary="索引单一飞书文档",
    description=(
        "透过飞书文档 URL 读取文档内容，建立知识图谱节点。"
        "支援所有飞书文档类型：文档 (Docx)、Wiki 页面、电子表格、多维表格、文件夹。"
        "成功后回传 graph_id，后续可用 graph_id 查询内容。"
    ),
    response_class=PlainTextResponse,
)
async def dify_index(req: IndexRequest, _key: str = Depends(require_api_key)):
    result_str = await tool_index_feishu_document(req.url)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=400, detail=result["error"])
    return _text(
        f"✅ 索引成功！\n\n"
        f"文件标题：{result['title']}\n"
        f"知识图谱 ID：{result['graph_id']}\n"
        f"节点数量：{result['node_count']} 个\n"
        f"边数量：{result['edge_count']} 个\n\n"
        f"请保存 graph_id = {result['graph_id']} 供后续查询使用。"
    )


# ─── 2. Deep scan entire workspace ───────────────────────────────────────────

class ScanRequest(BaseModel):
    url: str = Field(..., description="飞书根目录 URL（Wiki 知识库或云文档文件夹）")
    clear_cache: bool = Field(False, description="是否清除已扫描缓存，强制重新扫描全部文档")

@router.post(
    "/scan",
    summary="深度扫描整个飞书知识库",
    description=(
        "自动递归扫描整个飞书知识库或文件夹下的所有文档（所有层级）。"
        "已扫描过的页面会自动跳过，只处理新增或未索引的内容。"
        "适合用于初始化整个知识库，或在有新文件加入后增量更新。"
        "注意：大型知识库扫描需要较长时间，建议于非高峰期执行。"
    ),
    response_class=PlainTextResponse,
)
async def dify_scan(req: ScanRequest, _key: str = Depends(require_api_key)):
    result_str = await tool_scan_feishu_root(req.url, clear_visited=req.clear_cache)
    result = json.loads(result_str)
    if "error" in result:
        raise HTTPException(status_code=400, detail=result["error"])

    progress = "\n".join(result.get("progress_log", [])[-20:])
    return _text(
        f"✅ 深度扫描完成！\n\n"
        f"知识图谱 ID：{result['graph_id']}\n"
        f"图谱标题：{result['title']}\n"
        f"发现文件：{result['total_found']} 份\n"
        f"已建立图谱：{result['total_indexed']} 份\n"
        f"跳过（已知）：{result['total_skipped']} 份\n"
        f"总节点数：{result['node_count']}\n"
        f"总边数：{result['edge_count']}\n\n"
        f"最后 20 条扫描记录：\n{progress}\n\n"
        f"请保存 graph_id = {result['graph_id']} 供后续所有查询使用。"
    )


# ─── 3. Query / ask knowledge graph ──────────────────────────────────────────

@router.get(
    "/query",
    summary="查询知识图谱（关键字搜索）",
    description=(
        "在已建立的知识图谱中搜索与关键字相关的内容节点。"
        "可用于回答「文件中有没有提到 X」、「关于 Y 的内容在哪里」等问题。"
        "回传节点标题、类型、内容摘要和所在层级，供 LLM 整合回答。"
    ),
    response_class=PlainTextResponse,
)
async def dify_query(
    graph_id: str = Query(..., description="知识图谱 ID（由 index 或 scan 取得）"),
    q: str = Query(..., description="搜索关键字，例如：部署流程、数据库设计、API 接口"),
    _key: str = Depends(require_api_key),
):
    _require_graph(graph_id)
    result_str = await tool_search_graph(graph_id, q)
    result = json.loads(result_str)

    if result.get("total", 0) == 0:
        return _text(f"在知识图谱 {graph_id!r} 中没有找到与「{q}」相关的内容。")

    lines = [f"🔍 搜索「{q}」，共找到 {result['total']} 个相关节点：\n"]
    for i, node in enumerate(result["results"][:15], 1):
        lines.append(f"{i}. 【{node['type']}】 {node['label']}")
        if node.get("content_preview"):
            lines.append(f"   摘要：{node['content_preview'][:200]}")
        lines.append(f"   层级：第 {node['depth']} 层")
        if node.get("url"):
            lines.append(f"   连结：{node['url']}")
        lines.append("")

    if result["total"] > 15:
        lines.append(f"（还有 {result['total'] - 15} 个结果未显示）")

    return _text("\n".join(lines))


# ─── 4. Get document structure overview ──────────────────────────────────────

@router.get(
    "/overview",
    summary="取得知识图谱整体结构概览",
    description=(
        "回传知识图谱的顶层结构：标题、根节点、总节点/边数量、"
        "以及第一层子节点列表。适合用于了解知识库整体架构，"
        "或决定下一步要深入查询哪个章节。"
    ),
    response_class=PlainTextResponse,
)
async def dify_overview(
    graph_id: str = Query(..., description="知识图谱 ID"),
    _key: str = Depends(require_api_key),
):
    _require_graph(graph_id)
    result_str = await tool_get_graph_overview(graph_id)
    result = json.loads(result_str)

    root = result.get("root", {})
    children = result.get("top_level_children", [])

    lines = [
        f"📚 知识图谱：{result['title']}",
        f"图谱 ID：{graph_id}",
        f"总节点数：{result['total_nodes']}　总边数：{result['total_edges']}",
        f"根节点：{root.get('label', '—')} [{root.get('type', '—')}]",
        "",
        f"顶层结构（共 {len(children)} 个章节）：",
    ]
    for i, child in enumerate(children, 1):
        cnt = child.get("children_count", 0)
        lines.append(f"  {i}. 【{child['type']}】 {child['label']}"
                     + (f"（含 {cnt} 个子项）" if cnt else ""))

    return _text("\n".join(lines))


# ─── 5. Get node subtree ──────────────────────────────────────────────────────

@router.get(
    "/subtree",
    summary="展开指定节点的子树内容",
    description=(
        "取得知识图谱中特定节点及其所有子节点的完整内容树。"
        "用于深入阅读某个章节或文档的详细内容。"
        "node_id 可从 query 或 overview 的回传结果中取得。"
    ),
    response_class=PlainTextResponse,
)
async def dify_subtree(
    graph_id: str = Query(..., description="知识图谱 ID"),
    node_id: str = Query(..., description="节点 ID（从 query 或 overview 结果取得）"),
    depth: int = Query(3, description="展开深度，预设 3 层，最大建议 5"),
    _key: str = Depends(require_api_key),
):
    _require_graph(graph_id)
    result_str = await tool_get_node_subtree(graph_id, node_id, depth)
    result = json.loads(result_str)

    def render(node: dict, indent: int = 0) -> list[str]:
        if not node:
            return []
        prefix = "  " * indent
        icon_map = {
            "heading": "🔖", "code_block": "💻", "document": "📝",
            "wiki_page": "📄", "wiki_space": "📚", "spreadsheet": "📊",
            "bitable": "🗃️", "folder": "📁", "todo": "✅", "link": "🔗",
        }
        icon = icon_map.get(node.get("type", ""), "•")
        lines = [f"{prefix}{icon} {node.get('label', '—')} [{node.get('type', '—')}]"]
        if node.get("content_preview"):
            lines.append(f"{prefix}   {node['content_preview'][:150]}")
        for child in node.get("children", []):
            lines.extend(render(child, indent + 1))
        return lines

    return _text("\n".join(render(result)))


# ─── 6. List all graphs ───────────────────────────────────────────────────────

@router.get(
    "/graphs",
    summary="列出所有已建立的知识图谱",
    description=(
        "列出系统中所有已索引的飞书知识图谱，包含 graph_id、标题、节点数量。"
        "当用户问「目前有哪些知识库」或「我们有哪些文件已经建立图谱」时使用。"
    ),
    response_class=PlainTextResponse,
)
async def dify_list_graphs(_key: str = Depends(require_api_key)):
    result_str = await tool_list_graphs()
    result = json.loads(result_str)
    graphs = result.get("graphs", [])

    if not graphs:
        return _text("目前系统中没有任何知识图谱。请先使用 index 或 scan 建立知识图谱。")

    lines = [f"📋 系统共有 {len(graphs)} 个知识图谱：\n"]
    for i, g in enumerate(graphs, 1):
        lines.append(
            f"{i}. {g['title']}\n"
            f"   graph_id：{g['graph_id']}\n"
            f"   节点：{g['node_count']}　边：{g['edge_count']}\n"
        )
    return _text("\n".join(lines))


# ─── 7. Generate training plan ────────────────────────────────────────────────

@router.get(
    "/training-plan",
    summary="根据知识图谱生成培训计划",
    description=(
        "分析知识图谱中的文档结构，自动生成结构化培训计划（Markdown 格式）。"
        "适合用于为新进员工、特定角色或跨部门人员制定学习路径。"
        "可指定目标受众（如：新进工程师、运维团队、管理层）。"
    ),
    response_class=PlainTextResponse,
)
async def dify_training_plan(
    graph_id: str = Query(..., description="知识图谱 ID"),
    audience: str = Query("general", description="目标受众，例如：新进工程师、运维团队、管理层、开发人员"),
    _key: str = Depends(require_api_key),
):
    _require_graph(graph_id)
    content = await tool_generate_training_plan(graph_id, audience, "markdown")
    return _text(content)


# ─── 8. Generate maintenance plan ────────────────────────────────────────────

@router.get(
    "/maintenance-plan",
    summary="根据知识图谱生成运维/维护计划",
    description=(
        "从知识图谱中提取流程、待办事项和操作步骤，自动生成运维或维护计划。"
        "适合用于系统上线前检查、定期维护排程、事故处理 SOP 等场景。"
        "可指定范围（如：数据库、部署、监控、备份）。"
    ),
    response_class=PlainTextResponse,
)
async def dify_maintenance_plan(
    graph_id: str = Query(..., description="知识图谱 ID"),
    scope: str = Query("all", description="范围筛选，例如：database、deployment、monitoring、all"),
    _key: str = Depends(require_api_key),
):
    _require_graph(graph_id)
    content = await tool_generate_maintenance_plan(graph_id, scope)
    return _text(content)


# ─── 9. Code dependency analysis ─────────────────────────────────────────────

@router.get(
    "/code-deps",
    summary="分析代码的上下游依赖关系",
    description=(
        "分析知识图谱中代码块的上下游依赖关系。"
        "可回答「这段代码被哪些模块呼叫」、「这个函数依赖哪些上游」等问题。"
        "适用于 code review、影响分析、重构规划。"
    ),
    response_class=PlainTextResponse,
)
async def dify_code_deps(
    graph_id: str = Query(..., description="知识图谱 ID"),
    node_id: Optional[str] = Query(None, description="特定代码节点 ID（留空则分析全部代码块）"),
    direction: str = Query("both", description="分析方向：upstream（上游）、downstream（下游）、both（双向）"),
    _key: str = Depends(require_api_key),
):
    _require_graph(graph_id)
    result_str = await tool_get_code_dependencies(graph_id, node_id, direction)
    result = json.loads(result_str)

    nodes = result.get("code_nodes", [])
    if not nodes:
        return _text("该知识图谱中未发现任何代码块。")

    lines = [f"💻 代码依赖分析（{direction} 方向），共 {len(nodes)} 个代码块：\n"]
    for i, cn in enumerate(nodes[:10], 1):
        lines.append(f"{i}. 【{cn.get('language', '未知语言')}】 {cn.get('label', cn.get('id', '—'))}")
        if cn.get("preview"):
            lines.append(f"   代码预览：{cn['preview'][:120]}…")
        upstream = cn.get("upstream", [])
        downstream = cn.get("downstream", [])
        if upstream:
            labels = [u.get("label", u.get("id")) for u in upstream[:5]]
            lines.append(f"   ↑ 上游（{len(upstream)} 个）：{', '.join(labels)}"
                         + ("…" if len(upstream) > 5 else ""))
        if downstream:
            labels = [d.get("label", d.get("id")) for d in downstream[:5]]
            lines.append(f"   ↓ 下游（{len(downstream)} 个）：{', '.join(labels)}"
                         + ("…" if len(downstream) > 5 else ""))
        lines.append("")

    return _text("\n".join(lines))


# ─── 10. Export graph ─────────────────────────────────────────────────────────

@router.get(
    "/export",
    summary="导出知识图谱（Markdown 大纲 / Mermaid 图表）",
    description=(
        "将知识图谱导出为可阅读格式。"
        "markdown：层级大纲，适合直接阅读或存档。"
        "mermaid：Mermaid 流程图语法，可粘贴至支持 Mermaid 的编辑器渲染。"
        "json：完整图谱数据结构。"
    ),
    response_class=PlainTextResponse,
)
async def dify_export(
    graph_id: str = Query(..., description="知识图谱 ID"),
    format: str = Query("markdown", description="导出格式：markdown、mermaid、json"),
    _key: str = Depends(require_api_key),
):
    _require_graph(graph_id)
    content = await tool_export_graph(graph_id, format)
    return _text(content)


# ─── 11. Cached pages status ─────────────────────────────────────────────────

@router.get(
    "/cache-status",
    summary="查看已缓存（已扫描）的飞书页面",
    description=(
        "查看系统已扫描并缓存的飞书页面列表。"
        "可用于了解哪些文档已被索引、上次扫描了多少页面。"
        "再次扫描时已缓存页面会被跳过。"
    ),
    response_class=PlainTextResponse,
)
async def dify_cache_status(_key: str = Depends(require_api_key)):
    tracker = get_tracker()
    visited = tracker.all_visited()
    count = tracker.count

    if count == 0:
        return _text("目前没有任何已缓存的飞书页面。请先执行 scan 索引知识库。")

    lines = [f"🗂️ 已缓存飞书页面共 {count} 份：\n"]
    for token, info in list(visited.items())[:30]:
        lines.append(f"  • {info.get('title', '—')} [{info.get('type', '—')}]")
        lines.append(f"    Token: {token[:20]}…  扫描于：{info.get('scanned_at', '—')[:19]}")

    if count > 30:
        lines.append(f"\n（还有 {count - 30} 个页面未显示）")

    return _text("\n".join(lines))


# ─── 12. OpenAPI schema for DIFY import ──────────────────────────────────────

@router.get(
    "/openapi.json",
    summary="DIFY Custom Tool OpenAPI schema",
    include_in_schema=False,
)
async def dify_openapi_schema():
    """
    Returns an OpenAPI 3.0 schema specifically formatted for DIFY Custom Tool import.
    In DIFY: Settings > Tools > Custom Tool > paste this URL.
    """
    base = settings.PUBLIC_BASE_URL
    schema = {
        "openapi": "3.0.0",
        "info": {
            "title": "飞书知识图谱 MCP",
            "description": (
                "读取飞书知识库并构建知识图谱的工具集。"
                "支援查询知识内容、生成培训计划、生成运维计划、分析代码依赖等功能。"
                "使用前请先用 scan 或 index 索引飞书文档，取得 graph_id 后进行后续查询。"
            ),
            "version": "1.0.0",
        },
        "servers": [{"url": f"{base}/dify"}],
        "paths": {
            "/scan": {
                "post": {
                    "operationId": "scan_feishu_root",
                    "summary": "深度扫描整个飞书知识库",
                    "description": (
                        "自动递归扫描整个飞书知识库或文件夹所有层级的文档，建立知识图谱。"
                        "已扫描过的页面自动跳过（增量更新）。扫描完成回传 graph_id。"
                    ),
                    "requestBody": {
                        "required": True,
                        "content": {
                            "application/json": {
                                "schema": {
                                    "type": "object",
                                    "required": ["url"],
                                    "properties": {
                                        "url": {
                                            "type": "string",
                                            "description": "飞书根目录 URL（Wiki 知识库 或 文件夹）",
                                        },
                                        "clear_cache": {
                                            "type": "boolean",
                                            "description": "是否清除已扫描缓存，强制重新扫描",
                                            "default": False,
                                        },
                                    },
                                }
                            }
                        },
                    },
                    "responses": {"200": {"description": "扫描结果与 graph_id", "content": {"text/plain": {}}}},
                }
            },
            "/index": {
                "post": {
                    "operationId": "index_feishu_document",
                    "summary": "索引单一飞书文档",
                    "description": "读取单份飞书文档建立知识图谱，回传 graph_id。",
                    "requestBody": {
                        "required": True,
                        "content": {
                            "application/json": {
                                "schema": {
                                    "type": "object",
                                    "required": ["url"],
                                    "properties": {
                                        "url": {"type": "string", "description": "飞书文档 URL"},
                                    },
                                }
                            }
                        },
                    },
                    "responses": {"200": {"description": "索引结果与 graph_id", "content": {"text/plain": {}}}},
                }
            },
            "/query": {
                "get": {
                    "operationId": "query_knowledge_graph",
                    "summary": "查询知识图谱内容（关键字搜索）",
                    "description": (
                        "在知识图谱中搜索关键字，回传相关节点的标题、摘要、层级。"
                        "适合回答「有没有提到X」、「关于Y的内容在哪里」等问题。"
                    ),
                    "parameters": [
                        {"name": "graph_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "知识图谱 ID"},
                        {"name": "q", "in": "query", "required": True, "schema": {"type": "string"}, "description": "搜索关键字"},
                    ],
                    "responses": {"200": {"description": "搜索结果", "content": {"text/plain": {}}}},
                }
            },
            "/overview": {
                "get": {
                    "operationId": "get_graph_overview",
                    "summary": "取得知识图谱整体结构概览",
                    "description": "回传知识图谱顶层章节结构，用于了解知识库整体架构。",
                    "parameters": [
                        {"name": "graph_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "知识图谱 ID"},
                    ],
                    "responses": {"200": {"description": "结构概览", "content": {"text/plain": {}}}},
                }
            },
            "/subtree": {
                "get": {
                    "operationId": "get_node_subtree",
                    "summary": "展开指定节点的子树内容",
                    "description": "取得特定节点及其子节点的详细内容，用于深入阅读某章节。",
                    "parameters": [
                        {"name": "graph_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "知识图谱 ID"},
                        {"name": "node_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "节点 ID"},
                        {"name": "depth", "in": "query", "schema": {"type": "integer", "default": 3}, "description": "展开深度"},
                    ],
                    "responses": {"200": {"description": "节点子树内容", "content": {"text/plain": {}}}},
                }
            },
            "/graphs": {
                "get": {
                    "operationId": "list_all_graphs",
                    "summary": "列出所有已建立的知识图谱",
                    "description": "列出系统所有知识图谱的 graph_id 和标题，用于选择要查询的图谱。",
                    "parameters": [],
                    "responses": {"200": {"description": "图谱列表", "content": {"text/plain": {}}}},
                }
            },
            "/training-plan": {
                "get": {
                    "operationId": "generate_training_plan",
                    "summary": "生成培训计划",
                    "description": "根据知识图谱自动生成结构化培训计划，可指定目标受众。",
                    "parameters": [
                        {"name": "graph_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "知识图谱 ID"},
                        {"name": "audience", "in": "query", "schema": {"type": "string", "default": "general"}, "description": "目标受众"},
                    ],
                    "responses": {"200": {"description": "Markdown 培训计划", "content": {"text/plain": {}}}},
                }
            },
            "/maintenance-plan": {
                "get": {
                    "operationId": "generate_maintenance_plan",
                    "summary": "生成运维/维护计划",
                    "description": "从知识图谱提取操作流程和待办事项，生成运维计划。",
                    "parameters": [
                        {"name": "graph_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "知识图谱 ID"},
                        {"name": "scope", "in": "query", "schema": {"type": "string", "default": "all"}, "description": "范围筛选"},
                    ],
                    "responses": {"200": {"description": "Markdown 运维计划", "content": {"text/plain": {}}}},
                }
            },
            "/code-deps": {
                "get": {
                    "operationId": "analyze_code_dependencies",
                    "summary": "分析代码上下游依赖关系",
                    "description": "分析知识图谱中代码块的上下游依赖，适合 code review 和影响分析。",
                    "parameters": [
                        {"name": "graph_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "知识图谱 ID"},
                        {"name": "node_id", "in": "query", "schema": {"type": "string"}, "description": "代码节点 ID（留空分析全部）"},
                        {"name": "direction", "in": "query", "schema": {"type": "string", "default": "both"}, "description": "方向：upstream/downstream/both"},
                    ],
                    "responses": {"200": {"description": "依赖分析结果", "content": {"text/plain": {}}}},
                }
            },
            "/export": {
                "get": {
                    "operationId": "export_knowledge_graph",
                    "summary": "导出知识图谱",
                    "description": "将知识图谱导出为 Markdown 大纲、Mermaid 流程图或 JSON。",
                    "parameters": [
                        {"name": "graph_id", "in": "query", "required": True, "schema": {"type": "string"}, "description": "知识图谱 ID"},
                        {"name": "format", "in": "query", "schema": {"type": "string", "default": "markdown"}, "description": "格式：markdown/mermaid/json"},
                    ],
                    "responses": {"200": {"description": "导出内容", "content": {"text/plain": {}}}},
                }
            },
            "/cache-status": {
                "get": {
                    "operationId": "get_cache_status",
                    "summary": "查看已缓存的飞书页面",
                    "description": "列出已扫描并缓存的飞书页面，了解哪些文档已被索引。",
                    "parameters": [],
                    "responses": {"200": {"description": "缓存状态", "content": {"text/plain": {}}}},
                }
            },
        },
        "components": {
            "securitySchemes": {
                "BearerAuth": {
                    "type": "http",
                    "scheme": "bearer",
                    "description": "API Key，格式：Authorization: Bearer <your-api-key>",
                }
            }
        },
        "security": [{"BearerAuth": []}],
    }
    return JSONResponse(schema)


# ─── 13. DIFY plugin manifest ─────────────────────────────────────────────────

@router.get(
    "/manifest.yaml",
    summary="DIFY plugin manifest",
    include_in_schema=False,
    response_class=PlainTextResponse,
)
async def dify_manifest():
    """DIFY plugin manifest in YAML format."""
    base = settings.PUBLIC_BASE_URL
    manifest = {
        "version": "0.0.1",
        "type": "api",
        "author": "feishu-knowledge-graph",
        "name": "feishu_knowledge_graph",
        "title": "飞书知识图谱",
        "description": "读取飞书知识库，建立知识图谱，支援查询、培训计划、运维计划、代码依赖分析",
        "icon": "https://open.feishu.cn/favicon.ico",
        "category": "productivity",
        "created_at": "2024-01-01T00:00:00Z",
        "resource": {
            "memory": 268435456,
            "permission": {"tool": {"enabled": True}},
        },
        "plugins": {
            "tools": [f"{base}/dify/openapi.json"],
        },
        "meta": {
            "version": "0.0.1",
            "arch": ["amd64", "arm64"],
            "runner": {"language": "python", "version": "3.12", "entrypoint": "main"},
        },
    }
    return _text(yaml.dump(manifest, allow_unicode=True, default_flow_style=False))


# ─── 14. Admin: API key management ───────────────────────────────────────────

from backend.api.auth import create_api_key, list_api_keys, revoke_api_key

class CreateKeyRequest(BaseModel):
    name: str = "default"

@router.post("/admin/keys", summary="创建 API Key", include_in_schema=False)
async def admin_create_key(req: CreateKeyRequest):
    """Create a new API key (no auth required — protect this endpoint in production)."""
    key = create_api_key(req.name)
    return {
        "api_key": key,
        "name": req.name,
        "note": "请将此 key 保存至安全位置，系统不会再次显示完整 key。",
        "usage": f'Authorization: Bearer {key}',
    }

@router.get("/admin/keys", summary="列出 API Keys", include_in_schema=False)
async def admin_list_keys():
    return {"keys": list_api_keys()}

@router.delete("/admin/keys/{name}", summary="撤销 API Key", include_in_schema=False)
async def admin_revoke_key(name: str):
    ok = revoke_api_key(name)
    if not ok:
        raise HTTPException(status_code=404, detail=f"Key '{name}' not found")
    return {"message": f"Key '{name}' revoked."}
