# Gera o executável desktop (um arquivo só) no Windows. Rode na raiz do repositório:
#   powershell -ExecutionPolicy Bypass -File deploy\desktop\publicar.ps1
# Resultado:
#   artifacts\NightCityItemDB.exe               .NET, frontend, catálogo e configuração dentro dele
#   artifacts\NightCityItemDB-win-x64.zip       o .exe + LEIA-ME.txt, pronto para enviar
$ErrorActionPreference = 'Stop'

$root = Resolve-Path "$PSScriptRoot\..\.."
$artifacts = Join-Path $root 'artifacts'
$out = Join-Path $artifacts 'publicar-tmp'
$exe = Join-Path $artifacts 'NightCityItemDB.exe'
$zip = Join-Path $artifacts 'NightCityItemDB-win-x64.zip'
New-Item -ItemType Directory -Force $artifacts | Out-Null

$running = Get-Process NightCityItemDB -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($running) { throw "O $exe está aberto. Feche a janela do programa e rode o script de novo." }

Write-Host '>> Frontend'
Push-Location (Join-Path $root 'frontend')
npm ci --no-audit --no-fund; if ($LASTEXITCODE) { throw 'npm ci falhou' }
npm run build; if ($LASTEXITCODE) { throw 'build do frontend falhou' }
Pop-Location

Write-Host '>> Executável (arquivo único)'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root 'backend\src\CyberpunkDb.Api\CyberpunkDb.Api.csproj') `
  -c Release -r win-x64 -p:DesktopBundle=true -o $out
if ($LASTEXITCODE) { throw 'dotnet publish falhou' }

Copy-Item (Join-Path $out 'NightCityItemDB.exe') $exe -Force
Remove-Item $out -Recurse -Force

Write-Host '>> Pacote .zip'
$stage = Join-Path $artifacts 'zip-tmp'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory $stage | Out-Null
Copy-Item $exe $stage
Copy-Item (Join-Path $PSScriptRoot 'LEIA-ME.txt') $stage
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item $stage -Recurse -Force

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ">> Pronto: $exe ($size MB)"
Write-Host ">>         $zip"
