"""Migrações leves e seed executados no startup."""

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.config import settings
from app.core.security import hash_password
from app.models.user import User


async def _exec(session: AsyncSession, sql: str) -> None:
    await session.execute(text(sql))


async def run_migrations(session: AsyncSession) -> None:
    await _exec(
        session,
        """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            name VARCHAR(120) PRIMARY KEY,
            applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        )
        """,
    )
    await _exec(
        session,
        "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS is_admin BOOLEAN NOT NULL DEFAULT FALSE",
    )
    await _exec(
        session,
        "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS is_approved BOOLEAN NOT NULL DEFAULT FALSE",
    )
    await _exec(
        session,
        """
        CREATE TABLE IF NOT EXISTS device_access (
            id SERIAL PRIMARY KEY,
            user_id INTEGER NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
            device_id INTEGER NOT NULL REFERENCES dispositivos(id) ON DELETE CASCADE,
            first_access_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            last_access_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            CONSTRAINT uq_user_device UNIQUE (user_id, device_id)
        )
        """,
    )
    await _exec(session, "CREATE INDEX IF NOT EXISTS ix_device_access_user_id ON device_access(user_id)")
    await _exec(session, "CREATE INDEX IF NOT EXISTS ix_device_access_device_id ON device_access(device_id)")

    legacy = await session.scalar(
        text("SELECT 1 FROM schema_migrations WHERE name = 'approve_legacy_users'")
    )
    if not legacy:
        await _exec(session, "UPDATE usuarios SET is_approved = TRUE WHERE is_approved = FALSE")
        await _exec(
            session,
            "INSERT INTO schema_migrations (name) VALUES ('approve_legacy_users')",
        )

    await session.commit()


async def seed_admin(session: AsyncSession) -> None:
    email = settings.admin_seed_email
    password = settings.admin_seed_password
    if not email or not password:
        return

    result = await session.execute(
        text("SELECT id FROM usuarios WHERE email = :email"),
        {"email": email},
    )
    existing = result.scalar_one_or_none()
    if existing:
        await session.execute(
            text("UPDATE usuarios SET is_admin = TRUE, is_approved = TRUE WHERE email = :email"),
            {"email": email},
        )
    else:
        user = User(
            nome="Administrador",
            email=email,
            senha_hash=hash_password(password),
            is_admin=True,
            is_approved=True,
        )
        session.add(user)

    await session.commit()
