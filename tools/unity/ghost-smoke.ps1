# Multi-instance ghost smoke test in the House scene (local transport), driven by SmokeProbe bots.
# host (-ghostwait: the Krasue holds still until it hears or sees someone) + victim (sneaks behind it and screams)
# + lighter (shines a flashlight in its face during the cooldown).
param([int]$TimeoutSec = 60)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $ProjectPath 'Builds\Windows\BOO-TRAYAL.exe'
$dir = Join-Path $ProjectPath 'Builds\net\Ghost'
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

# The t= value (server Time.time) of a marker line.
function Time-Of([string]$line) {
    if ($line -notmatch ' t=([-\d\.]+)') { throw "no t= in: $line" }
    return [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
}

$ok = $false
try {
    Write-Host '== Ghost smoke =='
    $hostI = Start-Instance 'host' @('-autohost-local', '-smokeprobe', 'host', '-ghostwait')
    Wait-Marker $hostI 'GHOST_SPAWNED' | Out-Null

    Write-Host '-- 1. victim sneaks behind the ghost and screams: heard, 1 s warning, chase, grab, death'
    $victim = Start-Instance 'victim' @('-autojoin-local', '-smokeprobe', 'victim')
    Wait-Marker $victim 'PLAYER_PLACED' | Out-Null
    $lighter = Start-Instance 'lighter' @('-autojoin-local', '-smokeprobe', 'lighter')
    Wait-Marker $lighter 'PLAYER_PLACED' | Out-Null

    $heard = Time-Of (Wait-Marker $hostI 'GHOST_HEARD')
    $alert = Time-Of (Wait-Marker $hostI 'GHOST_STATE state=Alert target=1 ')
    if ($alert -lt $heard) { throw "ghost spotted the victim before hearing the scream (alert t=$alert, heard t=$heard)" }
    $chase = Time-Of (Wait-Marker $hostI 'GHOST_STATE state=Chase target=1 ')
    if ($chase - $alert -lt 0.95) { throw "chase started $($chase - $alert) s after the warning (need >= 1 s)" }
    $grab = Time-Of (Wait-Marker $hostI 'GHOST_GRAB target=1 ')
    $killed = Time-Of (Wait-Marker $hostI 'PLAYER_KILLED id=1 ')
    if ($killed - $grab -lt 1.9) { throw "victim died $($killed - $grab) s after the grab (need 2 s)" }
    Wait-Marker $hostI 'GHOST_STATE state=Cooldown' | Out-Null

    Write-Host '-- 2. the victim felt it coming (client-side fear + adrenaline from the synced ghost)'
    Wait-Marker $victim 'FEAR band=near' | Out-Null
    Wait-Marker $victim 'PLAYER_ADRENALINE state=Adrenaline' | Out-Null
    Wait-Marker $hostI 'PLAYER_RESPAWNED id=1' | Out-Null

    Write-Host '-- 3. a flashlight held on its face makes it flee'
    Wait-Marker $hostI 'GHOST_STATE state=Flee' | Out-Null
    $fleeLines = @(Select-String -Path $hostI.Log -Pattern 'GHOST_STATE state=Flee')
    $wanderAfter = Select-String -Path $hostI.Log -Pattern 'GHOST_STATE state=Wander' | Where-Object { $_.LineNumber -gt $fleeLines[0].LineNumber }
    $deadline = (Get-Date).AddSeconds(10)
    while (-not $wanderAfter -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $wanderAfter = Select-String -Path $hostI.Log -Pattern 'GHOST_STATE state=Wander' | Where-Object { $_.LineNumber -gt $fleeLines[0].LineNumber }
    }
    if (-not $wanderAfter) { throw 'ghost never returned to Wander after fleeing' }
    Write-Host "  [host] $(@($wanderAfter)[0].Line)"

    Write-Host '-- 4. no exceptions or Netcode errors in any log'
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

if ($ok) { Write-Host 'GHOST SMOKE OK' -ForegroundColor Green; exit 0 }
exit 1
