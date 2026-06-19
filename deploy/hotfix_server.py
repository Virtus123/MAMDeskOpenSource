#!/usr/bin/env python3
"""Atualiza arquivos do servidor MAMDesk na VPS e reinicia a API."""

from pathlib import Path

from ssh_env import connect, ensure_deploy_env, ssh_config

ROOT = Path(__file__).resolve().parents[1]

FILES = [
    "main.py",
    "db_migrate.py",
    "core/deps.py",
    "routers/auth.py",
    "routers/admin.py",
    "routers/devices.py",
    "routers/websocket.py",
    "models/__init__.py",
    "models/user.py",
    "models/device.py",
    "models/device_access.py",
    "schemas/user.py",
    "schemas/device.py",
    "services/device_access.py",
    "services/signaling.py",
    "services/redis_service.py",
    "config.py",
]


def main():
    ensure_deploy_env()
    cfg = ssh_config()
    remote = f"{cfg['install_dir']}/app"

    client = connect()
    sftp = client.open_sftp()

    for rel in FILES:
        local = ROOT / "server" / "app" / rel
        if not local.exists():
            print(f"AVISO: arquivo local ausente: {rel}")
            continue
        remote_path = f"{remote}/{rel}"
        print(f"Enviando {rel}...")
        sftp.put(str(local), remote_path)

    sftp.close()
    _, stdout, _ = client.exec_command(
        "pm2 restart mamdesk-api && sleep 3 && pm2 logs mamdesk-api --lines 15 --nostream"
    )
    print(stdout.read().decode("utf-8", errors="replace"))
    client.close()
    print("Servidor atualizado.")


if __name__ == "__main__":
    main()
