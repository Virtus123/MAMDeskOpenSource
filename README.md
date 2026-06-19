# MAMDesk

[![CI](https://github.com/Virtus123/MAMDesk/actions/workflows/ci.yml/badge.svg)](https://github.com/Virtus123/MAMDesk/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Plataforma de suporte remoto estilo AnyDesk, com dois clientes Windows e servidor centralizado self-hosted.

## Arquitetura

```
PC Cliente (Quick Support)          PC Operador (Painel)
         |                                    |
         +-------- Internet / Servidor -------+
                         |
              FastAPI + PostgreSQL + Redis
              (login, auth, signaling, relay)
```

O servidor faz login, autenticação, cadastro, descoberta online e **relay de signaling**. O streaming passa pela sessão WebSocket (MVP) e será migrado para **WebRTC P2P (H.264)**.

## Dois clientes

| Cliente | Público | Função |
|---------|---------|--------|
| **MAMDesk.QuickSupport** | Cliente final | Mostra ID + senha, aceita/recusa conexão, compartilha tela |
| **MAMDesk.Operator** | Sua equipe | Login, conectar por ID, lista de dispositivos, configurações (futuro) |

## Estrutura

```
MAMDESK/
├── server/          # FastAPI + PostgreSQL + Redis
├── client/
│   ├── MAMDesk.sln
│   └── src/
│       ├── MAMDesk.Shared/
│       ├── MAMDesk.QuickSupport/
│       └── MAMDesk.Operator/
├── deploy/          # Scripts de deploy (requer deploy/.env — não commitar)
└── docs/            # Assinatura de código, SignPath, etc.
```

## Requisitos

- **Servidor:** Python 3.12+, Docker (opcional)
- **Cliente:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) + Windows 10+

## Servidor — desenvolvimento local

```bash
cd server
cp .env.example .env
docker compose up -d postgres redis
pip install -r requirements.txt
uvicorn app.main:app --reload --host 0.0.0.0 --port 8100
```

Criar operador:

```bash
cd server
python scripts/create_operator.py
```

Opcional: defina `MAMDESK_ADMIN_EMAIL` e `MAMDESK_ADMIN_PASSWORD` no `.env` para seed automático na primeira subida.

## Cliente — compilar

```powershell
cd client
dotnet restore
dotnet build -c Release

dotnet run --project src/MAMDesk.QuickSupport
dotnet run --project src/MAMDesk.Operator
```

Por padrão os clientes apontam para `http://localhost:8100`. Variáveis de ambiente opcionais:

```
MAMDESK_SERVER=http://seu-servidor:8100
MAMDESK_WS=ws://seu-servidor:8100
```

## Fluxo de conexão

1. **Cliente** abre Quick Support → vê ID + senha → fica online no servidor
2. **Operador** faz login no painel → digita ID + senha → clica Conectar
3. **Cliente** recebe pedido → Aceitar / Recusar
4. **Sessão** estabelecida → tela, mouse, teclado e chat

## MVP implementado

- [x] Login de operador (JWT + bcrypt)
- [x] Cadastro de dispositivo (Quick Support)
- [x] Lista de dispositivos (operador logado)
- [x] Conectar por ID + senha
- [x] Compartilhamento de tela (JPEG via signaling)
- [x] Controle de mouse e teclado
- [x] Chat na sessão remota

## Deploy em produção

1. Copie `deploy/.env.example` para `deploy/.env` e preencha (host, domínio, credenciais SSH).
2. **Nunca** commite `deploy/.env`.
3. Instale o servidor: `python deploy/remote_deploy.py`
4. Publique site e downloads: `python deploy/deploy_site.py`

Veja [docs/CODE_SIGNING.md](docs/CODE_SIGNING.md) para assinar os `.exe` via SignPath (gratuito para OSS).

## Contribuir

Leia [CONTRIBUTING.md](CONTRIBUTING.md). Reporte vulnerabilidades conforme [SECURITY.md](SECURITY.md).

## Licença

[MIT](LICENSE) — Copyright (c) 2026 MAM Acesso / Virtus123
