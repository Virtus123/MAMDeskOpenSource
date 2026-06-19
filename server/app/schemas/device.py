from datetime import datetime

from pydantic import BaseModel, Field


class DeviceRegister(BaseModel):
    device_uid: str = Field(min_length=8, max_length=64)
    nome_pc: str = Field(min_length=1, max_length=120)
    senha_sessao: str = Field(min_length=4, max_length=32)
    tipo: str = Field(default="quick", pattern="^(quick|managed)$")


class DeviceUpdate(BaseModel):
    nome_pc: str | None = Field(default=None, min_length=1, max_length=120)
    senha_sessao: str | None = Field(default=None, min_length=4, max_length=32)


class DeviceOut(BaseModel):
    id: int
    device_uid: str
    nome_pc: str
    online: bool
    ultimo_ip: str | None
    ultima_conexao: datetime | None
    tipo: str

    model_config = {"from_attributes": True}


class ConnectRequest(BaseModel):
    device_uid: str = Field(min_length=8, max_length=64)
    senha_sessao: str = Field(min_length=4, max_length=32)
    session_id: str | None = Field(default=None, min_length=36, max_length=36)


class OperatorConnectRequest(BaseModel):
    device_uid: str = Field(min_length=8, max_length=64)
    session_id: str | None = Field(default=None, min_length=36, max_length=36)


class RecordAccessRequest(BaseModel):
    device_uid: str = Field(min_length=1, max_length=64)


class ConnectResponse(BaseModel):
    session_id: str
    device_uid: str
    nome_pc: str
    status: str
