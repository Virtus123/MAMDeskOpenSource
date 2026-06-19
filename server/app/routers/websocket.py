import json

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from app.config import settings
from app.services.redis_service import set_device_online
from app.services.signaling import signaling_manager

router = APIRouter(tags=["websocket"])


def _normalize_uid(device_uid: str) -> str:
    return device_uid.replace(" ", "").replace("-", "").strip().upper()


def _msg_type(message: dict) -> str:
    t = message.get("type") or message.get("Type") or ""
    t = str(t).lower().replace("connectionresponse", "connection_response")
    return t


def _get(message: dict, *keys):
    for k in keys:
        if k in message and message[k] is not None:
            return message[k]
    return None


@router.websocket("/ws/device/{device_uid}")
async def device_websocket(websocket: WebSocket, device_uid: str):
    """Quick Support / Host mantém conexão persistente para receber pedidos."""
    device_uid = _normalize_uid(device_uid)
    await websocket.accept()
    await signaling_manager.register_device(device_uid, websocket)

    try:
        while True:
            data = await websocket.receive_text()
            message = json.loads(data)
            msg_type = _msg_type(message)

            if msg_type == "ping":
                await websocket.send_json({"type": "pong"})
                await set_device_online(
                    device_uid, device_uid, ttl=settings.device_offline_timeout_seconds
                )
            elif msg_type == "connection_response":
                session_id = _get(message, "session_id", "SessionId")
                accepted = _get(message, "accepted", "Accepted") or False
                await signaling_manager.relay_to_session_peer(
                    session_id,
                    "host",
                    {"type": "connection_response", "accepted": accepted, "session_id": session_id},
                )
            elif msg_type in ("offer", "answer", "ice_candidate", "chat", "input"):
                session_id = _get(message, "session_id", "SessionId")
                if session_id:
                    await signaling_manager.relay_to_session_peer(session_id, "host", message)
    except WebSocketDisconnect:
        pass
    finally:
        await signaling_manager.unregister_device(device_uid, websocket)


@router.websocket("/ws/session/{session_id}/{role}")
async def session_websocket(websocket: WebSocket, session_id: str, role: str):
    """Operador (viewer) ou Host entra na sessão para signaling."""
    if role not in ("host", "viewer"):
        await websocket.close(code=4000)
        return

    await websocket.accept()
    await signaling_manager.join_session(session_id, role, websocket)

    try:
        while True:
            data = await websocket.receive_text()
            message = json.loads(data)
            msg_type = _msg_type(message)

            if msg_type == "ping":
                await websocket.send_json({"type": "pong"})
            elif msg_type in (
                "offer", "answer", "ice_candidate", "chat", "input", "frame",
                "connection_response", "session_closed", "draw", "command",
                "monitors", "session_end", "cursor",
            ):
                await signaling_manager.relay_to_session_peer(session_id, role, message)
    except WebSocketDisconnect:
        pass
    finally:
        await signaling_manager.leave_session(session_id, role, websocket)
