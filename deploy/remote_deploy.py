#!/usr/bin/env python3
"""Deploy MAMDesk server para VPS via SSH (paramiko)."""

import io
import os
import secrets
import sys
import tarfile
import textwrap
from pathlib import Path

from ssh_env import connect, ensure_deploy_env, ssh_config

LOCAL_SERVER = Path(__file__).resolve().parents[1] / "server"

SKIP_DIRS = {"__pycache__", ".git", "venv", ".venv"}
SKIP_FILES = {".env"}


def run(client, cmd: str, check=True) -> tuple[int, str, str]:
    print(f"  $ {cmd[:140]}")
    _, stdout, stderr = client.exec_command(cmd, get_pty=True, timeout=600)
    exit_code = stdout.channel.recv_exit_status()
    out = stdout.read().decode("utf-8", errors="replace")
    err = stderr.read().decode("utf-8", errors="replace")
    if out.strip():
        print(out[-3000:] if len(out) > 3000 else out)
    if check and exit_code != 0:
        raise RuntimeError(f"Comando falhou ({exit_code}): {cmd}\n{err}")
    return exit_code, out, err


def create_tarball() -> bytes:
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w:gz") as tar:
        for root, dirs, files in os.walk(LOCAL_SERVER):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for name in files:
                if name in SKIP_FILES:
                    continue
                full = Path(root) / name
                arc = full.relative_to(LOCAL_SERVER.parent)
                tar.add(full, arcname=str(arc).replace("\\", "/"))
    buf.seek(0)
    return buf.read()


def main():
    ensure_deploy_env()
    cfg = ssh_config()
    install_dir = cfg["install_dir"]
    api_port = cfg["api_port"]
    host = cfg["host"]
    db_password = cfg["db_password"] or secrets.token_urlsafe(24)
    jwt_secret = secrets.token_hex(32)

    admin_email = cfg["admin_email"]
    admin_password = cfg["admin_password"]

    print(f"=== Deploy MAMDesk -> {host}:{api_port} ===")
    print(f"Instalando em {install_dir} (SEM tocar /var/www/)")

    client = connect()

    try:
        run(
            client,
            "echo 'Produção em /var/www/:'; ls /var/www/ 2>/dev/null | head -5; echo '---'; uname -a",
            check=False,
        )

        run(client, f"mkdir -p {install_dir}")

        print("Enviando arquivos...")
        sftp = client.open_sftp()
        sftp.putfo(io.BytesIO(create_tarball()), "/tmp/mamdesk-server.tar.gz")

        env_lines = [
            "APP_NAME=MAMDesk",
            "DEBUG=false",
            f"DATABASE_URL=postgresql+asyncpg://mamdesk:{db_password}@127.0.0.1:5432/mamdesk",
            "REDIS_URL=redis://127.0.0.1:6379/0",
            f"JWT_SECRET={jwt_secret}",
            "JWT_ALGORITHM=HS256",
            "JWT_EXPIRE_MINUTES=1440",
        ]
        if admin_email and admin_password:
            env_lines.append(f"MAMDESK_ADMIN_EMAIL={admin_email}")
            env_lines.append(f"MAMDESK_ADMIN_PASSWORD={admin_password}")

        env_content = "\n".join(env_lines) + "\n"
        with sftp.file(f"{install_dir}/.env", "w") as f:
            f.write(env_content)

        ecosystem = textwrap.dedent(f"""\
            module.exports = {{
              apps: [{{
                name: 'mamdesk-api',
                cwd: '{install_dir}',
                script: '{install_dir}/venv/bin/uvicorn',
                args: 'app.main:app --host 0.0.0.0 --port {api_port}',
                interpreter: 'none',
                autorestart: true,
                max_restarts: 10,
              }}]
            }};
        """)
        with sftp.file(f"{install_dir}/ecosystem.config.cjs", "w") as f:
            f.write(ecosystem)
        sftp.close()

        run(
            client,
            f"cd {install_dir} && tar -xzf /tmp/mamdesk-server.tar.gz --strip-components=1 "
            "&& rm -f /tmp/mamdesk-server.tar.gz",
        )

        setup = f"""
set -e
export DEBIAN_FRONTEND=noninteractive

if ! command -v psql &>/dev/null; then
  apt-get update -qq
  apt-get install -y -qq postgresql postgresql-contrib redis-server python3 python3-pip python3-venv curl
fi

systemctl enable postgresql redis-server 2>/dev/null || true
systemctl start postgresql redis-server 2>/dev/null || true

sudo -u postgres psql -tc "SELECT 1 FROM pg_roles WHERE rolname='mamdesk'" | grep -q 1 || \\
  sudo -u postgres psql -c "CREATE USER mamdesk WITH PASSWORD '{db_password}';"
sudo -u postgres psql -tc "SELECT 1 FROM pg_database WHERE datname='mamdesk'" | grep -q 1 || \\
  sudo -u postgres psql -c "CREATE DATABASE mamdesk OWNER mamdesk;"

cd {install_dir}
python3 -m venv venv
./venv/bin/pip install -q --upgrade pip
./venv/bin/pip install -q -r requirements.txt

if ! command -v pm2 &>/dev/null; then
  if ! command -v node &>/dev/null; then
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
    apt-get install -y -qq nodejs
  fi
  npm install -g pm2
fi

pm2 delete mamdesk-api 2>/dev/null || true
pm2 start {install_dir}/ecosystem.config.cjs
pm2 save
env PATH=$PATH:/usr/bin pm2 startup systemd -u root --hp /root 2>/dev/null | tail -1 | bash 2>/dev/null || true

sleep 4
curl -sf http://127.0.0.1:{api_port}/health && echo " OK" || echo "Health check pendente"
"""
        print("Instalando dependências e iniciando PM2...")
        run(client, setup)

        if admin_email and admin_password:
            print(f"Admin seed configurado via env para {admin_email}")
        else:
            print(
                "AVISO: MAMDESK_ADMIN_EMAIL/PASSWORD não definidos. "
                "Use server/scripts/create_operator.py após o deploy."
            )

        run(client, f"pm2 list; curl -s http://127.0.0.1:{api_port}/health", check=False)
        print("\n=== DEPLOY CONCLUÍDO ===")
        print(f"API:    http://{host}:{api_port}")
        print(f"Health: http://{host}:{api_port}/health")
        print(f"Pasta:  {install_dir} (produção em /var/www/ intacta)")

    finally:
        client.close()


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"ERRO: {e}", file=sys.stderr)
        sys.exit(1)
