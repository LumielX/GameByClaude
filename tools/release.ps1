# Publishes a GitHub pre-release with the Windows zip, after every check that keeps a bad or unsafe release out:
# clean main that matches origin, a new tag, release notes, no secrets in the history, green tests, a zip that runs.
#   tools/release.ps1 -DryRun          everything except publishing (gh login not needed)
#   tools/release.ps1                  publish (needs `gh auth login`)
#   tools/release.ps1 -MakePublic      also switch the repo to Public first (only after the owner agreed)
param(
    [string]$Tag,
    [switch]$DryRun,
    [switch]$SkipBuild,
    [switch]$MakePublic
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$RepoSlug = 'LumielX/GameByClaude'

function Stop-Release([string]$why) { Write-Host "RELEASE REFUSED: $why" -ForegroundColor Red; exit 1 }
function Step([string]$what) { Write-Host "-- $what" }

$versionLine = Select-String -Path (Join-Path $ProjectPath 'Assets\_Project\Scripts\Core\GameVersion.cs') -Pattern 'Current = "([^"]+)"'
if (-not $versionLine) { Stop-Release 'cannot read GameVersion.Current' }
$version = $versionLine.Matches[0].Groups[1].Value
if (-not $Tag) { $Tag = "v$version-alpha" }
$notes = Join-Path $ProjectPath "docs\release\notes-$Tag.md"
$zip = Join-Path $ProjectPath "Builds\BOO-TRAYAL-v$version-win64.zip"
Write-Host "Release $Tag (game v$version)$(if ($DryRun) { ' - dry run' })"

$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh -and (Test-Path 'C:\Program Files\GitHub CLI\gh.exe')) { $gh = 'C:\Program Files\GitHub CLI\gh.exe' }

Step 'GitHub CLI login'
if ($DryRun) { Write-Host '  skipped in a dry run' -ForegroundColor Yellow }
else {
    if (-not $gh) { Stop-Release 'GitHub CLI (gh) is not installed' }
    & $gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Release 'not logged in to GitHub: run `gh auth login` first' }
}

Step 'git state'
Push-Location $ProjectPath
try {
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -ne 'main') { Stop-Release "on branch '$branch'; releases are cut from main" }
    if (git status --porcelain) { Stop-Release 'there are uncommitted changes (git status is not clean)' }
    git fetch -q origin
    if ($LASTEXITCODE -ne 0) { Stop-Release 'git fetch origin failed' }
    $head = (git rev-parse HEAD).Trim()
    $remote = (git rev-parse origin/main).Trim()
    if ($head -ne $remote) { Stop-Release 'main is not the same as origin/main: push (or pull) first' }
    if (git tag --list $Tag) { Stop-Release "tag $Tag already exists locally" }
    if (git ls-remote --tags origin "refs/tags/$Tag") { Stop-Release "tag $Tag already exists on GitHub" }
    if (-not (Test-Path $notes)) { Stop-Release "release notes missing: docs/release/notes-$Tag.md" }

    Step 'secret scan (tracked files + full history)'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ProjectPath 'tools\check-secrets.ps1')
    if ($LASTEXITCODE -ne 0) { Stop-Release 'the secret scan found something: fix it before going public' }

    if (-not $SkipBuild) {
        Step 'EditMode tests'
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ProjectPath 'tools\unity\run-tests.ps1')
        if ($LASTEXITCODE -ne 0) { Stop-Release 'tests failed' }
        Step 'Windows build + zip'
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ProjectPath 'tools\unity\build-windows.ps1') -Zip
        if ($LASTEXITCODE -ne 0) { Stop-Release 'the build failed' }
        # The builders must not have changed anything: the zip has to match the commit being tagged.
        if (git status --porcelain) { Stop-Release 'the build changed tracked files; commit them and run again' }
    }
    if (-not (Test-Path $zip)) { Stop-Release "zip missing: $zip (run without -SkipBuild)" }

    Step 'zip check (extract + smoke test)'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ProjectPath 'tools\unity\verify-zip.ps1') -Zip $zip
    if ($LASTEXITCODE -ne 0) { Stop-Release 'the zip check failed' }

    $title = "BOO-TRAYAL $Tag"
    $createArgs = @('release', 'create', $Tag, $zip, '--repo', $RepoSlug, '--prerelease', '--target', 'main', '--title', $title, '--notes-file', $notes)
    if ($DryRun) {
        if ($MakePublic) { Write-Host "  would run: gh repo edit $RepoSlug --visibility public --accept-visibility-change-consequences" }
        Write-Host "  would run: gh $($createArgs -join ' ')"
        Write-Host 'RELEASE DRY RUN OK' -ForegroundColor Green
        exit 0
    }

    if ($MakePublic) {
        Step "make $RepoSlug public"
        & $gh repo edit $RepoSlug --visibility public --accept-visibility-change-consequences
        if ($LASTEXITCODE -ne 0) { Stop-Release 'could not make the repo public' }
    }
    Step 'publish'
    & $gh @createArgs
    if ($LASTEXITCODE -ne 0) { Stop-Release 'gh release create failed' }
    $url = (& $gh release view $Tag --repo $RepoSlug --json url --jq .url).Trim()
    Write-Host "RELEASED $url" -ForegroundColor Green
}
finally { Pop-Location }
exit 0
