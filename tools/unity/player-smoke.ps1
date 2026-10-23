# Multi-instance player smoke test (local transport) driven by SmokeProbe bots.
# host + walker (move, grab, throw) + 2 lifters (heavy urn) + holder (killed while holding).
param([int]$TimeoutSec = 60)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $ProjectPath 'Builds\Windows\BOO-TRAYAL.exe'
$dir = Join-Path $ProjectPath 'Builds\net\Player'
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

function Count-Marker($inst, [string]$pattern) {
    if (-not (Test-Path $inst.Log)) { return 0 }
    return @(Select-String -Path $inst.Log -Pattern $pattern).Count
}

# Waits until the log has more than $after matches of $pattern; returns the newest matching line.
function Wait-Marker($inst, [string]$pattern, [int]$after = 0) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if ((Count-Marker $inst $pattern) -gt $after) {
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
    Write-Host '== Player smoke =='
    $hostI = Start-Instance 'host' @('-autohost-local', '-smokeprobe', 'host', '-playscene', 'Sandbox')
    Wait-Marker $hostI 'PLAYER_PLACED owner=0' | Out-Null

    Write-Host '-- 1. walker moves, grabs a box and throws it (noise)'
    $walker = Start-Instance 'walker' @('-autojoin-local', '-smokeprobe', 'walker')
    Wait-Marker $walker 'PLAYER_PLACED' | Out-Null
    Wait-Marker $hostI 'PLAYER_MOVED id=' | Out-Null
    Wait-Marker $hostI 'ITEM_GRABBED item=box\d by=1' | Out-Null
    $noiseBefore = Count-Marker $hostI 'NOISE '
    Wait-Marker $hostI 'ITEM_RELEASED item=box\d by=1 reason=throw' | Out-Null
    Wait-Marker $hostI 'NOISE ' $noiseBefore | Out-Null
    $line = Wait-Marker $hostI 'PLAYER_CROUCH id=1 crouch=True'
    if ($line -notmatch 'head=0\.95') { throw "host does not see the crouched head height: $line" }

    Write-Host '-- 2. two lifters carry the heavy urn'
    $l1 = Start-Instance 'lifter1' @('-autojoin-local', '-smokeprobe', 'lifter')
    $l2 = Start-Instance 'lifter2' @('-autojoin-local', '-smokeprobe', 'lifter')
    Wait-Marker $hostI 'HEAVY_LIFTED item=urn' | Out-Null

    Write-Host '-- 3. holder disconnects while holding a box'
    $holder = Start-Instance 'holder' @('-autojoin-local', '-smokeprobe', 'holder')
    Wait-Marker $holder 'PROBE_HOLDING' | Out-Null
    $releasesBefore = Count-Marker $hostI 'reason=disconnect'
    Stop-Process -Id $holder.Process.Id -Force
    Wait-Marker $hostI 'ITEM_RELEASED .* reason=disconnect' $releasesBefore | Out-Null

    Write-Host '-- 4. a box carried into a wall stays inside the room'
    $waller = Start-Instance 'waller' @('-autojoin-local', '-smokeprobe', 'waller')
    $line = Wait-Marker $waller 'PROBE_DROPPED item='
    $itemId = ($line -split 'item=')[1].Trim()
    Start-Sleep -Seconds 2
    $state = Select-String -Path $hostI.Log -Pattern "ITEM_STATE item=$itemId " | Select-Object -Last 1
    if (-not $state) { throw "no ITEM_STATE for $itemId after drop" }
    Write-Host "  [host] $($state.Line)"
    if ($state.Line -notmatch 'pos=\(([-\d\.]+),') { throw "cannot parse position: $($state.Line)" }
    $x = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
    if ($x -gt 14.75) { throw "box went through the east wall (x=$x)" }

    Write-Host '-- 5. a player pushes the host; an immediate second push hits the cooldown'
    $pusher = Start-Instance 'pusher' @('-autojoin-local', '-smokeprobe', 'pusher')
    Wait-Marker $hostI 'PUSH by=\d+ target=0' | Out-Null
    Wait-Marker $hostI 'PUSH_DENIED by=\d+ reason=cooldown' | Out-Null

    Write-Host '-- 6. no exceptions or Netcode errors in any log'
    $bad = Get-ChildItem $dir -Filter *.log |
        Select-String -Pattern 'Exception|\[Netcode\].*[Ee]rror|\[Netcode-Server Sender' |
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

if ($ok) { Write-Host 'PLAYER SMOKE OK' -ForegroundColor Green; exit 0 }
exit 1
