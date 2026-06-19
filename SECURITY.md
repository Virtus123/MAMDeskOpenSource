# Política de segurança

## Versões suportadas

| Versão | Suporte |
|--------|---------|
| main (última release) | Sim |
| Anteriores | Não |

## Reportar vulnerabilidade

Envie um e-mail para **vitorfernandes.y02@gmail.com** (ou abra um [GitHub Security Advisory](https://github.com/Virtus123/MAMDeskOpenSource/security/advisories/new) privado).

Inclua:
- Descrição do problema
- Passos para reproduzir
- Impacto estimado

Responderemos em até **72 horas** úteis.

## Boas práticas para quem faz deploy

- Altere `JWT_SECRET` e senhas padrão do PostgreSQL
- Use HTTPS (Let's Encrypt) na API e no site de downloads
- Não commite `deploy/.env` nem certificados `.pfx`
- Prefira releases assinadas via SignPath ou certificado EV

## Credenciais padrão

O servidor **não** cria admin automaticamente em produção. Defina `MAMDESK_ADMIN_EMAIL` e `MAMDESK_ADMIN_PASSWORD` no `.env` apenas na primeira instalação, ou use `server/scripts/create_operator.py`.
