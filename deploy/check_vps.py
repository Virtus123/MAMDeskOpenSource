#!/usr/bin/env python3
"""Verifica saúde básica da instalação remota."""

from ssh_env import connect, ensure_deploy_env, ssh_config


def main():
    ensure_deploy_env()
    cfg = ssh_config()
    domain = cfg["site_domain"] or "seu-dominio"
    www = cfg["www_dir"]
    api_port = cfg["api_port"]
    nginx_site = f"/etc/nginx/sites-available/{domain}"

    c = connect()
    cmds = [
        f"test -f {www}/panel/index.html && echo PANEL_OK || echo PANEL_MISSING",
        f"grep 'location /panel' {nginx_site} || echo NGINX_PANEL_MISSING",
        f"grep 'location /api' {nginx_site} || echo NGINX_API_MISSING",
        f"curl -s http://127.0.0.1:{api_port}/health",
        f"ls -lh {www}/downloads/*.exe 2>/dev/null | tail -2",
    ]
    for cmd in cmds:
        _, o, _ = c.exec_command(cmd)
        print(o.read().decode().strip())
        print("---")
    c.close()


if __name__ == "__main__":
    main()
