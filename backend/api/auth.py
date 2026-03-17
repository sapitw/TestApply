"""
API Key authentication for DIFY and external agent access.
"""
import secrets
import hashlib
from fastapi import HTTPException, Security, Header
from fastapi.security import APIKeyHeader
from typing import Optional
from loguru import logger

from backend.config import settings

api_key_header = APIKeyHeader(name="Authorization", auto_error=False)

# In-memory key store: { hashed_key: {"name": str, "created_at": str} }
_api_keys: dict[str, dict] = {}

# Bootstrap a default key from settings if provided
def _bootstrap():
    if settings.DIFY_API_KEY:
        hashed = _hash_key(settings.DIFY_API_KEY)
        _api_keys[hashed] = {"name": "default", "created_at": "startup"}

def _hash_key(key: str) -> str:
    return hashlib.sha256(key.encode()).hexdigest()

def create_api_key(name: str = "default") -> str:
    key = "fkg-" + secrets.token_urlsafe(32)
    _api_keys[_hash_key(key)] = {"name": name}
    logger.info(f"Created API key: name={name}")
    return key

def list_api_keys() -> list[dict]:
    return [{"name": v["name"], "hash_prefix": k[:8]} for k, v in _api_keys.items()]

def revoke_api_key(name: str) -> bool:
    target = None
    for k, v in _api_keys.items():
        if v["name"] == name:
            target = k
            break
    if target:
        del _api_keys[target]
        return True
    return False

async def require_api_key(authorization: Optional[str] = Header(None)) -> str:
    """
    FastAPI dependency that validates Bearer token or raw API key.
    Usage: key = Depends(require_api_key)
    """
    # If no keys are registered yet, allow all (development mode)
    if not _api_keys:
        return "dev-mode"

    if not authorization:
        raise HTTPException(status_code=401, detail="Missing Authorization header")

    token = authorization.removeprefix("Bearer ").strip()
    hashed = _hash_key(token)

    if hashed not in _api_keys:
        raise HTTPException(status_code=403, detail="Invalid API key")

    return _api_keys[hashed]["name"]
