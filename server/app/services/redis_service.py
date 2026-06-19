import json
from typing import Any

import redis.asyncio as aioredis

from app.config import settings

_redis: aioredis.Redis | None = None


async def get_redis() -> aioredis.Redis:
    global _redis
    if _redis is None:
        _redis = aioredis.from_url(settings.redis_url, decode_responses=True)
    return _redis


async def close_redis() -> None:
    global _redis
    if _redis is not None:
        await _redis.close()
        _redis = None


def device_online_key(device_uid: str) -> str:
    return f"mamdesk:device:online:{device_uid}"


def device_ws_key(device_uid: str) -> str:
    return f"mamdesk:device:ws:{device_uid}"


def session_key(session_id: str) -> str:
    return f"mamdesk:session:{session_id}"


async def set_device_online(device_uid: str, connection_id: str, ttl: int = 90) -> None:
    redis = await get_redis()
    await redis.setex(device_online_key(device_uid), ttl, connection_id)


async def is_device_online(device_uid: str) -> bool:
    redis = await get_redis()
    return await redis.exists(device_online_key(device_uid)) == 1


async def set_device_offline(device_uid: str) -> None:
    redis = await get_redis()
    await redis.delete(device_online_key(device_uid))
    await redis.delete(device_ws_key(device_uid))


async def save_session(session_id: str, data: dict[str, Any], ttl: int = 3600) -> None:
    redis = await get_redis()
    await redis.setex(session_key(session_id), ttl, json.dumps(data))


async def get_session(session_id: str) -> dict[str, Any] | None:
    redis = await get_redis()
    raw = await redis.get(session_key(session_id))
    if not raw:
        return None
    return json.loads(raw)
