# Two-instance lobby smoke test (local transport, House lobby, real UI on both):
# a not-ready client blocks the host's Start, chat is rate-limited to 1 line per second,
# and a kicked client gets the localized "kicked" popup.
param([int]$TimeoutSec = 60)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $ProjectPath 'Builds\Windows\BOO-TRAYAL.exe'
$dir = Join-Path $ProjectPath 'Builds\net\Lobby'
if (-not (Test-Path $exe)) { Write-Host "Build first: $exe missing" -ForegroundColor Red; exit 1 }
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Force $dir | Out-Null

$procs = New-Object System.Collections.ArrayList

function Start-Instance([string]$name, [string[]]$extra) {
    $log = Join-Path $dir "$name.log"
    $argList = @('-batchmode', '-nographics', '-logFile', "`"$log`"", '-name', $name, '-profile', $name) + $extra
    $p = Start-Process -FilePath $exe -ArgumentList $argList -PassThru
    [void]$procs.Add($p)
    return [pscustomobject]@{ Name = $name; Log = $log; Process = $p }
}

function Wait-Marker($inst, [string]$pattern) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Path $inst.Log) -and (Select-String -Path $inst.Log -Pattern $pattern -Quiet)) {
            $line = (Select-String -Path $inst.Log -Pattern $pattern | Select-Object -Last 1).Line
            Write-Host "  [$($inst.Name)] $line"
            return $line
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timeout waiting for '$pattern' in $($inst.Log)"
}

$ok = $false
try {
    Write-Host '== Lobby smoke =='
    $hostI = Start-Instance 'host' @('-autohost-local', '-smokeprobe', 'lobbyhost')
    Wait-Marker $hostI 'PHASE phase=Lobby' | Out-Null
    $client = Start-Instance 'client' @('-autojoin-local', '-smokeprobe', 'lobbyclient')

    Write-Host '-- a player who is not ready blocks Start'
    Wait-Marker $hostI 'PROBE_LOBBY allReady=False' | Out-Null
    Wait-Marker $hostI 'PROBE_LOBBY phase=Lobby' | Out-Null

    Write-Host '-- two chat lines in one frame: only the first arrives (1 per second)'
    Wait-Marker $client 'PROBE_CHAT_SENT count=2' | Out-Null
    Wait-Marker $hostI 'PROBE_CHAT count=1$' | Out-Null

    Write-Host '-- the host kicks the client, who sees the "kicked" popup'
    Wait-Marker $hostI 'KICK id=1' | Out-Null
    Wait-Marker $client 'reason=kicked' | Out-Null
    Wait-Marker $client 'UI_ERROR key=kicked' | Out-Null

    $bad = Get-ChildItem $dir -Filter *.log |
        Select-String -Pattern 'Exception|\[Netcode\].*[Ee]rror' |
        Where-Object { $_.Line -notmatch 'Disconnect Event' }
    if ($bad) { $bad | Select-Object -First 10 | ForEach-Object { Write-Host "  $($_.Filename): $($_.Line)" }; throw 'exceptions found in logs' }
    $ok = $true
}
catch {
    Write-Host "SMOKE FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Logs: $dir"
}
finally {
    foreach ($p in $procs) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }
}

if ($ok) { Write-Host 'LOBBY SMOKE OK' -ForegroundColor Green; exit 0 }
exit 1
