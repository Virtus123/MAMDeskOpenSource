from datetime import UTC, datetime

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.device_access import DeviceAccess


async def record_device_access(db: AsyncSession, user_id: int, device_id: int) -> None:
    """Registra ou atualiza histórico de acesso operador → dispositivo."""
    access = await db.scalar(
        select(DeviceAccess).where(
            DeviceAccess.user_id == user_id,
            DeviceAccess.device_id == device_id,
        )
    )
    now = datetime.now(UTC)
    if access:
        access.last_access_at = now
    else:
        db.add(DeviceAccess(user_id=user_id, device_id=device_id, first_access_at=now, last_access_at=now))
    await db.flush()
