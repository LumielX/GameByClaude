# Checks a release zip the way a friend would use it: extract it somewhere new (a path with Thai letters and a
# space on purpose), check what is inside, and run the game's smoke test from there.
# Prints "ZIP OK size=<MB> sha256=<hex>" and exits 0, or explains the problem and exits 1.
param([string]$Zip)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not $Zip) {
    $latest = Get-ChildItem (Join-Path $ProjectPath 'Builds') -Filter 'BOO-TRAYAL-v*-win64.zip' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $latest) { Write-Host 'No zip found: run tools/unity/build-windows.ps1 -Zip first' -ForegroundColor Red; exit 1 }
    $Zip = $latest.FullName
}
if (-not (Test-Path $Zip)) { Write-Host "Zip not found: $Zip" -ForegroundColor Red; exit 1 }

function Fail([string]$why) { Write-Host "ZIP FAILED: $why" -ForegroundColor Red; exit 1 }

$sizeMb = [math]::Round((Get-Item $Zip).Length / 1MB, 1)
# GitHub release assets must stay under 2 GB.
if ($sizeMb -ge 2000) { Fail "zip is $sizeMb MB (GitHub limit 2 GB)" }

# "ทดสอบ BOO" (Thai + space): the game must run from any folder a friend picks.
$folder = [string]::Concat([char]0x0E17, [char]0x0E14, [char]0x0E2A, [char]0x0E2D, [char]0x0E1A) + ' BOO'
$root = Join-Path $env:TEMP (Join-Path $folder ([guid]::NewGuid().ToString('N').Substring(0, 8)))
New-Item -ItemType Directory -Force $root | Out-Null
$ok = $false
try {
    Expand-Archive -Path $Zip -DestinationPath $root -Force
    # OFL.txt: the Kanit font license must travel with every copy of the font (SIL OFL 1.1).
    foreach ($name in @('BOO-TRAYAL.exe', 'README.txt', 'CREDITS.md', 'OFL.txt', 'BOO-TRAYAL_Data', 'UnityPlayer.dll')) {
        if (-not (Test-Path (Join-Path $root $name))) { Fail "$name is missing from the zip root" }
    }
    $noShip = Get-ChildItem $root -Recurse -Directory | Where-Object { $_.Name -match 'DoNotShip|DontShip' }
    if ($noShip) { Fail "debug folder in zip: $($noShip[0].Name)" }

    $log = Join-Path $root 'smoke.log'
    $p = Start-Process -FilePath (Join-Path $root 'BOO-TRAYAL.exe') -ArgumentList @('-batchmode', '-nographics', '-smoketest', '-logFile', "`"$log`"") -PassThru
    if (-not $p.WaitForExit(60000)) { $p.Kill(); Fail 'smoke test timed out (60 s)' }
    if (-not (Select-String -Path $log -Pattern 'BOOTRAYAL_SMOKE_OK' -Quiet)) { Fail "smoke marker missing, see $log" }

    $sha = (Get-FileHash $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "ZIP OK size=$sizeMb sha256=$sha" -ForegroundColor Green
    $ok = $true
}
finally {
    # Keep the folder only when something failed, so the log can be read.
    if ($ok) { Remove-Item (Split-Path $root -Parent) -Recurse -Force -ErrorAction SilentlyContinue }
}
exit 0
