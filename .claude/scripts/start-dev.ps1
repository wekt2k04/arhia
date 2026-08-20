<#
Demarre l'environnement de dev complet en une seule commande : Docker Desktop (si besoin),
conteneurs agirh-sql/agirh-qdrant (jamais recrees), Ollama natif, Api .NET (profil Maison) et
frontend Next.js - chacun dans sa propre fenetre PowerShell (hot-reload conserve, aucune donnee
touchee, mode dev identique a un demarrage manuel). Idempotent : ne relance pas ce qui tourne
deja. A lancer depuis n'importe quel repertoire :
    powershell -File .claude\scripts\start-dev.ps1
#>

$ErrorActionPreference = "Stop"

# $PSScriptRoot = .claude/scripts -> 2 niveaux vers le haut pour atteindre la racine du depot
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apiDir = Join-Path $root "src\Agirh.Api"
$frontendDir = Join-Path $root "frontend"

function Test-DockerReady {
    docker info *> $null
    return ($LASTEXITCODE -eq 0)
}

function Test-PortOpen {
    param([string]$ComputerName, [int]$Port)
    try {
        return (Test-NetConnection -ComputerName $ComputerName -Port $Port -WarningAction SilentlyContinue -InformationLevel Quiet)
    } catch {
        return $false
    }
}

function Start-InNewWindow {
    param([string]$Title, [string]$WorkingDirectory, [string]$Command)
    $inner = "`$host.UI.RawUI.WindowTitle = '$Title'; "
    if ($WorkingDirectory) {
        $inner += "Set-Location -LiteralPath `"$WorkingDirectory`"; "
    }
    $inner += $Command
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $inner
}

# --- 1. Docker Desktop ---
Write-Host "Verification de Docker Desktop..."
$dockerReady = Test-DockerReady

if (-not $dockerReady) {
    $dockerExe = "C:\Program Files\Docker\Docker\Docker Desktop.exe"
    if (Test-Path $dockerExe) {
        Write-Host "Docker Desktop n'est pas lance -> demarrage automatique (peut prendre ~1 min)..."
        Start-Process $dockerExe
        $elapsed = 0
        while (-not $dockerReady -and $elapsed -lt 90) {
            Start-Sleep -Seconds 3
            $elapsed += 3
            $dockerReady = Test-DockerReady
        }
    }
}

if (-not $dockerReady) {
    Write-Host "Docker Desktop ne repond toujours pas. Demarre-le manuellement puis relance ce script." -ForegroundColor Red
    exit 1
}

Write-Host "Docker pret. Demarrage des conteneurs agirh-sql / agirh-qdrant (jamais recrees)..."
docker start agirh-sql agirh-qdrant

# --- 2. Ollama (natif, jamais conteneurise) ---
Write-Host "Verification d'Ollama (port 11434)..."
if (Test-PortOpen -ComputerName "localhost" -Port 11434) {
    Write-Host "Ollama deja actif, rien a faire."
} else {
    Write-Host "Ollama non detecte -> lancement dans une nouvelle fenetre..."
    Start-InNewWindow -Title "AGIRH - Ollama" -Command "ollama serve"
    Start-Sleep -Seconds 3
}

# --- 3. Api (.NET, profil Maison = Ollama local, cf. launchSettings.json) ---
Write-Host "Lancement de l'Api (dotnet run --launch-profile Maison)..."
Start-InNewWindow -Title "AGIRH - Api (.NET)" -WorkingDirectory $apiDir -Command "dotnet run --launch-profile Maison"

# --- 4. Frontend (Next.js) ---
Write-Host "Lancement du frontend (npm run dev)..."
Start-InNewWindow -Title "AGIRH - Frontend (Next.js)" -WorkingDirectory $frontendDir -Command "npm run dev"

Write-Host ""
Write-Host "3 fenetres ouvertes (Ollama si besoin, Api, Frontend) + celle-ci pour Docker."
Write-Host "Ouverture du navigateur dans 12s (le temps que l'Api et le frontend demarrent)..."
Start-Sleep -Seconds 12
Start-Process "http://localhost:3000"
