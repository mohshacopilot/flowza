# Flowza: start the whole platform (AppHost), check every service's readiness, then stop it.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File .claude\skills\run-and-verify\scripts\check-platform.ps1 [-WaitSeconds 60] [-KeepRunning]
#
# Prints one line per service with its readiness checks (database, messaging transport, broker) and exits with 0
# when every service is ready, 1 otherwise. Never prints secrets.
param(
    [int] $WaitSeconds = 60,
    [switch] $KeepRunning
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
Set-Location $root

$services = [ordered]@{
    identity = 'http://localhost:5101/health/ready'
    design   = 'http://localhost:5102/health/ready'
    runtime  = 'http://localhost:5103/health/ready'
    worker   = 'http://localhost:5104/health/ready'
    gateway  = 'http://localhost:5100/health/ready'
}

function Stop-Platform {
    Get-Process | Where-Object { $_.ProcessName -like 'Flowza*' -or $_.ProcessName -like 'dcp*' -or $_.ProcessName -like 'Rnwood*' } |
        ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch { } }
}

Write-Host 'Building...'
dotnet build Flowza.slnx -v q -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed.' -ForegroundColor Red; exit 1 }

Stop-Platform
$log = Join-Path ([IO.Path]::GetTempPath()) 'flowza-apphost.log'
Write-Host "Starting the AppHost (log: $log) ..."
$apphost = Start-Process dotnet -ArgumentList 'run', '--project', 'src/AppHost/Flowza.AppHost', '--no-build', '--launch-profile', 'https' `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru -WindowStyle Hidden

$deadline = (Get-Date).AddSeconds($WaitSeconds)
$results = @{}
do {
    Start-Sleep -Seconds 5
    foreach ($name in $services.Keys) {
        try {
            $response = Invoke-WebRequest -Uri $services[$name] -UseBasicParsing -TimeoutSec 5
            $checks = ($response.Content | ConvertFrom-Json).checks.PSObject.Properties | ForEach-Object {
                $transport = $_.Value.data.transport
                "$($_.Name)=$($_.Value.status)$(if ($transport) { "($transport)" })"
            }
            $results[$name] = "ready  " + ($checks -join ' ')
        }
        catch { $results[$name] = "NOT READY  $($_.Exception.Message)" }
    }
    $allReady = -not ($results.Values | Where-Object { $_ -like 'NOT READY*' })
} until ($allReady -or (Get-Date) -gt $deadline -or $apphost.HasExited)

Write-Host ''
foreach ($name in $services.Keys) {
    $color = if ($results[$name] -like 'ready*') { 'Green' } else { 'Red' }
    Write-Host ("{0,-9} {1}" -f $name, $results[$name]) -ForegroundColor $color
}

if ($KeepRunning) {
    Write-Host 'The platform keeps running; stop it with Ctrl+C in Visual Studio or by closing the AppHost.'
}
else {
    Stop-Platform
    Write-Host 'Platform stopped.'
}

if ($allReady) { exit 0 } else { exit 1 }
