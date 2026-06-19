#!/usr/bin/env python3
"""Deploy landing page + downloads + nginx."""

import sys
from pathlib import Path

from ssh_env import connect, ensure_deploy_env, render_nginx_config, ssh_config

ROOT = Path(__file__).resolve().parents[1]


def run(client, cmd, check=True):
    print(f"  $ {cmd[:100]}")
    _, stdout, stderr = client.exec_command(cmd, get_pty=True, timeout=300)
    code = stdout.channel.recv_exit_status()
    out = stdout.read().decode("utf-8", errors="replace")
    if out.strip():
        print(out[-2000:])
    if check and code != 0:
        raise RuntimeError(f"Falhou ({code}): {stderr.read().decode('utf-8', errors='replace')}")
    return code, out


def main():
    ensure_deploy_env()
    cfg = ssh_config()
    domain = cfg["site_domain"]
    if not domain:
        raise SystemExit("Defina MAMDESK_SITE_DOMAIN em deploy/.env")

    www = cfg["www_dir"]
    certbot_email = cfg["certbot_email"] or "admin@example.com"
    nginx_site = f"/etc/nginx/sites-available/{domain}"

    print("=== Deploy MAMDesk Site ===")

    client = connect()
    sftp = client.open_sftp()

    try:
        run(client, f"mkdir -p {www}/downloads {www}/panel")

        sftp.put(str(ROOT / "deploy" / "www" / "index.html"), f"{www}/index.html")
        sftp.put(str(ROOT / "deploy" / "www" / "panel" / "index.html"), f"{www}/panel/index.html")
        sftp.put(
            str(ROOT / "deploy" / "www" / "downloads" / "quicksupport.html"),
            f"{www}/downloads/quicksupport.html",
        )
        sftp.put(
            str(ROOT / "deploy" / "www" / "downloads" / "operator.html"),
            f"{www}/downloads/operator.html",
        )
        sftp.put(str(ROOT / "assets" / "Icon.ico"), f"{www}/favicon.ico")

        qs = ROOT / "client" / "dist-build" / "MAMDesk.QuickSupport.exe"
        op = ROOT / "client" / "dist-build" / "MAMDesk.Operator.exe"
        if not qs.exists() or not op.exists():
            print("ERRO: Rode client/build-single.ps1 antes do deploy.")
            sys.exit(1)

        print("Enviando executaveis...")
        sftp.put(str(qs), f"{www}/downloads/MAMDesk.QuickSupport.exe")
        sftp.put(str(op), f"{www}/downloads/MAMDesk.Operator.exe")

        nginx_content = render_nginx_config(
            ROOT / "deploy" / "nginx-mamdesk-site.conf", domain, www
        )
        with sftp.file(nginx_site, "w") as f:
            f.write(nginx_content)

        run(client, f"ln -sf {nginx_site} /etc/nginx/sites-enabled/{domain}")
        run(client, "nginx -t")
        run(client, "systemctl reload nginx")

        _, out = run(
            client,
            f"certbot certificates 2>/dev/null | grep {domain} || echo NO_CERT",
            check=False,
        )
        if "NO_CERT" in out or f"Certificate Name: {domain}" not in out:
            print("Solicitando certificado SSL...")
            run(
                client,
                f"certbot --nginx -d {domain} --non-interactive --agree-tos "
                f"-m {certbot_email} --redirect 2>&1 || echo CERTBOT_FAILED",
                check=False,
            )
        else:
            print("Certificado SSL já existe.")

        run(client, f"ls -lh {www}/downloads/", check=False)

        print("\n=== CONCLUÍDO ===")
        print(f"Site:  https://{domain}")
        print(f"Quick: https://{domain}/downloads/MAMDesk.QuickSupport.exe")
        print(f"Oper:  https://{domain}/downloads/MAMDesk.Operator.exe")

    finally:
        sftp.close()
        client.close()


if __name__ == "__main__":
    main()
