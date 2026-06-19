from datetime import datetime

from sqlalchemy import DateTime, ForeignKey, UniqueConstraint, func
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.database import Base


class DeviceAccess(Base):
    """Histórico de quais operadores acessaram quais dispositivos."""

    __tablename__ = "device_access"
    __table_args__ = (UniqueConstraint("user_id", "device_id", name="uq_user_device"),)

    id: Mapped[int] = mapped_column(primary_key=True, autoincrement=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("usuarios.id", ondelete="CASCADE"), index=True)
    device_id: Mapped[int] = mapped_column(ForeignKey("dispositivos.id", ondelete="CASCADE"), index=True)
    first_access_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    last_access_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    usuario: Mapped["User"] = relationship(back_populates="acessos")
    dispositivo: Mapped["Device"] = relationship(back_populates="acessos")
