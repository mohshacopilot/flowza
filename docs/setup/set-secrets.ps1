# Flowza: store the development passwords on this PC (owner steps O2 and O4).
#
# Asks for the SQL password of the "flowza_app" login (typing is hidden), stores it in
# .NET user secrets (store "flowza-dev", in your Windows profile, never in git), checks that the SQL password
# works, and installs smtp4dev if it is missing. Passwords are never printed.
#
# Run from the project folder:
#   powershell -ExecutionPolicy Bypass -File docs\setup\set-secrets.ps1

$ErrorActionPreference = 'Stop'

function Read-Secret([string] $prompt) {
    $secure = Read-Host -Prompt $prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

Write-Host ''
Write-Host 'Flowza password setup' -ForegroundColor Cyan
Write-Host 'Type each password and press Enter. Nothing appears on screen while you type; that is normal.'
Write-Host ''

# 1. SQL Server
while ($true) {
    $sqlPassword = Read-Secret 'Password of the SQL login flowza_app'
    & sqlcmd -S localhost -U flowza_app -P $sqlPassword -C -l 10 -b -Q 'SELECT 1' *> $null
    if ($LASTEXITCODE -eq 0) { Write-Host '  SQL password works.' -ForegroundColor Green; break }
    Write-Host '  That password did not work for flowza_app on localhost. Please try again.' -ForegroundColor Yellow
}
dotnet user-secrets set 'Sql:Password' $sqlPassword --id flowza-dev | Out-Null
$sqlPassword = $null

# 2. smtp4dev (development mail catcher)
if (Test-Path (Join-Path $env:USERPROFILE '.dotnet\tools\smtp4dev.exe')) {
    Write-Host '  smtp4dev is already installed.' -ForegroundColor Green
}
else {
    Write-Host '  Installing smtp4dev ...'
    dotnet tool install -g Rnwood.Smtp4dev | Out-Null
    Write-Host '  smtp4dev installed.' -ForegroundColor Green
}

# 3. Summary (key names only)
$file = Join-Path $env:APPDATA 'Microsoft\UserSecrets\flowza-dev\secrets.json'
$keys = (Get-Content $file -Raw | ConvertFrom-Json).PSObject.Properties.Name -join ', '
Write-Host ''
Write-Host "Done. Stored: $keys" -ForegroundColor Cyan
Write-Host 'You can close this tab and tell Claude "done".'
