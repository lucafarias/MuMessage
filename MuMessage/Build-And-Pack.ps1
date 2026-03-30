param(
    [string]$OutputDir = "", 
    [switch]$IncrementVersion
)

Write-Host "=== MU-MESSAGE PACKAGE BUILDER ===" -ForegroundColor Cyan

# 1. DETERMINA DIRECTORY OUTPUT
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = "$PSScriptRoot\..\..\Packages"
}
if (!(Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
Write-Host "Target Directory: $OutputDir" -ForegroundColor Yellow

# 2. FILE PER LA VERSIONE E CSPROJ
$versionFile = "$PSScriptRoot\build-version.json"
$csprojPath = "$PSScriptRoot\MuMessage.csproj"

# 3. LEGGI VERSIONE ATTUALE DAL JSON
if (Test-Path $versionFile) {
    $versionData = Get-Content $versionFile | ConvertFrom-Json
    $currentVersion = $versionData.Version
    $buildCount = $versionData.BuildCount + 1
} else {
    # Se il file non esiste, partiamo da una versione valida
    $currentVersion = "1.0.0"
    $buildCount = 1
}

# 4. INCREMENTA VERSIONE (Assicuriamoci che non sia vuota o malformata)
if ($IncrementVersion) {
    $parts = $currentVersion -split '\.'
    if ($parts.Count -lt 3) { $parts = "1","0","0" }
    $patch = [int]$parts[2] + 1
    $newVersion = "$($parts[0]).$($parts[1]).$patch"
} else {
    $newVersion = $currentVersion
}

Write-Host "Processing Version: $newVersion" -ForegroundColor Green

# 5. SALVA NUOVA VERSIONE NEL JSON
$newVersionData = @{
    Version = $newVersion
    BuildCount = $buildCount
    LastBuild = [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss")
}
$newVersionData | ConvertTo-Json | Set-Content $versionFile

# 6. AGGIORNA CSPROJ (Uso Regex più sicura)
$csprojContent = Get-Content $csprojPath -Raw
$csprojContent = $csprojContent -replace '<Version>[\s\S]*?<\/Version>', "<Version>$newVersion</Version>"
[System.IO.File]::WriteAllText($csprojPath, $csprojContent)

# 7. RIPRISTINO E PULIZIA CRITICA
Write-Host "`nRestore e Clean in corso..." -ForegroundColor Green
dotnet restore
dotnet clean --configuration Release

# 8. COMPILAZIONE
Write-Host "Compilazione Release..." -ForegroundColor Green
dotnet build --configuration Release --no-restore /p:Version=$newVersion

# 9. CREA PACCHETTO
Write-Host "Creazione pacchetto NuGet..." -ForegroundColor Green
dotnet pack `
    --configuration Release `
    --output $OutputDir `
    --no-build `
    -p:PackageOutputPath="$OutputDir" `
    -p:Version=$newVersion `
    --include-symbols

Write-Host "`n=== COMPLETATO: MuMessage $newVersion ===" -ForegroundColor Cyan