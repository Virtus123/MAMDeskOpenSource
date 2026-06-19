"""Configuração SSH para scripts de deploy — lê deploy/.env ou variáveis de ambiente."""

from __future__ import annotations

import os
import shutil
from pathlib import Path

import paramiko

_DEPLOY_DIR = Path(__file__).resolve().parent
_ENV_FILE = _DEPLOY_DIR / ".env"
_ENV_EXAMPLE = _DEPLOY_DIR / ".env.example"


def ensure_deploy_env() -> None:
    """Cria deploy/.env a partir do exemplo se ainda não existir."""
    if _ENV_FILE.exists():
        return
    if not _ENV_EXAMPLE.exists():
        raise SystemExit("Arquivo deploy/.env.example não encontrado.")
    shutil.copy(_ENV_EXAMPLE, _ENV_FILE)
    print(f"Criado {_ENV_FILE} a partir do exemplo.")
    print("Edite deploy/.env com seus valores e execute novamente.")
    raise SystemExit(1)


def _load_dotenv() -> None:
    if not _ENV_FILE.exists():
        return
    for line in _ENV_FILE.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        os.environ.setdefault(key, value)


def ssh_config() -> dict[str, str]:
    _load_dotenv()
    host = os.environ.get("MAMDESK_VPS_HOST", "").strip()
    user = os.environ.get("MAMDESK_VPS_USER", "root").strip() or "root"
    password = os.environ.get("MAMDESK_VPS_PASSWORD", "").strip()
    key_path = os.environ.get("MAMDESK_VPS_SSH_KEY", "").strip()
    install_dir = os.environ.get("MAMDESK_INSTALL_DIR", "/opt/mamdesk").strip()
    www_dir = os.environ.get("MAMDESK_WWW_DIR", f"{install_dir}/www").strip()
    site_domain = os.environ.get("MAMDESK_SITE_DOMAIN", "").strip()
    certbot_email = os.environ.get("MAMDESK_CERTBOT_EMAIL", "").strip()
    api_port = os.environ.get("MAMDESK_API_PORT", "8100").strip() or "8100"
    admin_email = os.environ.get("MAMDESK_ADMIN_EMAIL", "").strip()
    admin_password = os.environ.get("MAMDESK_ADMIN_PASSWORD", "").strip()
    db_password = os.environ.get("MAMDESK_DB_PASSWORD", "").strip()

    if not host:
        raise SystemExit(
            "Defina MAMDESK_VPS_HOST. Copie deploy/.env.example para deploy/.env"
        )
    if not password and not key_path:
        raise SystemExit(
            "Defina MAMDESK_VPS_PASSWORD ou MAMDESK_VPS_SSH_KEY em deploy/.env"
        )

    return {
        "host": host,
        "user": user,
        "password": password,
        "key_path": key_path,
        "install_dir": install_dir,
        "www_dir": www_dir,
        "site_domain": site_domain,
        "certbot_email": certbot_email,
        "api_port": api_port,
        "admin_email": admin_email,
        "admin_password": admin_password,
        "db_password": db_password,
    }


def connect(timeout: int = 30) -> paramiko.SSHClient:
    cfg = ssh_config()
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())

    kwargs: dict = {
        "hostname": cfg["host"],
        "username": cfg["user"],
        "timeout": timeout,
    }
    if cfg["key_path"]:
        kwargs["key_filename"] = cfg["key_path"]
    else:
        kwargs["password"] = cfg["password"]

    client.connect(**kwargs)
    return client


def render_nginx_config(template_path: Path, domain: str, www_dir: str) -> str:
    content = template_path.read_text(encoding="utf-8")
    return (
        content.replace("{{SITE_DOMAIN}}", domain)
        .replace("{{WWW_ROOT}}", www_dir)
    )
