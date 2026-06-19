import uuid
from datetime import UTC, datetime

from fastapi import APIRouter, Depends, HTTPException, Request, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.deps import get_current_user, get_optional_user
from app.core.security import hash_password, verify_password
from app.database import get_db
from app.models.device import Device
from app.models.user import User
from app.schemas.device import (
    ConnectRequest,
    ConnectResponse,
    DeviceOut,
    DeviceRegister,
    DeviceUpdate,
    OperatorConnectRequest,
    RecordAccessRequest,
)
from app.services.device_access import record_device_access
from app.services.redis_service import is_device_online
from app.services.signaling import signaling_manager

router = APIRouter(prefix="/devices", tags=["devices"])


def format_device_id(device_uid: str) -> str:
    clean = device_uid.replace("-", "").upper()[:9]
    return " ".join(clean[i : i + 3] for i in range(0, min(len(clean), 9), 3))


def normalize_device_uid(device_uid: str) -> str:
    return device_uid.replace(" ", "").replace("-", "").strip().upper()


async def find_device(db: AsyncSession, raw_uid: str) -> Device | None:
    """Busca por UID exato ou prefixo (ID curto de 9 dígitos exibido no Quick Support)."""
    uid = normalize_device_uid(raw_uid)
    device = await db.scalar(select(Device).where(Device.device_uid == uid))
    if device:
        return device

    if len(uid) < 32:
        result = await db.scalars(select(Device).where(Device.device_uid.startswith(uid)))
        matches = list(result.all())
        if len(matches) == 1:
            return matches[0]
        if len(matches) > 1:
            raise HTTPException(
                status_code=409,
                detail="ID ambíguo — digite o ID completo",
            )
    return None


@router.post("/register", response_model=DeviceOut, status_code=status.HTTP_201_CREATED)
async def register_device(payload: DeviceRegister, request: Request, db: AsyncSession = Depends(get_db)):
    uid = normalize_device_uid(payload.device_uid)
    existing = await db.scalar(select(Device).where(Device.device_uid == uid))
    if existing:
        existing.nome_pc = payload.nome_pc
        existing.senha_sessao_hash = hash_password(payload.senha_sessao)
        existing.tipo = payload.tipo
        existing.ultimo_ip = request.client.host if request.client else None
        await db.commit()
        await db.refresh(existing)
        return existing

    device = Device(
        device_uid=uid,
        nome_pc=payload.nome_pc,
        senha_sessao_hash=hash_password(payload.senha_sessao),
        tipo=payload.tipo,
        ultimo_ip=request.client.host if request.client else None,
    )
    db.add(device)
    await db.commit()
    await db.refresh(device)
    return device


@router.put("/{device_uid}", response_model=DeviceOut)
async def update_device(
    device_uid: str,
    payload: DeviceUpdate,
    db: AsyncSession = Depends(get_db),
):
    device = await db.scalar(select(Device).where(Device.device_uid == device_uid))
    if not device:
        raise HTTPException(status_code=404, detail="Dispositivo não encontrado")

    if payload.nome_pc:
        device.nome_pc = payload.nome_pc
    if payload.senha_sessao:
        device.senha_sessao_hash = hash_password(payload.senha_sessao)

    await db.commit()
    await db.refresh(device)
    return device


@router.get("/{device_uid}", response_model=DeviceOut)
async def get_device(device_uid: str, db: AsyncSession = Depends(get_db)):
    device = await find_device(db, device_uid)
    if not device:
        raise HTTPException(status_code=404, detail="Dispositivo não encontrado")
    device.online = await is_device_online(device.device_uid)
    return device


async def _start_connection(
    device: Device,
    db: AsyncSession,
    *,
    trusted: bool,
    session_id: str | None = None,
    operator_user_id: int | None = None,
) -> ConnectResponse:
    uid = device.device_uid
    sid = session_id or str(uuid.uuid4())

    if not await is_device_online(uid):
        raise HTTPException(status_code=409, detail="Dispositivo offline")

    sent = await signaling_manager.send_to_device(
        uid,
        {
            "type": "connection_request",
            "session_id": sid,
            "device_uid": uid,
            "trusted": trusted,
        },
    )
    if not sent:
        raise HTTPException(status_code=409, detail="Não foi possível contatar o dispositivo")

    device.ultima_conexao = datetime.now(UTC)
    if operator_user_id is not None:
        await record_device_access(db, operator_user_id, device.id)
    await db.commit()

    return ConnectResponse(
        session_id=sid,
        device_uid=device.device_uid,
        nome_pc=device.nome_pc,
        status="pending",
    )


@router.post("/connect", response_model=ConnectResponse)
async def request_connection(
    payload: ConnectRequest,
    db: AsyncSession = Depends(get_db),
    current_user: User | None = Depends(get_optional_user),
):
    device = await find_device(db, payload.device_uid)
    if not device:
        raise HTTPException(status_code=404, detail="Dispositivo não encontrado")

    if not verify_password(payload.senha_sessao, device.senha_sessao_hash):
        raise HTTPException(status_code=401, detail="Senha incorreta")

    return await _start_connection(
        device,
        db,
        trusted=False,
        session_id=payload.session_id,
        operator_user_id=current_user.id if current_user else None,
    )


@router.post("/connect-operator", response_model=ConnectResponse)
async def operator_connection(
    payload: OperatorConnectRequest,
    current_user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Operador autenticado conecta sem senha (dispositivo deve estar online)."""
    device = await find_device(db, payload.device_uid)
    if not device:
        raise HTTPException(status_code=404, detail="Dispositivo não encontrado")

    return await _start_connection(
        device,
        db,
        trusted=True,
        session_id=payload.session_id,
        operator_user_id=current_user.id,
    )


@router.post("/record-access")
async def record_access(
    payload: RecordAccessRequest,
    current_user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Registra dispositivo no histórico do operador após conexão bem-sucedida."""
    device = await find_device(db, payload.device_uid)
    if not device:
        raise HTTPException(status_code=404, detail="Dispositivo não encontrado")
    await record_device_access(db, current_user.id, device.id)
    await db.commit()
    return {"ok": True}
