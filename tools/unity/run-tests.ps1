# Runs Unity Test Framework tests and summarizes the NUnit XML.
param(
    [ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode',
    [string]$Filter
)
. (Join-Path $PSScriptRoot 'UnityCommon.ps1')
Assert-EditorClosed

$resultsDir = Join-Path $ProjectPath 'TestResults'
New-Item -ItemType Directory -Force $resultsDir | Out-Null
$xml = Join-Path $resultsDir "$Platform.xml"
if (Test-Path $xml) { Remove-Item $xml -Force }

$unityArgs = @('-runTests', '-testPlatform', $Platform, '-testResults', "`"$xml`"")
if ($Filter) { $unityArgs += @('-testFilter', "`"$Filter`"") }
$r = Invoke-Unity -UnityArgs $unityArgs -LogName "tests-$Platform.log"

if (-not (Test-Path $xml)) {
    Write-Problems (Test-UnityLog $r.Log) $r.Log
    Write-Host "No test results produced (exit $($r.ExitCode)) - likely a compile error." -ForegroundColor Red
    exit 1
}

[xml]$doc = Get-Content $xml -Raw
$run = $doc.'test-run'
Write-Host "Tests $Platform : total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped)"
$failed = $doc.SelectNodes("//test-case[@result='Failed']")
foreach ($tc in $failed) {
    Write-Host "  FAIL $($tc.fullname)" -ForegroundColor Red
    $node = $tc.SelectSingleNode('failure/message')
    $msg = if ($node) { $node.InnerText } else { $null }
    if ($msg) { Write-Host "       $($msg.Trim())" }
}
if ([int]$run.failed -gt 0 -or [int]$run.total -eq 0) { exit 1 }
Write-Host 'Tests OK' -ForegroundColor Green
