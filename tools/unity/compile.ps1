# Opens the project in batchmode and fails on compile errors.
. (Join-Path $PSScriptRoot 'UnityCommon.ps1')
Assert-EditorClosed

$r = Invoke-Unity -UnityArgs @('-quit') -LogName 'compile.log'
$problems = Test-UnityLog $r.Log
if ($r.ExitCode -ne 0 -or $problems.Count -gt 0) {
    Write-Problems $problems $r.Log
    Write-Host "Compile FAILED (exit $($r.ExitCode))" -ForegroundColor Red
    exit 1
}
Write-Host 'Compile OK' -ForegroundColor Green
