param()
$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot 'build/GeMMeElec.exe'
$samples = @()
foreach ($count in @(100, 200)) {
    # One fresh hidden process per size; no user project/session is touched.
    $process = Start-Process -FilePath $app -ArgumentList @('--benchmark', "--folios=$count") -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Mesure $count folios échouée. Voir build/benchmark.json." }
    $report = Get-Content -LiteralPath (Join-Path $PSScriptRoot "build/benchmark-$count.json") -Raw | ConvertFrom-Json
    if (!$report.passed) { throw "Rapport $count folios en échec." }
    $samples += $report.samples
}
@{ passed = $true; version = $report.version; logicalProcessors = $report.logicalProcessors; renderer = $report.renderer; isolatedProcessPerSize = $true; samples = $samples } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'build/benchmark.json') -Encoding utf8
Write-Output 'Mesures 100 / 5 000 et 200 / 10 000 disponibles dans build/benchmark.json.'
