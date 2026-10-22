# Gera o executável desktop (um arquivo só) no Windows. Rode na raiz do repositório:
#   powershell -ExecutionPolicy Bypass -File deploy\desktop\publicar.ps1 [-Versao 1.3.0] [-Repositorio dono/repo]
# Resultado:
#   artifacts\NightCityItemDB.exe               .NET, frontend, catálogo e configuração dentro dele
#   artifacts\NightCityItemDB.exe.sha256        hash conferido pela atualização automática
#   artifacts\NightCityItemDB-win-x64.zip       o .exe + LEIA-ME.txt, pronto para enviar
# As releases oficiais saem pelo GitHub Actions (.github/workflows/release.yml), que passa os dois parâmetros.
# -Repositorio é o repo do GitHub de onde o programa busca atualizações; sem ele, a atualização automática fica desligada.
param(
  [string]$Versao = '0.0.0-dev',
  [string]$Repositorio = ''
)
$ErrorActionPreference = 'Stop'

$root = Resolve-Path "$PSScriptRoot\..\.."
$artifacts = Join-Path $root 'artifacts'
$out = Join-Path $artifacts 'publicar-tmp'
$exe = Join-Path $artifacts 'NightCityItemDB.exe'
$zip = Join-Path $artifacts 'NightCityItemDB-win-x64.zip'
$hash = "$exe.sha256"
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
  -c Release -r win-x64 -p:DesktopBundle=true "-p:Version=$Versao" "-p:UpdatesRepository=$Repositorio" -o $out
if ($LASTEXITCODE) { throw 'dotnet publish falhou' }

Copy-Item (Join-Path $out 'NightCityItemDB.exe') $exe -Force
Remove-Item $out -Recurse -Force
$sha = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($hash, "$sha  NightCityItemDB.exe`n")

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
Write-Host ">> Pronto: $exe ($size MB, versão $Versao)"
Write-Host ">>         $zip"
