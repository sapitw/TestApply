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

app = FastAPI(
    title="Feishu Knowledge Graph",
    description=(
        "A system that reads all types of Feishu documents and builds "
        "an interactive knowledge graph. Exposes MCP tools for AI agent integration."
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
    logger.info(f"Feishu Knowledge Graph API started on {settings.HOST}:{settings.PORT}")
    logger.info(f"Feishu App ID: {settings.FEISHU_APP_ID or '(not configured)'}")
    logger.info(f"API docs: http://{settings.HOST}:{settings.PORT}/docs")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "backend.main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG,
    )
