# UI smoke test: one player instance with -uismoke opens every screen and overlay in Thai and English
# (menus, error, lobby + chat + ready, HUD, pause, vote, summary, ranking) through a real Solo game.
#   -Shots: run windowed instead of batchmode and save PNGs of every screen into docs/ui_screens/.
param([switch]$Shots, [int]$TimeoutSec = 180)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $ProjectPath 'Builds\Windows\BOO-TRAYAL.exe'
$dir = Join-Path $ProjectPath 'Builds\net\UI'
if (-not (Test-Path $exe)) { Write-Host "Build first: $exe missing" -ForegroundColor Red; exit 1 }
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Force $dir | Out-Null
$log = Join-Path $dir 'ui.log'

$game = @('-uismoke', '-nights', '2', '-nightseconds', '30,8', '-meetingscale', '0.1', '-noghost', '-profile', 'uismoke', '-name', 'Pim')
if ($Shots) {
    $shotDir = Join-Path $ProjectPath 'docs\ui_screens'
    New-Item -ItemType Directory -Force $shotDir | Out-Null
    $argList = @('-logFile', "`"$log`"", '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-uishots', "`"$shotDir`"") + $game
}
else {
    $argList = @('-batchmode', '-nographics', '-logFile', "`"$log`"") + $game
}

$p = Start-Process -FilePath $exe -ArgumentList $argList -PassThru
$ok = $false
try {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline -and -not $p.HasExited) {
        if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'UI_SMOKE_OK|UI_SMOKE_TIMEOUT' -Quiet)) { break }
        Start-Sleep -Milliseconds 500
    }
    Start-Sleep -Seconds 1
    $lines = @(Select-String -Path $log -Encoding UTF8 -Pattern 'UI_SCREEN|UI_CHAT|UI_READY|UI_VOTE|UI_SMOKE|UI_CURSOR|UI_BADCODE' | ForEach-Object { $_.Line })
    $lines | ForEach-Object { Write-Host "  $_" }
    if (-not ($lines -match 'UI_SMOKE_OK')) { throw 'UI_SMOKE_OK missing' }
    if ($lines -match 'UI_SMOKE_TIMEOUT') { throw 'a UI step timed out' }
    foreach ($screen in 'title', 'play', 'create', 'settings', 'howto', 'error', 'lobby', 'hud', 'pause', 'vote', 'summary', 'ranking') {
        foreach ($lang in 'Thai', 'English') {
            if (-not ($lines -match "UI_SCREEN name=$screen lang=$lang visible=True")) { throw "screen $screen ($lang) was not shown" }
        }
    }
    # the Thai greeting "sawasdee" as regex escapes (Windows PowerShell reads this script as ANSI).
    if (-not ($lines -match 'UI_CHAT_OK text=\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35 hello$')) { throw 'chat was not sanitized and delivered as expected' }
    if (-not ($lines -match 'UI_READY allReady=True')) { throw 'ready did not reach the host' }
    if (-not ($lines -match 'UI_BADCODE error=code_invalid')) { throw 'an invalid room code showed no error popup' }
    # The cursor must be free on every menu-like screen and locked only while walking around in the HUD.
    foreach ($c in 'lobby=True', 'hud=False', 'pause=True', 'vote=True', 'summary=True') {
        $screen, $free = $c -split '='
        if (-not ($lines -match "UI_CURSOR screen=$screen free=$free")) { throw "cursor on $screen should be free=$free" }
    }
    $bad = Select-String -Path $log -Pattern 'Exception|\[Netcode\].*[Ee]rror' | Where-Object { $_.Line -notmatch 'Disconnect Event' }
    if ($bad) { $bad | Select-Object -First 10 | ForEach-Object { Write-Host "  $($_.Line)" }; throw 'exceptions found in the log' }
    $ok = $true
}
catch {
    Write-Host "UI SMOKE FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Log: $log"
}
finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}

if ($ok) { Write-Host 'UI SMOKE OK' -ForegroundColor Green; exit 0 }
exit 1
