#!/usr/bin/env python3
"""Cria o primeiro usuário operador no MAMDesk."""

import asyncio
import getpass
import sys

sys.path.insert(0, ".")

from sqlalchemy import select

from app.core.security import hash_password
from app.database import async_session
from app.models.user import User


async def main():
    nome = input("Nome: ").strip()
    email = input("E-mail: ").strip()
    senha = getpass.getpass("Senha: ")

    async with async_session() as db:
        existing = await db.scalar(select(User).where(User.email == email))
        if existing:
            print("E-mail já cadastrado.")
            return

        user = User(nome=nome, email=email, senha_hash=hash_password(senha))
        db.add(user)
        await db.commit()
        print(f"Operador criado: {user.email} (id={user.id})")


if __name__ == "__main__":
    asyncio.run(main())
