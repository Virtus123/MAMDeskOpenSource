from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    app_name: str = "MAMDesk"
    debug: bool = False

    database_url: str = "postgresql+asyncpg://mamdesk:mamdesk@localhost:5432/mamdesk"
    redis_url: str = "redis://localhost:6379/0"

    jwt_secret: str = "CHANGE-ME-IN-PRODUCTION-use-openssl-rand-hex-32"
    jwt_algorithm: str = "HS256"
    jwt_expire_minutes: int = 1440

    cors_origins: list[str] = ["*"]

    # Seed opcional do admin na primeira instalação (defina no .env)
    mamdesk_admin_email: str | None = None
    mamdesk_admin_password: str | None = None

    @property
    def admin_seed_email(self) -> str | None:
        return self.mamdesk_admin_email

    @property
    def admin_seed_password(self) -> str | None:
        return self.mamdesk_admin_password

    # WebSocket / signaling
    ws_heartbeat_seconds: int = 30
    device_offline_timeout_seconds: int = 300

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
