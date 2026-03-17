"""
Central configuration for Feishu Knowledge Graph system.
"""
from pydantic_settings import BaseSettings
from typing import Optional


class Settings(BaseSettings):
    # Feishu / Lark API credentials
    FEISHU_APP_ID: str = ""
    FEISHU_APP_SECRET: str = ""
    FEISHU_BASE_URL: str = "https://open.feishu.cn/open-apis"

    # Server settings
    HOST: str = "0.0.0.0"
    PORT: int = 8000
    DEBUG: bool = False

    # MCP server settings
    MCP_SERVER_NAME: str = "feishu-knowledge-graph"
    MCP_SERVER_VERSION: str = "1.0.0"

    # Redis (optional, for caching)
    REDIS_URL: Optional[str] = None

    # CORS origins for frontend
    CORS_ORIGINS: list[str] = ["http://localhost:3000", "http://localhost:5173"]

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
