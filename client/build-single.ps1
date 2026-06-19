# Gera .exe unico portatil (sem instalador, sem ZIP).
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist-build"
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$checksumFile = Join-Path $dist "checksums.txt"
if (Test-Path $checksumFile) { Remove-Item $checksumFile -Force }

# EnableCompressionInSingleFile=false -> evita falha ao abrir no Windows 11
$pubArgs = @(
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=false",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
)

function Publish-App($name, $csproj) {
    $temp = Join-Path $dist "temp-$name"
    if (Test-Path $temp) { Remove-Item -Recurse -Force $temp }
    Write-Host "=== $name ==="
    dotnet publish (Join-Path $root "src\$csproj") @pubArgs -o $temp
    Copy-Item (Join-Path $temp "$name.exe") (Join-Path $dist "$name.exe") -Force
    Remove-Item -Recurse -Force $temp
}

Publish-App "MAMDesk.QuickSupport" "MAMDesk.QuickSupport\MAMDesk.QuickSupport.csproj"
Publish-App "MAMDesk.Operator" "MAMDesk.Operator\MAMDesk.Operator.csproj"

Get-Item (Join-Path $dist "*.exe") | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    "$($_.Name)|$([math]::Round($_.Length/1MB,2)) MB|$hash" | Out-File (Join-Path $dist "checksums.txt") -Append -Encoding utf8
    Write-Host "$($_.Name) $([math]::Round($_.Length/1MB,1)) MB"
}

Write-Host "Pronto: client\dist-build\*.exe"
