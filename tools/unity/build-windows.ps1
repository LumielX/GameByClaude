# Applies settings, rebuilds scenes, builds Windows x64, then runs the smoke test.
param(
    [switch]$SkipSmoke,
    [switch]$Zip
)
. (Join-Path $PSScriptRoot 'UnityCommon.ps1')
Assert-EditorClosed

$methods = @(
    'BooTrayal.Editor.Builders.ProjectSetupBuilder.Apply',
    'BooTrayal.Editor.Builders.SceneBuilder.BuildBootstrap',
    'BooTrayal.Editor.BuildPipeline.WindowsBuild.Build'
)
foreach ($m in $methods) {
    $short = ($m -split '\.')[-2..-1] -join '.'
    $r = Invoke-Unity -UnityArgs @('-quit', '-executeMethod', $m) -LogName "build-$short.log"
    $problems = Test-UnityLog $r.Log
    if ($r.ExitCode -ne 0 -or $problems.Count -gt 0) {
        Write-Problems $problems $r.Log
        Write-Host "$m FAILED (exit $($r.ExitCode))" -ForegroundColor Red
        exit 1
    }
    Write-Host "$m OK"
}

$exe = Join-Path $ProjectPath 'Builds\Windows\BOO-TRAYAL.exe'
if (-not (Test-Path $exe)) { Write-Host "Missing $exe" -ForegroundColor Red; exit 1 }
Write-Host "Built $exe"

if (-not $SkipSmoke) {
    $smokeLog = Join-Path $ProjectPath 'Builds\smoke.log'
    if (Test-Path $smokeLog) { Remove-Item $smokeLog -Force }
    $p = Start-Process -FilePath $exe -ArgumentList @('-batchmode', '-nographics', '-smoketest', '-logFile', "`"$smokeLog`"") -PassThru
    if (-not $p.WaitForExit(60000)) { $p.Kill(); Write-Host 'Smoke test timed out' -ForegroundColor Red; exit 1 }
    if (-not (Select-String -Path $smokeLog -Pattern 'BOOTRAYAL_SMOKE_OK' -Quiet)) {
        Write-Host "Smoke marker missing, see $smokeLog" -ForegroundColor Red
        exit 1
    }
    Write-Host 'SMOKE OK' -ForegroundColor Green
}

if ($Zip) {
    $version = (Select-String -Path (Join-Path $ProjectPath 'ProjectSettings\ProjectSettings.asset') -Pattern '^\s*bundleVersion:\s*(\S+)').Matches[0].Groups[1].Value
    $zipPath = Join-Path $ProjectPath "Builds\BOO-TRAYAL-v$version-win64.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $staging = Join-Path $ProjectPath 'Builds\Windows'
    # Exclude the debug-symbols folder Unity writes next to the player.
    $items = Get-ChildItem $staging | Where-Object { $_.Name -notlike '*_BurstDebugInformation_DoNotShip' -and $_.Name -notlike '*_BackUpThisFolder_ButDontShipItWithYourGame' }
    Compress-Archive -Path $items.FullName -DestinationPath $zipPath
    Write-Host "Zipped $zipPath"
}
