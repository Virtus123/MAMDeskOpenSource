from datetime import datetime

from pydantic import BaseModel, EmailStr, Field


class UserCreate(BaseModel):
    nome: str = Field(min_length=2, max_length=120)
    email: EmailStr
    senha: str = Field(min_length=6, max_length=128)


class UserLogin(BaseModel):
    email: EmailStr
    senha: str


class UserOut(BaseModel):
    id: int
    nome: str
    email: EmailStr
    is_admin: bool = False
    is_approved: bool = False

    model_config = {"from_attributes": True}


class AdminUserOut(BaseModel):
    id: int
    nome: str
    email: EmailStr
    is_admin: bool
    is_approved: bool
    criado_em: datetime

    model_config = {"from_attributes": True}


class AdminUserCreate(BaseModel):
    nome: str = Field(min_length=2, max_length=120)
    email: EmailStr
    senha: str = Field(min_length=6, max_length=128)
    is_approved: bool = True


class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    usuario: UserOut
