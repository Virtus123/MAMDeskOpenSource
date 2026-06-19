# Solicitar certificado SignPath (open source)

O certificado **não pode ser obtido automaticamente**. Siga os passos abaixo após o repositório estar público no GitHub.

## Pré-requisitos

- Repositório **público**: https://github.com/Virtus123/MAMDesk
- Licença MIT (já incluída no projeto)
- Builds via GitHub Actions (workflow `release.yml`)

## Passo 1 — Abrir o formulário

1. Acesse https://about.signpath.io/product/open-source  
2. Clique em **Apply for free** (ou equivalente em português/inglês)

## Passo 2 — Preencher a aplicação

Use o texto abaixo (copie e cole nos campos correspondentes):

---

**Nome do projeto:** MAMDesk

**URL do repositório:**  
https://github.com/Virtus123/MAMDesk

**Licença:** MIT

**Descrição do projeto (português):**

> MAMDesk é uma plataforma de suporte remoto open source, similar ao AnyDesk/TeamViewer, composta por dois clientes Windows (.NET 8) e um servidor self-hosted (FastAPI + PostgreSQL + Redis).
>
> - **MAMDesk.QuickSupport** — aplicativo leve para o cliente final (ID + senha de sessão, compartilhamento de tela).
> - **MAMDesk.Operator** — painel para técnicos (login JWT, conexão por ID, controle remoto e chat).
>
> O código é distribuído publicamente para permitir auditoria, contribuições da comunidade e implantação em infraestrutura própria.

**Por que precisa de assinatura de código:**

> Os executáveis Windows são distribuídos como `.exe` portáteis. Sem assinatura Authenticode, o Windows SmartScreen e o Google Chrome bloqueiam ou alertam fortemente no download, prejudicando usuários legítimos. A assinatura via SignPath Foundation garante integridade e confiança sem custo para projetos open source.

**Sistema de build confiável:**

> GitHub Actions (repositório acima). Workflow: `.github/workflows/release.yml` — compila os clientes, envia artefato ao SignPath e publica releases assinadas.

**Contato:** vitorfernandes.y02@gmail.com (Virtus123)

---

## Passo 3 — Após aprovação (alguns dias úteis)

1. Crie o projeto **MAMDesk** no painel SignPath  
2. Instale o app **SignPath** no repositório GitHub (permissão de leitura de Actions/artefatos)  
3. Importe a configuração de artefato: `.signpath/artifact-configurations/windows-clients.xml`  
4. Crie políticas de assinatura:
   - `test-signing` — PRs e branches de desenvolvimento
   - `release-signing` — branch `main` e tags de release
5. No GitHub, em **Settings → Secrets and variables → Actions**:
   - Secret: `SIGNPATH_API_TOKEN`
   - Variable: `SIGNPATH_ORGANIZATION_ID`
6. Dispare uma release ou execute manualmente o workflow **Release**

Detalhes completos: [CODE_SIGNING.md](CODE_SIGNING.md)

## Links úteis

- [SignPath Open Source](https://about.signpath.io/product/open-source)
- [Termos SignPath Foundation](https://signpath.org/terms.html)
- [Integração GitHub Actions](https://docs.signpath.io/trusted-build-systems/github)
