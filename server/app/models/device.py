from datetime import datetime

from sqlalchemy import Boolean, DateTime, ForeignKey, String, func
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.database import Base


class Device(Base):
    __tablename__ = "dispositivos"

    id: Mapped[int] = mapped_column(primary_key=True, autoincrement=True)
    usuario_id: Mapped[int | None] = mapped_column(
        ForeignKey("usuarios.id", ondelete="SET NULL"), nullable=True, index=True
    )
    device_uid: Mapped[str] = mapped_column(String(64), unique=True, nullable=False, index=True)
    nome_pc: Mapped[str] = mapped_column(String(120), nullable=False)
    senha_sessao_hash: Mapped[str] = mapped_column(String(255), nullable=False)
    tipo: Mapped[str] = mapped_column(String(20), default="quick", nullable=False)
    online: Mapped[bool] = mapped_column(Boolean, default=False, nullable=False)
    ultimo_ip: Mapped[str | None] = mapped_column(String(45), nullable=True)
    ultima_conexao: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    criado_em: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    usuario: Mapped["User | None"] = relationship(back_populates="dispositivos")
    acessos: Mapped[list["DeviceAccess"]] = relationship(back_populates="dispositivo", cascade="all, delete-orphan")
