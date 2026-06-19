# Assina os .exe em client/dist-build com certificado Authenticode local.
# Requer Windows SDK (signtool) e variáveis SIGN_CERT_PATH + SIGN_CERT_PASSWORD.

param(
    [string]$CertPath = $env:SIGN_CERT_PATH,
    [string]$CertPassword = $env:SIGN_CERT_PASSWORD,
    [string]$DistDir = (Join-Path $PSScriptRoot "dist-build"),
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

if (-not $CertPath -or -not (Test-Path $CertPath)) {
    Write-Error "Defina SIGN_CERT_PATH apontando para um arquivo .pfx"
}
if (-not $CertPassword) {
    Write-Error "Defina SIGN_CERT_PASSWORD"
}

$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if (-not $signtool) {
    $sdk = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $sdk) {
        $latest = Get-ChildItem $sdk -Directory | Sort-Object Name -Descending | Select-Object -First 1
        $candidate = Join-Path $latest.FullName "x64\signtool.exe"
        if (Test-Path $candidate) { $signtool = $candidate }
    }
}
if (-not $signtool) { Write-Error "signtool.exe nao encontrado. Instale Windows SDK." }
if ($signtool -is [System.Management.Automation.CommandInfo]) { $signtool = $signtool.Source }

$exes = Get-ChildItem (Join-Path $DistDir "*.exe")
if (-not $exes) { Write-Error "Nenhum .exe em $DistDir. Rode build-single.ps1 primeiro." }

foreach ($exe in $exes) {
    Write-Host "Assinando $($exe.Name)..."
    & $signtool sign /fd SHA256 /tr $TimestampUrl /td SHA256 /f $CertPath /p $CertPassword $exe.FullName
    if ($LASTEXITCODE -ne 0) { throw "Falha ao assinar $($exe.Name)" }
    & $signtool verify /pa $exe.FullName
}

Write-Host "Executaveis assinados em $DistDir"
