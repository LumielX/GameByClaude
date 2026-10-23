# Looks for secrets in the tracked files AND in every commit of the history: once the repo is public, a key that
# was committed and later deleted is still readable. Prints one line per hit (never the value itself):
#   SECRET <commit|HEAD> <path>: <what>
# and exits 1, or prints "SECRETS OK commits=<n>" and exits 0.
# -SelfTest builds two throwaway repos (one with a planted fake key, one clean) and checks both verdicts.
param([string]$Repo, [switch]$SelfTest)
$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }

# POSIX extended regexes (git grep -E / log -G). No double quotes inside: PowerShell 5.1 mangles them for native tools.
$Patterns = [ordered]@{
    'private key'     = '-----BEGIN [A-Z ]*PRIVATE KEY-----'
    'AWS key'         = 'AKIA[0-9A-Z]{16}'
    'GitHub token'    = 'gh[pousr]_[A-Za-z0-9]{36}'
    'Google API key'  = 'AIza[0-9A-Za-z_-]{35}'
    'Vivox token key' = 'tokenKey.?[[:space:]]*:[[:space:]]*.[A-Za-z0-9+/=_-]'
    # Need a real value after the name, so docs and this script that only mention the words do not count.
    'service account' = 'private_key_id.{0,4}:[[:space:]]*.[0-9a-f]{40}'
    'client secret'   = 'client_secret.{0,3}[:=][[:space:]]*.?[A-Za-z0-9_-]{16,}'
}
$BadNames = '(\.pem|\.p12|\.pfx|\.keystore)$|serviceaccount[^/]*\.json$|credentials[^/]*\.json$|(^|/)\.env$'

function Find-Secrets([string]$repo) {
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($kv in $Patterns.GetEnumerator()) {
        foreach ($path in @(git -C $repo grep -lIE -e $kv.Value 2>$null)) { $hits.Add("SECRET HEAD ${path}: $($kv.Key)") }
    }
    # Every commit whose diff adds or removes a match, then which files hold it in that commit.
    $any = @($Patterns.Values) -join '|'
    foreach ($c in @(git -C $repo log --all --format=%h -E "-G$any" 2>$null)) {
        foreach ($kv in $Patterns.GetEnumerator()) {
            foreach ($line in @(git -C $repo grep -lIE -e $kv.Value $c 2>$null)) {
                $hits.Add("SECRET $c $($line.Substring($c.Length + 1)): $($kv.Key)") # git grep prints "<rev>:<path>"
            }
        }
    }
    $paths = @(git -C $repo log --all --format= --name-only 2>$null) | Where-Object { $_ } | Sort-Object -Unique
    foreach ($p in $paths) { if ($p -match $BadNames) { $hits.Add("SECRET history ${p}: secret-looking file name") } }
    return @($hits | Sort-Object -Unique)
}

function Invoke-SelfTest {
    $base = Join-Path $env:TEMP ('bt-secrets-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $dirty = Join-Path $base 'dirty'
    $clean = Join-Path $base 'clean'
    try {
        foreach ($d in @($dirty, $clean)) {
            New-Item -ItemType Directory -Force $d | Out-Null
            git -C $d init -q
            git -C $d config user.email 'selftest@example.invalid'
            git -C $d config user.name 'selftest'
            Set-Content -Path (Join-Path $d 'readme.txt') -Value 'nothing to see' -Encoding ASCII
            git -C $d add -A
            git -C $d commit -q -m 'clean start'
        }
        # A fake AWS-style key, committed and then deleted: only the history still has it.
        Set-Content -Path (Join-Path $dirty 'config.txt') -Value ('aws = AKIA' + ('Q' * 16)) -Encoding ASCII
        git -C $dirty add -A
        git -C $dirty commit -q -m 'oops'
        $leak = (git -C $dirty rev-parse --short HEAD).Trim()
        git -C $dirty rm -q config.txt
        git -C $dirty commit -q -m 'remove it'

        $dirtyHits = @(Find-Secrets $dirty)
        $cleanHits = @(Find-Secrets $clean)
        if (-not ($dirtyHits | Where-Object { $_ -match "^SECRET $leak config\.txt: AWS key$" })) {
            Write-Host "SELFTEST FAILED: the deleted key in commit $leak was not found (got: $($dirtyHits -join '; '))" -ForegroundColor Red
            return $false
        }
        if ($cleanHits.Count -gt 0) {
            Write-Host "SELFTEST FAILED: the clean repo reported $($cleanHits -join '; ')" -ForegroundColor Red
            return $false
        }
        Write-Host 'SELFTEST OK' -ForegroundColor Green
        return $true
    }
    finally { Remove-Item $base -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($SelfTest) { if (Invoke-SelfTest) { exit 0 } else { exit 1 } }

$hits = @(Find-Secrets $Repo)
if ($hits.Count -gt 0) {
    $hits | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host "SECRETS FOUND: $($hits.Count)" -ForegroundColor Red
    exit 1
}
$commits = (git -C $Repo rev-list --all --count).Trim()
Write-Host "SECRETS OK commits=$commits" -ForegroundColor Green
exit 0
