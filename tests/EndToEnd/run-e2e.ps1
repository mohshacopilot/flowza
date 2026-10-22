<#
.SYNOPSIS
  Runs Flowza's end-to-end tests (T029; tech discussion TE5) against the whole platform on the test databases.

.DESCRIPTION
  1. Builds the solution (unless -NoBuild).
  2. Empties the flowza_test_* databases (never the development ones).
  3. Starts the AppHost on those databases, as on a new installation, and waits until everything answers.
  4. Loads the demo users with the real `flowza demo-data` command.
  5. Runs the Playwright tests (extra arguments go to Playwright, e.g. -PlaywrightArgs '--headed').
  6. Stops the platform again (unless -KeepRunning).
  The development AppHost uses the same ports: stop it first.
  Windows (PowerShell 5.1 or 7):  powershell -NoProfile -ExecutionPolicy Bypass -File tests\EndToEnd\run-e2e.ps1
  CI on Linux (PowerShell 7):     pwsh -NoProfile -File tests/EndToEnd/run-e2e.ps1
#>
param(
    [switch] $NoBuild,
    [switch] $KeepRunning,
    [string[]] $PlaywrightArgs = @()
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$e2e = $PSScriptRoot
$setupProject = Join-Path $root 'tests/Flowza.EndToEnd.Setup'
$appHostProject = Join-Path $root 'src/AppHost/Flowza.AppHost'
$gatewayUrl = 'http://localhost:5100'

# HttpClient instead of Invoke-WebRequest: the same in Windows PowerShell 5.1 and PowerShell 7, no redirects followed.
Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$http = New-Object System.Net.Http.HttpClient($handler)
$http.Timeout = [TimeSpan]::FromSeconds(5)

# The HTTP status of a GET, or 0 when nothing answers.
function Test-Url([string] $url) {
    try {
        $response = $http.GetAsync($url).GetAwaiter().GetResult()
        return [int] $response.StatusCode
    } catch {
        return 0
    }
}

# In GitHub Actions a failure also becomes an annotation on the run, readable without opening the full log.
function Write-CiError([string] $title, [string] $text) {
    if (-not $env:GITHUB_ACTIONS) { return }
    $escaped = $text.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
    Write-Host "::error title=$title::$escaped"
}

function Get-FileTail([string] $path, [int] $lines = 40) {
    if (Test-Path $path) { return ((Get-Content $path -Tail $lines) -join "`n") }
    return '(no file)'
}

# What each part of the platform answers now: the readiness answer names the failing check (database, transport).
function Get-PlatformState {
    $parts = foreach ($target in @(
            @{ Name = 'gateway'; Url = "$gatewayUrl/health/ready" },
            @{ Name = 'identity'; Url = 'http://localhost:5101/health/ready' },
            @{ Name = 'design'; Url = 'http://localhost:5102/health/ready' },
            @{ Name = 'runtime'; Url = 'http://localhost:5103/health/ready' },
            @{ Name = 'worker'; Url = 'http://localhost:5104/health/ready' },
            @{ Name = 'web'; Url = 'http://localhost:5105/' })) {
        try {
            $response = $http.GetAsync($target.Url).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ($body.Length -gt 400) { $body = $body.Substring(0, 400) }
            "$($target.Name): $([int] $response.StatusCode) $body"
        } catch {
            "$($target.Name): no answer"
        }
    }
    return ($parts -join "`n")
}

function Wait-Url([string] $name, [string] $url, [int[]] $expected, [int] $seconds = 240) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Url $url) -in $expected) {
            Write-Host "  $name is up"
            return
        }
        Start-Sleep -Seconds 2
    }
    throw "$name did not answer at $url within $seconds seconds."
}

function Invoke-Checked([string] $what, [scriptblock] $command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)." }
}

if ((Test-Url $gatewayUrl) -ne 0) {
    throw "Something already answers at $gatewayUrl (a development AppHost?). Stop it first: the end-to-end run needs the same ports."
}

# The tests read the SQL password from FLOWZA_Sql__Password in CI; the AppHost reads Sql:Password.
if ($env:FLOWZA_Sql__Password -and -not $env:Sql__Password) { $env:Sql__Password = $env:FLOWZA_Sql__Password }

trap {
    Write-CiError 'End-to-end preparation failed' $_.ToString()
    break
}

if (-not $NoBuild) {
    Write-Host '1. Building the solution'
    Invoke-Checked 'Build' { dotnet build (Join-Path $root 'Flowza.slnx') -v q --nologo }
}

Write-Host '2. Emptying the flowza_test_* databases'
Invoke-Checked 'Reset' { dotnet run --project $setupProject --no-build -- reset }

Write-Host '3. Starting the platform on the test databases'
$env:Databases__Prefix = 'flowza_test_'
$env:Gateway__LoginPermitLimit = '1000'
$env:Smtp4dev__Enabled = 'false'
$log = Join-Path $e2e 'test-results/apphost.log'
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
$appHost = Start-Process -FilePath 'dotnet' -PassThru -NoNewWindow `
    -ArgumentList @('run', '--project', $appHostProject, '--no-build', '--launch-profile', 'http') `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err"

$exitCode = 1
try {
    Wait-Url 'Identity' 'http://localhost:5101/health/ready' @(200)
    Wait-Url 'Web app' 'http://localhost:5105/' @(200)
    Wait-Url 'Gateway' "$gatewayUrl/health/ready" @(200)

    Write-Host '4. Loading the demo data'
    Invoke-Checked 'Demo data' { dotnet run --project $setupProject --no-build -- demo-data }

    Write-Host '5. Running the end-to-end tests'
    Push-Location $e2e
    try {
        if (-not (Test-Path 'node_modules')) { Invoke-Checked 'npm ci' { npm ci --no-fund --no-audit } }
        npx playwright test @PlaywrightArgs
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }
} catch {
    Write-CiError 'End-to-end run stopped' ($_.ToString() + "`n`n" + (Get-PlatformState))
    throw
} finally {
    if ($exitCode -ne 0) {
        Write-CiError 'AppHost output (end)' ((Get-FileTail $log) + "`n--- errors ---`n" + (Get-FileTail "$log.err"))
    }
    if ($KeepRunning) {
        Write-Host "The platform keeps running on the test databases (process $($appHost.Id)); stop it with Ctrl+C in its window or Stop-Process."
    } else {
        Write-Host '6. Stopping the platform'
        Stop-Process -Id $appHost.Id -ErrorAction SilentlyContinue
        $appHost.WaitForExit(30000) | Out-Null
        Get-Process -Name 'Flowza.*' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

exit $exitCode
