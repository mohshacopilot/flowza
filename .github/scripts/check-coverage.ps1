# Flowza coverage gate (T031, PLT-09-AC1, constitution 5.6): line coverage of the Domain and Application assemblies,
# merged over all test projects by ReportGenerator (JsonSummary), must be at least the minimum (80 %).
# Fails too when no coverage was collected at all, so a broken coverage step can never pass the gate by accident.
#
#   pwsh .github/scripts/check-coverage.ps1 -Summary coverage/Summary.json -Minimum 80
param(
    [Parameter(Mandatory)] [string] $Summary,
    [double] $Minimum = 80
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Summary)) {
    Write-Error "Coverage summary '$Summary' not found: no coverage was collected."
}

$report = Get-Content $Summary -Raw | ConvertFrom-Json
$coverable = [int] $report.summary.coverablelines
if ($coverable -eq 0) {
    Write-Error 'No Domain or Application lines were measured: coverage collection is not working.'
}

$total = [double] $report.summary.linecoverage
$lines = @("| Assembly | Line coverage |", "|---|---|")
foreach ($assembly in $report.coverage.assemblies) {
    $lines += "| $($assembly.name) | $($assembly.coverage) % |"
}
$lines += "| **Total (gate: $Minimum %)** | **$total %** |"
$lines | ForEach-Object { Write-Host $_ }

if ($env:GITHUB_STEP_SUMMARY) {
    @('## Coverage gate (Domain + Application)', '') + $lines | Add-Content -Path $env:GITHUB_STEP_SUMMARY
}

if ($total -lt $Minimum) {
    Write-Error "Line coverage $total % is below the required $Minimum % (constitution 5.6)."
}

Write-Host "Coverage gate passed: $total % >= $Minimum %."
