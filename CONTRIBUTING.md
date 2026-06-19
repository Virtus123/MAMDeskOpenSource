# Contribuindo

Obrigado por contribuir com o MAMDesk.

## Setup

1. Fork + clone
2. Servidor: `cd server && cp .env.example .env && docker compose up -d && pip install -r requirements.txt`
3. Cliente: `cd client && dotnet build`

## Pull requests

- Uma mudança lógica por PR
- Descreva o problema e a solução
- Teste login, QuickSupport e conexão remota quando alterar cliente/servidor

## Commits

Mensagens claras em português ou inglês, no imperativo: `Corrige crash ao focar campo de texto`.

## Licença

Ao contribuir, você concorda que seu código será licenciado sob a [MIT License](LICENSE).
