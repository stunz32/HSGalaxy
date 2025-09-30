param(
    [Parameter(Mandatory=$true)]
    [string]$Endpoint,
    [switch]$Persist
)

Write-Host "Setting HSGalaxy Azure Vision environment variables..." -ForegroundColor Cyan

if (-not ($Endpoint -match '^https?://')) {
    Write-Error "-Endpoint must be a valid URL (e.g., https://xxxx.cognitiveservices.azure.com)"
    exit 1
}

$key = Read-Host -AsSecureString -Prompt "Enter Azure Vision Key (will not echo)"
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($key)
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

if ([string]::IsNullOrWhiteSpace($plain)) {
    Write-Error "Key cannot be empty"
    exit 1
}

if ($Persist) {
    # User-level persistence for new shells
    [Environment]::SetEnvironmentVariable('HSGALAXY_AZURE_VISION_ENDPOINT', $Endpoint, 'User')
    [Environment]::SetEnvironmentVariable('HSGALAXY_AZURE_VISION_KEY', $plain, 'User')
    Write-Host "Saved to user environment. Open a NEW PowerShell to use them." -ForegroundColor Yellow
}

# Also set for current process so this shell can use immediately
$env:HSGALAXY_AZURE_VISION_ENDPOINT = $Endpoint
$env:HSGALAXY_AZURE_VISION_KEY = $plain

Write-Host "Endpoint: $($env:HSGALAXY_AZURE_VISION_ENDPOINT)" -ForegroundColor Green
Write-Host "Key length: $($env:HSGALAXY_AZURE_VISION_KEY.Length)" -ForegroundColor Green
Write-Host "Done." -ForegroundColor Green

