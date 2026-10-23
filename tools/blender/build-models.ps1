# Builds every model from tools/blender/build_models.py with Blender in background mode.
# Output: Assets/_Project/Art/Models/*.fbx + tris.txt, docs/art_previews/models/*.png. Fails when a model is over budget.
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Blender = 'X:\TheBlender\blender.exe'
if (-not (Test-Path $Blender)) { Write-Host "Blender not found at $Blender" -ForegroundColor Red; exit 1 }
$script = Join-Path $PSScriptRoot 'build_models.py'
$models = Join-Path $ProjectPath 'Assets\_Project\Art\Models'
$previews = Join-Path $ProjectPath 'docs\art_previews\models'
$log = Join-Path $ProjectPath 'Logs\blender-models.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null

# Blender prints deprecation warnings on stderr; Windows PowerShell would turn them into terminating errors.
$ErrorActionPreference = 'Continue'
& $Blender --background --factory-startup --python $script -- $models $previews *> $log
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
Select-String -Path $log -Pattern 'MODEL_BUILT|MODELS_OK|MODELS_OVER_BUDGET|Error|Traceback' | ForEach-Object { Write-Host "  $($_.Line)" }
if ($code -ne 0 -or -not (Select-String -Path $log -Pattern 'MODELS_OK' -Quiet)) {
    Write-Host "Blender model build FAILED (exit $code). Log: $log" -ForegroundColor Red
    exit 1
}
Write-Host 'MODELS OK' -ForegroundColor Green
