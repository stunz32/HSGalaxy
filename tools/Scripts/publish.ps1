param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'

Push-Location (Split-Path -Parent $MyInvocation.MyCommand.Path)
try {
    $repoRoot = Resolve-Path "$PSScriptRoot\..\.." | Select-Object -ExpandProperty Path
    Set-Location $repoRoot

    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $temp = Join-Path $env:TEMP 'HSGalaxy'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    $distRoot = Join-Path $temp ("dist_" + $stamp)
    New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

    $scFlag = [bool]$SelfContained

    $appOut = Join-Path $distRoot 'HSGalaxy.App'
    $cliOut = Join-Path $distRoot 'HSGalaxy.CLI'
    New-Item -ItemType Directory -Force -Path $appOut, $cliOut | Out-Null

    Write-Host "Publishing App -> $appOut"
    $scSwitch = if ($scFlag) { '--self-contained' } else { '--no-self-contained' }
    dotnet publish 'src/HSGalaxy.App' -c $Configuration -r $Runtime $scSwitch -o $appOut | Out-Host

    Write-Host "Publishing CLI -> $cliOut"
    dotnet publish 'src/HSGalaxy.CLI' -c $Configuration -r $Runtime $scSwitch -o $cliOut | Out-Host

    $suffix = if ($scFlag) { '_sc' } else { '_fd' }
    $zipApp = Join-Path $distRoot ("HSGalaxy.App_${Configuration}_${Runtime}${suffix}.zip")
    $zipCli = Join-Path $distRoot ("HSGalaxy.CLI_${Configuration}_${Runtime}${suffix}.zip")

    if (Test-Path $zipApp) { Remove-Item -Force $zipApp }
    if (Test-Path $zipCli) { Remove-Item -Force $zipCli }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($appOut, $zipApp)
    [System.IO.Compression.ZipFile]::CreateFromDirectory($cliOut, $zipCli)

    $appSize = (Get-Item $zipApp).Length
    $cliSize = (Get-Item $zipCli).Length

    Write-Host ("App ZIP:   " + $zipApp + "  (" + [math]::Round($appSize/1MB,2) + " MB)")
    Write-Host ("CLI ZIP:   " + $zipCli + "  (" + [math]::Round($cliSize/1MB,2) + " MB)")

    # Optionally copy evidence bundle/report into dist root if present
    $evidenceZip = Join-Path $temp (Get-ChildItem -Path $temp -Filter 'evidence_*.zip' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { $_.Name })
    $evidenceHtml = Join-Path $temp (Get-ChildItem -Path $temp -Filter 'report_evidence_*.html' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { $_.Name })
    if (Test-Path $evidenceZip) { Copy-Item $evidenceZip -Destination (Join-Path $distRoot (Split-Path $evidenceZip -Leaf)) -Force }
    if (Test-Path $evidenceHtml) { Copy-Item $evidenceHtml -Destination (Join-Path $distRoot (Split-Path $evidenceHtml -Leaf)) -Force }

    Write-Host ("DIST ROOT: " + $distRoot)
}
finally {
    Pop-Location
}
