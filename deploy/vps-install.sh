#!/bin/bash
# Deploy MAMDesk na VPS — NÃO mexe em /var/www/
set -euo pipefail

INSTALL_DIR="/opt/mamdesk"
SERVICE_USER="mamdesk"

echo "=== MAMDesk Deploy ==="
echo "Instalando em ${INSTALL_DIR} (sem tocar em /var/www/)"

if [ "$EUID" -ne 0 ]; then
  echo "Execute como root: sudo bash deploy/vps-install.sh"
  exit 1
fi

mkdir -p "${INSTALL_DIR}"
cp -r server/* "${INSTALL_DIR}/"

if ! id "${SERVICE_USER}" &>/dev/null; then
  useradd -r -s /bin/false "${SERVICE_USER}"
fi

chown -R "${SERVICE_USER}:${SERVICE_USER}" "${INSTALL_DIR}"

cd "${INSTALL_DIR}"

if [ ! -f .env ]; then
  cp .env.example .env
  echo "ATENÇÃO: Edite ${INSTALL_DIR}/.env antes de subir em produção!"
fi

docker compose up -d --build

cat > /etc/systemd/system/mamdesk.service << 'EOF'
[Unit]
Description=MAMDesk API
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/opt/mamdesk
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
User=root

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable mamdesk

echo ""
echo "MAMDesk instalado em ${INSTALL_DIR}"
echo "Configure nginx com HTTPS apontando para localhost:8000"
echo "Exemplo: deploy/nginx-mamdesk.conf"
