# Multi-instance night-loop smoke test in the House (local transport), driven by SmokeProbe bots.
#   -Scenario Coop: 2 crew bots close every seal, deliver the ritual items, escape (night 1 crew win),
#                   then idle until time runs out (night 2), game over, back to the lobby.
#   -Scenario Spy:  5 bots. Night 1: Spy sabotages a safe + blacks out, the crew votes it out (correct vote).
#                   Night 2: the crew wrongly votes out the host and revives it; the Spy wins on time.
param([ValidateSet('Coop', 'Spy')][string]$Scenario = 'Coop', [int]$TimeoutSec = 150)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $ProjectPath 'Builds\Windows\BOO-TRAYAL.exe'
$dir = Join-Path $ProjectPath "Builds\net\Night$Scenario"
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

function Lines($inst, [string]$pattern) { return @(Select-String -Path $inst.Log -Pattern $pattern | ForEach-Object { $_.Line }) }

$ok = $false
try {
    Write-Host "== Night smoke: $Scenario =="
    if ($Scenario -eq 'Coop') {
        $common = @('-smokeprobe', 'coop')
        $hostI = Start-Instance 'host' (@('-autohost-local', '-autostart', '2', '-nights', '2', '-nightseconds', '200,15', '-noghost', '-seed', '7') + $common)
        Wait-Marker $hostI 'PHASE phase=Lobby' | Out-Null
        $c1 = Start-Instance 'c1' (@('-autojoin-local') + $common)
        $clients = @($c1)

        Write-Host '-- night 1: seals, ritual, gate, escape'
        $start = Wait-Marker $hostI 'NIGHT_START night=1 '
        if ($start -notmatch 'seals=(\d+)') { throw "no seal count: $start" }
        $seals = [int]$Matches[1]
        for ($i = 0; $i -lt $seals; $i++) { Wait-Marker $hostI 'SEAL_SOLVED' $i | Out-Null }
        Wait-Marker $hostI 'RITUAL_DELIVERED' | Out-Null
        Wait-Marker $hostI 'RITUAL_DELIVERED' 1 | Out-Null
        Wait-Marker $hostI 'GATE_OPEN' | Out-Null
        Wait-Marker $hostI 'PLAYER_ESCAPED' | Out-Null
        Wait-Marker $hostI 'PLAYER_ESCAPED' 1 | Out-Null
        Wait-Marker $hostI 'NIGHT_END night=1 winner=Crew reason=escaped' | Out-Null
        foreach ($a in (Lines $hostI 'AWARD id=')) {
            if ($a -notmatch 'coins=(\d+)' -or [int]$Matches[1] -lt 100) { throw "a crew member earned under 100 coins: $a" }
        }
        Wait-Marker $hostI 'PHASE phase=Summary' | Out-Null
        Wait-Marker $c1 'SUMMARY_RECEIVED night=1' | Out-Null

        Write-Host '-- night 2: nobody acts, time runs out; game over; lobby'
        Wait-Marker $hostI 'NIGHT_END night=2 winner=None reason=time_up' | Out-Null
        Wait-Marker $hostI 'GAME_OVER ranking=' | Out-Null
        Wait-Marker $hostI 'PHASE phase=Lobby' 1 | Out-Null
        foreach ($inst in @($hostI) + $clients) {
            if ((Count-Marker $inst 'ROLE_RECEIVED role=Crew') -lt 2) { throw "$($inst.Name) did not get Crew for both nights" }
            if ((Count-Marker $inst 'ROLE_RECEIVED role=Spy') -gt 0) { throw "$($inst.Name) got a Spy role in co-op" }
        }
    }
    else {
        $common = @('-smokeprobe', 'spybot')
        $hostI = Start-Instance 'host' (@('-autohost-local', '-autostart', '5', '-nights', '2', '-nightseconds', '40,30', '-noghost', '-meetingscale', '0.1', '-seed', '4') + $common)
        Wait-Marker $hostI 'PHASE phase=Lobby' | Out-Null
        $clients = @()
        foreach ($n in 1..4) {
            $clients += Start-Instance "c$n" (@('-autojoin-local') + $common)
            Wait-Marker $clients[-1] 'PLAYER_PLACED' | Out-Null
        }
        $all = @($hostI) + $clients

        Write-Host '-- night 1: roles are private, the Spy sabotages, the crew votes it out'
        Wait-Marker $hostI 'NIGHT_START night=1 ' | Out-Null
        $roles = Wait-Marker $hostI 'NIGHT_ROLES'
        if ($roles -notmatch 'spies=(\d+)') { throw "no spies in: $roles" }
        $spy1 = $Matches[1]
        if ($spy1 -eq '0') { throw 'seed 4 made the host the Spy; the bots need a crew host (pick another seed)' }
        Start-Sleep -Seconds 2
        $spyLogs = @($all | Where-Object { (Count-Marker $_ 'ROLE_RECEIVED role=Spy') -ge 1 })
        if ($spyLogs.Count -ne 1) { throw "expected exactly one Spy client in night 1, got $($spyLogs.Count)" }
        foreach ($inst in $all) {
            $line = (Lines $inst 'ROLE_RECEIVED')[0]
            if ($line -match 'role=Crew' -and $line -notmatch 'partners=$') { throw "$($inst.Name) (crew) got partner ids: $line" }
        }
        Wait-Marker $hostI "POWER_USED by=$spy1 power=SabotagePuzzle counted=True" | Out-Null
        Wait-Marker $hostI "POWER_USED by=$spy1 power=Blackout counted=True" | Out-Null
        Wait-Marker $clients[0] "SPY_TRACE id=$spy1" | Out-Null
        Wait-Marker $clients[0] 'BLACKOUT zone=' | Out-Null
        Wait-Marker $hostI 'MEETING_START caller=0' | Out-Null
        Wait-Marker $hostI 'MEETING_VOTE_OPEN' | Out-Null
        foreach ($inst in $all) { Wait-Marker $inst 'AUDIO_BELL' | Out-Null }
        Wait-Marker $hostI "MEETING_RESULT ejected=$spy1 spy=True" | Out-Null
        Wait-Marker $hostI "PLAYER_MINIGHOST id=$spy1 cause=vote" | Out-Null
        Wait-Marker $hostI 'NIGHT_END night=1 winner=None reason=time_up' | Out-Null

        Write-Host '-- night 2: wrong vote, revive at the shrine, the Spy wins on time'
        Wait-Marker $hostI 'NIGHT_START night=2 ' | Out-Null
        $roles = Wait-Marker $hostI 'NIGHT_ROLES' 1
        if ($roles -notmatch 'spies=(\d+)') { throw "no spies in: $roles" }
        $spy2 = $Matches[1]
        if ($spy2 -eq '0') { throw 'seed 4 made the host the Spy in night 2 (pick another seed)' }
        Wait-Marker $hostI 'MEETING_START caller=0' 1 | Out-Null
        Wait-Marker $hostI 'MEETING_RESULT ejected=0 spy=False' | Out-Null
        Wait-Marker $hostI 'SOUL_SPAWNED id=0' | Out-Null
        Wait-Marker $hostI 'PLAYER_REVIVED id=0' | Out-Null
        Wait-Marker $hostI 'NIGHT_END night=2 winner=Spies reason=time_up' | Out-Null
        $award = (Lines $hostI "AWARD id=$spy2 ")[-1]
        if ($award -notmatch 'coins=(\d+)' -or [int]$Matches[1] -lt 150) { throw "the winning Spy got under 150 coins: $award" }
        Wait-Marker $hostI 'GAME_OVER ranking=' | Out-Null
    }

    Write-Host '-- no exceptions or Netcode errors in any log'
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

if ($ok) { Write-Host 'NIGHT SMOKE OK' -ForegroundColor Green; exit 0 }
exit 1
