# Assinatura de código (Authenticode) — MAMDesk

O MAMDesk usa **SignPath Foundation** (gratuito para projetos open source) para assinar os `.exe` e eliminar avisos do SmartScreen / Chrome.

## Passo a passo (faça uma vez)

### 1. Publicar o repositório no GitHub

```powershell
cd MAMDESK
git init
git add .
git commit -m "Open source: MAMDesk remote support"
git branch -M main
git remote add origin https://github.com/Virtus123/MAMDesk.git
git push -u origin main
```

O repositório precisa ser **público** e usar licença OSI (este projeto usa MIT).

### 2. Solicitar certificado gratuito

1. Acesse https://about.signpath.io/product/open-source  
2. Clique em **Apply for free**  
3. Informe:
   - URL do repositório GitHub
   - Descrição: software de suporte remoto (QuickSupport + Operador)
   - Motivo: eliminar bloqueio SmartScreen/Chrome em downloads `.exe`

Aprovação costuma levar alguns dias úteis.

### 3. Configurar projeto no SignPath

Após aprovação, no painel SignPath:

| Campo | Valor |
|-------|--------|
| Project slug | `MAMDesk` |
| Repository URL | `https://github.com/Virtus123/MAMDesk` |
| Trusted build system | GitHub.com (instale o app SignPath no repo) |

**Artifact configuration:** importe o arquivo  
`.signpath/artifact-configurations/windows-clients.xml`  
(slug sugerido: `windows-clients`).

**Signing policies:**
- `test-signing` — builds de PR / branches de dev
- `release-signing` — branch `main` e tags de release (com origin verification)

### 4. Secrets no GitHub

Em **Settings → Secrets and variables → Actions**:

| Nome | Tipo | Onde obter |
|------|------|------------|
| `SIGNPATH_API_TOKEN` | Secret | SignPath → API tokens |
| `SIGNPATH_ORGANIZATION_ID` | Variable | Canto superior direito do painel SignPath |

### 5. Gerar release assinada

**Opção A — Release no GitHub:**
1. Crie uma release (tag `v1.0.0`)
2. O workflow `.github/workflows/release.yml` compila, envia ao SignPath e anexa os `.exe` assinados

**Opção B — Manual:**
1. Actions → **Release** → **Run workflow**

### 6. Publicar no site

Com os `.exe` assinados em `signed-out/` (ou baixados da release):

```powershell
# Copie os assinados para dist-build
Copy-Item signed-out\*.exe client\dist-build\ -Force
python deploy/deploy_exes_only.py
```

## Assinatura local (certificado próprio)

Se você comprar um certificado EV/OV (DigiCert, Sectigo, SSL.com ~US$200–500/ano):

```powershell
$env:SIGN_CERT_PATH = "C:\certs\mamdesk.pfx"
$env:SIGN_CERT_PASSWORD = "senha-do-certificado"
.\client\sign-release.ps1
```

Requer Windows SDK (`signtool.exe` no PATH).

## Alternativas pagas

| Opção | Custo | SmartScreen |
|-------|-------|-------------|
| SignPath OSS | Grátis | Bom (certificado da foundation) |
| Sectigo / DigiCert OV | ~US$200/ano | Melhora após reputação |
| DigiCert EV | ~US$400+/ano | Reputação imediata |

## Referências

- [SignPath + GitHub Actions](https://docs.signpath.io/trusted-build-systems/github)
- [Condições OSS SignPath Foundation](https://signpath.org/terms.html)
