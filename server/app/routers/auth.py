from datetime import timedelta

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.deps import get_current_user
from app.core.security import create_access_token, hash_password, verify_password
from app.database import get_db
from app.models.device import Device
from app.models.device_access import DeviceAccess
from app.models.user import User
from app.schemas.device import DeviceOut
from app.schemas.user import TokenResponse, UserCreate, UserLogin, UserOut
from app.services.redis_service import is_device_online

router = APIRouter(prefix="/auth", tags=["auth"])


@router.post("/register", response_model=UserOut, status_code=status.HTTP_201_CREATED)
async def register(payload: UserCreate, db: AsyncSession = Depends(get_db)):
    existing = await db.scalar(select(User).where(User.email == payload.email))
    if existing:
        raise HTTPException(status_code=409, detail="E-mail já cadastrado")

    user = User(
        nome=payload.nome,
        email=payload.email,
        senha_hash=hash_password(payload.senha),
        is_approved=False,
    )
    db.add(user)
    await db.commit()
    await db.refresh(user)
    return user


@router.post("/login", response_model=TokenResponse)
async def login(payload: UserLogin, db: AsyncSession = Depends(get_db)):
    user = await db.scalar(select(User).where(User.email == payload.email))
    if not user or not verify_password(payload.senha, user.senha_hash):
        raise HTTPException(status_code=401, detail="Credenciais inválidas")

    if not user.is_admin and not user.is_approved:
        raise HTTPException(
            status_code=403,
            detail="Conta aguardando aprovação do administrador. Entre em contato com o administrador do sistema.",
        )

    token = create_access_token({"sub": str(user.id)}, expires_delta=timedelta(minutes=1440))
    return TokenResponse(access_token=token, usuario=UserOut.model_validate(user))


@router.get("/me", response_model=UserOut)
async def me(current_user: User = Depends(get_current_user)):
    return current_user


@router.get("/devices", response_model=list[DeviceOut])
async def list_my_devices(
    current_user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Lista apenas dispositivos que este operador já conectou (histórico pessoal)."""
    result = await db.scalars(
        select(Device)
        .join(DeviceAccess, DeviceAccess.device_id == Device.id)
        .where(DeviceAccess.user_id == current_user.id)
        .order_by(DeviceAccess.last_access_at.desc())
    )
    devices = list(result.all())
    for device in devices:
        device.online = await is_device_online(device.device_uid)
    devices.sort(key=lambda d: (not d.online, d.nome_pc or ""))
    return devices
