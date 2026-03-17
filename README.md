# 飞书知识图谱 (Feishu Knowledge Graph)

一套完整的飞书文档知识图谱系统，支持读取所有类型的飞书文档，自动构建可视化树状知识图谱，并通过 MCP 协议为 AI 智能体提供知识库接口。

## 功能特性

### 文档支持（全类型）
| 类型 | 说明 |
|------|------|
| 📝 飞书文档 (Docx) | 标题层级、代码块、超链接 |
| 📚 Wiki 知识库 | 完整树状结构递归读取 |
| 📊 电子表格 (Sheets) | 工作表元数据 |
| 🗃️ 多维表格 (Bitable) | 表格与字段结构 |
| 📁 云文档文件夹 | 递归文件列表 |

### 知识图谱
- 自动解析文档结构，生成节点与边
- 支持层级展开、平铺视图
- 节点颜色编码（按类型）
- 交互式缩放、平移、小地图
- 选中节点查看详情

### MCP 工具（AI 智能体集成）
| 工具 | 描述 |
|------|------|
| `index_feishu_document` | 通过 URL 索引飞书文档，建立图谱 |
| `get_graph_overview` | 获取图谱高层概览 |
| `get_node_subtree` | 获取节点子树 |
| `search_graph` | 关键字搜索节点 |
| `get_code_dependencies` | 分析代码上下游依赖 |
| `generate_training_plan` | 生成培训计划（Markdown） |
| `generate_maintenance_plan` | 生成运维计划（Markdown） |
| `export_graph` | 导出 JSON / Markdown / Mermaid |
| `list_graphs` | 列出所有已索引图谱 |

## 快速启动

### 1. 环境配置

```bash
cp .env.example .env
# 编辑 .env，填写飞书应用 App ID 和 App Secret
```

### 2. 后端启动

```bash
cd backend
pip install -r requirements.txt
uvicorn backend.main:app --reload --port 8000
```

### 3. 前端启动

```bash
cd frontend
npm install
npm run dev
# 访问 http://localhost:5173
```

### 4. Docker 一键启动

```bash
docker-compose up --build
```

## 飞书应用配置

1. 前往 [飞书开放平台](https://open.feishu.cn/app) 创建企业自建应用
2. 开启以下权限：
   - `docs:doc:readonly` 文档读取
   - `drive:drive:readonly` 云空间读取
   - `wiki:wiki:readonly` 知识库读取
   - `bitable:app:readonly` 多维表格读取
   - `sheets:spreadsheet:readonly` 电子表格读取
3. 将 App ID / App Secret 填入 `.env`

## MCP 集成（Claude Desktop）

将以下配置添加至 `~/.claude/claude_desktop_config.json`：

```json
{
  "mcpServers": {
    "feishu-knowledge-graph": {
      "command": "python",
      "args": ["-m", "backend.mcp.server"],
      "cwd": "/path/to/TestApply",
      "env": {
        "FEISHU_APP_ID": "your_app_id",
        "FEISHU_APP_SECRET": "your_app_secret"
      }
    }
  }
}
```

重启 Claude Desktop 后即可使用：
- "帮我索引这个飞书文档：https://xxx.feishu.cn/docx/TOKEN"
- "根据这个知识图谱为新工程师生成培训计划"
- "分析这段代码的上下游依赖关系"

## HTTP API（供其他系统集成）

```
POST /api/documents/index        # 索引文档
GET  /api/graphs                 # 列出图谱
GET  /api/graphs/{id}            # 获取完整图谱
GET  /api/graphs/{id}/overview   # 获取概览
POST /api/graphs/search          # 搜索
POST /api/plans/training         # 生成培训计划
POST /api/plans/maintenance      # 生成运维计划
POST /api/graphs/export          # 导出图谱
POST /api/mcp/call               # HTTP 方式调用 MCP 工具
```

API 文档：`http://localhost:8000/docs`

## 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                     React Frontend (Vite)                       │
│  ┌───────────────┐  ┌─────────────────────┐  ┌──────────────┐  │
│  │  左侧栏        │  │  知识图谱画布        │  │  节点详情    │  │
│  │  - 结构树      │  │  (React Flow)       │  │  - 内容预览  │  │
│  │  - 搜索        │  │  - 节点 / 边        │  │  - 元数据    │  │
│  │  - 生成计划    │  │  - 缩放 / 平移      │  │  - 链接      │  │
│  │  - MCP 工具    │  │  - 小地图           │  │              │  │
│  └───────────────┘  └─────────────────────┘  └──────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │ HTTP /api
┌─────────────────────────────────────────────────────────────────┐
│                    FastAPI Backend                               │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐    │
│  │ Feishu Client│  │ Graph Builder│  │    MCP Server      │    │
│  │ - Auth Token │  │ - Node/Edge  │  │ - 9 Tools          │    │
│  │ - Docx/Wiki/ │  │ - Hierarchy  │  │ - stdio transport  │    │
│  │   Sheet/BT/  │  │ - Layout     │  │ - HTTP transport   │    │
│  │   Folder     │  │ - Merge      │  │                    │    │
│  └──────────────┘  └──────────────┘  └────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────┐
│              飞书 Open API (HTTPS)                               │
│  /docx  /wiki  /sheets  /bitable  /drive                        │
└─────────────────────────────────────────────────────────────────┘
```
