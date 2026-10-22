# Two-instance network smoke test on one PC using the built player.
#   -Mode Local : plain NGO on 127.0.0.1 (no Unity Services)
#   -Mode Relay : Unity Multiplayer Sessions (Lobby + Relay) with a real join code
param(
    [ValidateSet('Local', 'Relay')][string]$Mode = 'Local',
    [int]$TimeoutSec = 60
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $ProjectPath 'Builds\Windows\BOO-TRAYAL.exe'
$dir = Join-Path $ProjectPath "Builds\net\$Mode"
if (-not (Test-Path $exe)) { Write-Host "Build first: $exe missing" -ForegroundColor Red; exit 1 }
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Force $dir | Out-Null

$procs = New-Object System.Collections.ArrayList

function Start-Instance([string]$name, [string[]]$extra) {
    $log = Join-Path $dir "$name.log"
    $argList = @('-batchmode', '-nographics', '-logFile', "`"$log`"", '-name', $name) + $extra
    $p = Start-Process -FilePath $exe -ArgumentList $argList -PassThru
    [void]$procs.Add($p)
    return [pscustomobject]@{ Name = $name; Log = $log; Process = $p }
}

function Wait-Marker($inst, [string]$pattern) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Path $inst.Log) -and (Select-String -Path $inst.Log -Pattern $pattern -Quiet)) {
            $line = (Select-String -Path $inst.Log -Pattern $pattern | Select-Object -First 1).Line
            Write-Host "  [$($inst.Name)] $line"
            return $line
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timeout waiting for '$pattern' in $($inst.Log)"
}

$ok = $false
try {
    Write-Host "== $Mode smoke =="
    if ($Mode -eq 'Local') {
        $host1 = Start-Instance 'host' @('-autohost-local')
        Wait-Marker $host1 'NET_HOSTING' | Out-Null
        $joinArgs = @('-autojoin-local')
    } else {
        $codeFile = Join-Path $dir 'code.txt'
        $host1 = Start-Instance 'host' @('-autohost', '-profile', 'smokehost', '-codefile', "`"$codeFile`"")
        $line = Wait-Marker $host1 'NET_HOSTING|NET_FAILED'
        if ($line -match 'NET_FAILED') { throw "Host failed: $line" }
        $code = (Get-Content $codeFile -Raw).Trim()
        Write-Host "  room code: $code"
        $joinArgs = @('-autojoin', $code)
    }

    Write-Host '-- 1. matching client joins'
    $c1 = Start-Instance 'client1' ($joinArgs + @('-profile', 'smokec1'))
    $line = Wait-Marker $c1 'NET_CONNECTED|NET_FAILED|NET_REJECTED'
    if ($line -notmatch 'NET_CONNECTED') { throw "client1 did not connect: $line" }
    Wait-Marker $host1 'NET_CLIENT_JOINED' | Out-Null

    Write-Host '-- 2. old-version client is refused'
    $c2 = Start-Instance 'client_old' ($joinArgs + @('-profile', 'smokec2', '-fakeversion', '0.0.9'))
    $line = Wait-Marker $c2 'NET_CONNECTED|NET_FAILED|NET_REJECTED'
    if ($line -notmatch 'NET_REJECTED reason=version_mismatch') { throw "expected version_mismatch, got: $line" }

    Start-Sleep -Seconds 2
    if (Select-String -Path $host1.Log -Pattern 'NET_CLIENT_LEFT' -Quiet) { throw 'host logged NET_CLIENT_LEFT for a refused client' }
    if (Select-String -Path $c1.Log -Pattern 'NET_HOST_LOST' -Quiet) { throw 'client1 lost the host before the host was stopped' }

    Write-Host '-- 3. host quits, client returns to menu'
    Stop-Process -Id $host1.Process.Id -Force
    $line = Wait-Marker $c1 'NET_HOST_LOST|NET_FAILED'
    if ($line -notmatch 'NET_HOST_LOST') { throw "expected NET_HOST_LOST, got: $line" }

    $ok = $true
}
catch {
    Write-Host "SMOKE FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Logs: $dir"
}
finally {
    foreach ($p in $procs) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }
}

if ($ok) { Write-Host "$($Mode.ToUpper()) SMOKE OK" -ForegroundColor Green; exit 0 }
exit 1
