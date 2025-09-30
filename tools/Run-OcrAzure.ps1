param(
    [switch]$Release
)

Write-Host "Running Azure OCR test..." -ForegroundColor Cyan

if (-not $env:HSGALAXY_AZURE_VISION_ENDPOINT -or -not $env:HSGALAXY_AZURE_VISION_KEY) {
    Write-Host "Azure env vars missing. Run:" -ForegroundColor Yellow
    Write-Host "  .\\tools\\Set-HSGalaxyAzureEnv.ps1 -Endpoint 'https://<name>.cognitiveservices.azure.com' [-Persist]" -ForegroundColor Yellow
    exit 1
}

$config = if ($Release) { 'Release' } else { 'Debug' }

dotnet build .\HSGalaxyArena.sln -c $config -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project .\src\HSGalaxy.CLI\HSGalaxy.CLI.csproj -c $config -- ocr:azure
exit $LASTEXITCODE

