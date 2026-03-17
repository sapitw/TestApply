"""
Feishu Knowledge Graph - Main FastAPI Application
"""
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse
import os
from loguru import logger

from backend.config import settings
from backend.api.routes import router
from backend.api.dify import router as dify_router
from backend.api.auth import _bootstrap as auth_bootstrap

app = FastAPI(
    title="飞书知识图谱 API",
    description=(
        "读取所有类型飞书文档，建立交互式知识图谱。\n\n"
        "- `/api/*` — 内部 REST API（供前端与 MCP 使用）\n"
        "- `/dify/*` — **DIFY LLM Tool 接口**（OpenAPI 3.0，可直接导入 DIFY）\n"
        "- `/dify/openapi.json` — DIFY Custom Tool Schema\n"
        "- `/docs` — 完整 API 文档"
    ),
    version="1.0.0",
)

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS + ["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# API routes
app.include_router(router)
app.include_router(dify_router)

# Serve frontend static files if built
STATIC_DIR = os.path.join(os.path.dirname(__file__), "..", "frontend", "dist")
if os.path.exists(STATIC_DIR):
    app.mount("/assets", StaticFiles(directory=os.path.join(STATIC_DIR, "assets")), name="assets")

    @app.get("/", include_in_schema=False)
    @app.get("/{path:path}", include_in_schema=False)
    async def serve_frontend(path: str = ""):
        index = os.path.join(STATIC_DIR, "index.html")
        if os.path.exists(index):
            return FileResponse(index)
        return {"message": "Frontend not built. Run: cd frontend && npm run build"}


@app.on_event("startup")
async def startup():
    auth_bootstrap()
    logger.info(f"Feishu Knowledge Graph API started on {settings.HOST}:{settings.PORT}")
    logger.info(f"Feishu App ID: {settings.FEISHU_APP_ID or '(not configured)'}")
    logger.info(f"API docs: http://{settings.HOST}:{settings.PORT}/docs")
    logger.info(f"DIFY schema: {settings.PUBLIC_BASE_URL}/dify/openapi.json")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "backend.main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG,
    )
