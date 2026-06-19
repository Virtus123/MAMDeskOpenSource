import asyncio
import json
from typing import Any

from fastapi import WebSocket

from app.config import settings
from app.services.redis_service import save_session, set_device_offline, set_device_online


class SignalingManager:
    """Gerencia conexões WebSocket para signaling WebRTC entre clientes."""

    def __init__(self) -> None:
        self._device_connections: dict[str, WebSocket] = {}
        self._session_connections: dict[str, dict[str, WebSocket]] = {}
        self._pending_viewer_messages: dict[str, dict[str, Any]] = {}
        self._lock = asyncio.Lock()

    async def register_device(self, device_uid: str, websocket: WebSocket) -> None:
        old: WebSocket | None = None
        async with self._lock:
            old = self._device_connections.get(device_uid)
            self._device_connections[device_uid] = websocket

        if old is not None and old is not websocket:
            try:
                await old.close(code=4001, reason="replaced")
            except Exception:
                pass

        ttl = settings.device_offline_timeout_seconds
        await set_device_online(device_uid, device_uid, ttl=ttl)

    async def unregister_device(self, device_uid: str, websocket: WebSocket) -> None:
        removed = False
        async with self._lock:
            current = self._device_connections.get(device_uid)
            if current is websocket:
                self._device_connections.pop(device_uid, None)
                removed = True

        if removed:
            await set_device_offline(device_uid)

    async def send_to_device(self, device_uid: str, message: dict[str, Any]) -> bool:
        ws = self._device_connections.get(device_uid)
        if ws is None:
            return False
        try:
            await ws.send_json(message)
            return True
        except Exception:
            return False

    async def join_session(self, session_id: str, role: str, websocket: WebSocket) -> None:
        old: WebSocket | None = None
        pending: dict[str, Any] | None = None
        async with self._lock:
            if session_id not in self._session_connections:
                self._session_connections[session_id] = {}
            old = self._session_connections[session_id].get(role)
            self._session_connections[session_id][role] = websocket
            if role == "viewer":
                pending = self._pending_viewer_messages.pop(session_id, None)

        if old is not None and old is not websocket:
            try:
                await old.close(code=4001, reason="replaced")
            except Exception:
                pass

        if pending is not None:
            try:
                await websocket.send_json(pending)
            except Exception:
                async with self._lock:
                    self._pending_viewer_messages[session_id] = pending

    async def leave_session(self, session_id: str, role: str, websocket: WebSocket) -> None:
        target_ws = None
        removed = False
        async with self._lock:
            session = self._session_connections.get(session_id)
            if session and session.get(role) is websocket:
                target_role = "viewer" if role == "host" else "host"
                target_ws = session.get(target_role)
                session.pop(role, None)
                removed = True
                if not session:
                    self._session_connections.pop(session_id, None)

        if not removed or target_ws is None:
            return

        try:
            await target_ws.send_json(
                {"type": "session_closed", "session_id": session_id, "reason": f"{role}_disconnected"}
            )
        except Exception:
            pass

    async def relay_to_session_peer(
        self, session_id: str, sender_role: str, message: dict[str, Any]
    ) -> bool:
        target_role = "viewer" if sender_role == "host" else "host"
        async with self._lock:
            session = self._session_connections.get(session_id, {})
            target_ws = session.get(target_role)

        payload = {**message, "from": sender_role}
        msg_type = str(message.get("type") or "").lower()

        if target_ws is None:
            if target_role == "viewer" and msg_type == "connection_response":
                async with self._lock:
                    self._pending_viewer_messages[session_id] = payload
            return False

        try:
            await target_ws.send_json(payload)
            return True
        except Exception:
            if target_role == "viewer" and msg_type == "connection_response":
                async with self._lock:
                    self._pending_viewer_messages[session_id] = payload
            return False

    async def create_session(self, session_id: str, host_uid: str, viewer_uid: str | None = None) -> None:
        await save_session(
            session_id,
            {"host_uid": host_uid, "viewer_uid": viewer_uid, "status": "active"},
        )


signaling_manager = SignalingManager()
