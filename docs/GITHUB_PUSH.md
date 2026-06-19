# Publicar no GitHub (Virtus123/MAMDesk)

O GitHub CLI (`gh`) não está instalado ou autenticado nesta máquina. Siga os passos abaixo manualmente.

## 1. Criar Personal Access Token (PAT)

1. Acesse https://github.com/settings/tokens  
2. **Generate new token (classic)**  
3. Escopos necessários:
   - `repo` (repositório completo)
   - `workflow` (se quiser editar Actions depois)
4. Copie o token — ele só aparece uma vez. **Não commite nem salve em arquivos do projeto.**

## 2. Criar repositório no GitHub

1. https://github.com/new  
2. **Repository name:** `MAMDesk`  
3. **Visibility:** Public  
4. **Não** marque "Add a README" (o projeto já tem um)  
5. Crie o repositório

## 3. Enviar o código (PowerShell)

Execute na pasta do projeto (`MAMDESK`):

```powershell
cd C:\Users\vitor\OneDrive\Desktop\MAMDESK

git init
git add .
git status
# Confirme que deploy/.env, *.exe e server/.env NÃO aparecem

git commit -m "Open source: MAMDesk remote support platform"
git branch -M main
git remote add origin https://github.com/Virtus123/MAMDesk.git
git push -u origin main
```

Quando o Git pedir credenciais:

| Campo | Valor |
|-------|--------|
| Username | `Virtus123` |
| Password | **Cole o PAT** (não use a senha da conta com 2FA) |

## 4. Alternativa: GitHub CLI (opcional)

```powershell
winget install GitHub.cli
gh auth login
# Escolha: GitHub.com → HTTPS → Login with browser (ou token)

gh repo create Virtus123/MAMDesk --public --source=. --remote=origin --push
```

## 5. Após o push

1. Confira https://github.com/Virtus123/MAMDesk  
2. Solicite certificado SignPath: [SIGNPATH_APPLY.md](SIGNPATH_APPLY.md)  
3. Após aprovação SignPath, configure secrets no GitHub:
   - `SIGNPATH_API_TOKEN` (secret)
   - `SIGNPATH_ORGANIZATION_ID` (variable)

## Segurança

- **Nunca** commite `deploy/.env` — contém credenciais SSH da VPS  
- **Nunca** commite `server/.env` — JWT e banco de dados  
- Use PAT com escopo mínimo; revogue tokens antigos em https://github.com/settings/tokens
