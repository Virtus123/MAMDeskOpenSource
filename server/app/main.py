from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy import select

from app.config import settings
from app.database import Base, async_session, engine
from app.db_migrate import run_migrations, seed_admin
from app.routers import admin, auth, devices, websocket
from app.services.redis_service import close_redis


@asynccontextmanager
async def lifespan(app: FastAPI):
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
    async with async_session() as session:
        await run_migrations(session)
        await seed_admin(session)
    yield
    await close_redis()
    await engine.dispose()


app = FastAPI(title=settings.app_name, lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(auth.router, prefix="/api")
app.include_router(admin.router, prefix="/api")
app.include_router(devices.router, prefix="/api")
app.include_router(websocket.router)


@app.get("/health")
async def health():
    return {"status": "ok", "app": settings.app_name}
