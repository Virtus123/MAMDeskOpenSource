from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.deps import get_admin_user
from app.core.security import hash_password
from app.database import get_db
from app.models.user import User
from app.schemas.user import AdminUserCreate, AdminUserOut

router = APIRouter(prefix="/admin", tags=["admin"])


@router.post("/users", response_model=AdminUserOut, status_code=status.HTTP_201_CREATED)
async def create_user(
    payload: AdminUserCreate,
    _: User = Depends(get_admin_user),
    db: AsyncSession = Depends(get_db),
):
    """Cadastra operador pelo painel admin (já aprovado por padrão)."""
    existing = await db.scalar(select(User).where(User.email == payload.email))
    if existing:
        raise HTTPException(status_code=409, detail="E-mail já cadastrado")

    user = User(
        nome=payload.nome,
        email=payload.email,
        senha_hash=hash_password(payload.senha),
        is_admin=False,
        is_approved=payload.is_approved,
    )
    db.add(user)
    await db.commit()
    await db.refresh(user)
    return user


@router.get("/users", response_model=list[AdminUserOut])
async def list_users(
    _: User = Depends(get_admin_user),
    db: AsyncSession = Depends(get_db),
):
    result = await db.scalars(select(User).order_by(User.criado_em.desc()))
    return list(result.all())


@router.post("/users/{user_id}/approve", response_model=AdminUserOut)
async def approve_user(
    user_id: int,
    admin: User = Depends(get_admin_user),
    db: AsyncSession = Depends(get_db),
):
    user = await db.scalar(select(User).where(User.id == user_id))
    if not user:
        raise HTTPException(status_code=404, detail="Usuário não encontrado")
    if user.is_admin and user.id != admin.id:
        raise HTTPException(status_code=400, detail="Administradores já estão aprovados")
    user.is_approved = True
    await db.commit()
    await db.refresh(user)
    return user


@router.post("/users/{user_id}/revoke", response_model=AdminUserOut)
async def revoke_user(
    user_id: int,
    admin: User = Depends(get_admin_user),
    db: AsyncSession = Depends(get_db),
):
    user = await db.scalar(select(User).where(User.id == user_id))
    if not user:
        raise HTTPException(status_code=404, detail="Usuário não encontrado")
    if user.is_admin:
        raise HTTPException(status_code=400, detail="Não é possível revogar um administrador")
    if user.id == admin.id:
        raise HTTPException(status_code=400, detail="Não é possível revogar a si mesmo")
    user.is_approved = False
    await db.commit()
    await db.refresh(user)
    return user
