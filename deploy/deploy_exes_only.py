#!/usr/bin/env python3
"""Envia apenas os exes novos para a VPS."""

from pathlib import Path

from ssh_env import connect, ensure_deploy_env, ssh_config

ROOT = Path(__file__).resolve().parents[1]


def main():
    ensure_deploy_env()
    cfg = ssh_config()
    www = f"{cfg['www_dir']}/downloads"

    qs = ROOT / "client" / "dist-build" / "MAMDesk.QuickSupport.exe"
    op = ROOT / "client" / "dist-build" / "MAMDesk.Operator.exe"
    if not qs.exists() or not op.exists():
        raise SystemExit("Exes não encontrados. Rode client/build-single.ps1")

    client = connect()
    sftp = client.open_sftp()
    print("Enviando QuickSupport...")
    sftp.put(str(qs), f"{www}/MAMDesk.QuickSupport.exe")
    print("Enviando Operator...")
    sftp.put(str(op), f"{www}/MAMDesk.Operator.exe")
    sftp.close()
    _, o, _ = client.exec_command(f"ls -lh {www}/*.exe")
    print(o.read().decode())
    client.close()
    print("Executáveis atualizados no site.")


if __name__ == "__main__":
    main()
