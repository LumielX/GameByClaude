# Shared helpers for running Unity in batchmode. Dot-source this file.
$ErrorActionPreference = 'Stop'

$UnityExe    = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$LogDir      = Join-Path $ProjectPath 'Logs'

function Assert-EditorClosed {
    # Unity Hub ships its own helper named unity.exe; only real Editors block batchmode.
    $editors = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath -notmatch 'Unity Hub' }
    if ($editors) {
        Write-Host 'Close the Unity Editor first (batchmode cannot open a project the Editor holds).' -ForegroundColor Red
        exit 2
    }
}

function Invoke-Unity {
    param([string[]]$UnityArgs, [string]$LogName)
    New-Item -ItemType Directory -Force $LogDir | Out-Null
    $log = Join-Path $LogDir $LogName
    if (Test-Path $log) { Remove-Item $log -Force }
    $all = @('-batchmode', '-nographics', '-projectPath', "`"$ProjectPath`"", '-logFile', "`"$log`"") + $UnityArgs
    $proc = Start-Process -FilePath $UnityExe -ArgumentList $all -PassThru -Wait -NoNewWindow
    return [pscustomobject]@{ ExitCode = $proc.ExitCode; Log = $log }
}

function Test-UnityLog {
    param([string]$LogPath)
    if (-not (Test-Path $LogPath)) { return @("Log not found: $LogPath") }
    $patterns = @('error CS\d+', '^\s*[\w\.]*Exception:', 'Scripts have compiler errors', 'executeMethod method .* threw exception')
    $hits = Select-String -Path $LogPath -Pattern $patterns
    return @($hits | ForEach-Object { $_.Line.Trim() } | Select-Object -Unique)
}

function Write-Problems {
    param([string[]]$Problems, [string]$LogPath)
    Write-Host "Problems in $LogPath :" -ForegroundColor Red
    $Problems | Select-Object -First 40 | ForEach-Object { Write-Host "  $_" }
}
