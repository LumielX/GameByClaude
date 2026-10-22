# Runs a static Editor method via -executeMethod.
param(
    [Parameter(Mandatory)][string]$Method,
    [string[]]$ExtraArgs = @()
)
. (Join-Path $PSScriptRoot 'UnityCommon.ps1')
Assert-EditorClosed

$short = ($Method -split '\.')[-2..-1] -join '.'
$r = Invoke-Unity -UnityArgs (@('-quit', '-executeMethod', $Method) + $ExtraArgs) -LogName "method-$short.log"
$problems = Test-UnityLog $r.Log
if ($r.ExitCode -ne 0 -or $problems.Count -gt 0) {
    Write-Problems $problems $r.Log
    Write-Host "$Method FAILED (exit $($r.ExitCode))" -ForegroundColor Red
    exit 1
}
Write-Host "$Method OK" -ForegroundColor Green
