#!/usr/bin/env python3
"""Sobe painel admin + nginx (sem exes)."""

from pathlib import Path

from ssh_env import connect, ensure_deploy_env, render_nginx_config, ssh_config

ROOT = Path(__file__).resolve().parents[1]


def run(client, cmd):
    _, stdout, stderr = client.exec_command(cmd, get_pty=True, timeout=120)
    code = stdout.channel.recv_exit_status()
    out = stdout.read().decode("utf-8", errors="replace")
    if out.strip():
        print(out)
    if code != 0:
        raise RuntimeError(stderr.read().decode("utf-8", errors="replace") or f"exit {code}")


def main():
    ensure_deploy_env()
    cfg = ssh_config()
    domain = cfg["site_domain"]
    if not domain:
        raise SystemExit("Defina MAMDESK_SITE_DOMAIN em deploy/.env")

    www = cfg["www_dir"]
    nginx_site = f"/etc/nginx/sites-available/{domain}"

    client = connect()
    sftp = client.open_sftp()
    try:
        run(client, f"mkdir -p {www}/panel")
        sftp.put(str(ROOT / "deploy" / "www" / "panel" / "index.html"), f"{www}/panel/index.html")

        nginx_content = render_nginx_config(
            ROOT / "deploy" / "nginx-mamdesk-site.conf", domain, www
        )
        with sftp.file(nginx_site, "w") as f:
            f.write(nginx_content)

        run(client, f"ln -sf {nginx_site} /etc/nginx/sites-enabled/{domain}")
        run(client, "nginx -t")
        run(client, "systemctl reload nginx")
        print(f"Painel publicado: https://{domain}/panel/")
    finally:
        sftp.close()
        client.close()


if __name__ == "__main__":
    main()
