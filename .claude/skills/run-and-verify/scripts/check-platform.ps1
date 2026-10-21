# Flowza: check that the whole platform starts and every service is ready.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File .claude\skills\run-and-verify\scripts\check-platform.ps1 [-WaitSeconds 60] [-KeepRunning]
#   powershell -ExecutionPolicy Bypass -File .claude\skills\run-and-verify\scripts\check-platform.ps1 -UseRunning
#
# - Default: builds, starts the AppHost (HTTP development profile), checks every service's readiness and then
#   stops ONLY the processes it started itself.
# - If the platform is already running (for example from Visual Studio), it does not touch it: it stops with a
#   message, or with -UseRunning it only checks the running platform's readiness.
# Exits 0 when every service is ready, 1 otherwise. Never prints secrets.
param(
    [int] $WaitSeconds = 60,
    [switch] $KeepRunning,
    [switch] $UseRunning
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
$platformPorts = 15100, 5100, 5101, 5102, 5103, 5104

function Test-PortInUse([int] $port) {
    $client = New-Object Net.Sockets.TcpClient
    try { $client.Connect('localhost', $port); return $true } catch { return $false } finally { $client.Dispose() }
}

function Get-ProcessTree([int] $parentId) {
    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $parentId" -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        Get-ProcessTree $child.ProcessId
        $child.ProcessId
    }
}

function Stop-OwnPlatform([System.Diagnostics.Process] $apphost) {
    # Only the AppHost started by this script and everything below it; never other sessions (e.g. Visual Studio).
    $ids = @(Get-ProcessTree $apphost.Id) + $apphost.Id
    foreach ($id in $ids) { try { Stop-Process -Id $id -Force -ErrorAction Stop } catch { } }
}

function Get-Readiness {
    $results = [ordered]@{}
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
    return $results
}

function Write-Readiness($results) {
    Write-Host ''
    foreach ($name in $results.Keys) {
        $color = if ($results[$name] -like 'ready*') { 'Green' } else { 'Red' }
        Write-Host ("{0,-9} {1}" -f $name, $results[$name]) -ForegroundColor $color
    }
}

$alreadyRunning = $platformPorts | Where-Object { Test-PortInUse $_ }

if ($UseRunning) {
    if (-not $alreadyRunning) { Write-Host 'The platform is not running. Start it (F5 in Visual Studio) or run without -UseRunning.' -ForegroundColor Yellow; exit 1 }
    $results = Get-Readiness
    Write-Readiness $results
    if (-not ($results.Values | Where-Object { $_ -like 'NOT READY*' })) { exit 0 } else { exit 1 }
}

if ($alreadyRunning) {
    Write-Host "The platform (or something on its ports: $($alreadyRunning -join ', ')) is already running, for example from Visual Studio." -ForegroundColor Yellow
    Write-Host 'It is left untouched. Use -UseRunning to check it, or stop it first.' -ForegroundColor Yellow
    exit 1
}

Write-Host 'Building...'
dotnet build Flowza.slnx -v q -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed.' -ForegroundColor Red; exit 1 }

$log = Join-Path ([IO.Path]::GetTempPath()) 'flowza-apphost.log'
Write-Host "Starting the AppHost (log: $log) ..."
$apphost = Start-Process dotnet -ArgumentList 'run', '--project', 'src/AppHost/Flowza.AppHost', '--no-build', '--launch-profile', 'http' `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru -WindowStyle Hidden

$deadline = (Get-Date).AddSeconds($WaitSeconds)
do {
    Start-Sleep -Seconds 5
    $results = Get-Readiness
    $allReady = -not ($results.Values | Where-Object { $_ -like 'NOT READY*' })
} until ($allReady -or (Get-Date) -gt $deadline -or $apphost.HasExited)

Write-Readiness $results

if ($KeepRunning) {
    Write-Host 'The platform keeps running (dashboard: http://localhost:15100).'
}
else {
    Stop-OwnPlatform $apphost
    Write-Host 'Platform stopped (only the processes this script started).'
}

if ($allReady) { exit 0 } else { exit 1 }
