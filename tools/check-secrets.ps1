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
$VivoxSettings = 'ProjectSettings/Packages/com.unity.services.vivox/Settings.json'
$BinaryOk = '\.(png|jpe?g|gif|tga|psd|exr|hdr|fbx|blend|ttf|otf|wav|ogg|mp3|asset)$'
$BadNames ='(\.pem|\.p12|\.pfx|\.keystore)$|serviceaccount[^/]*\.json$|credentials[^/]*\.json$|(^|/)\.env$'

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
    # Vivox test-mode credentials: Unity writes the key name and its value on different lines, so parse the JSON.
    foreach ($c in @(git -C $repo log --all --format=%h -- $VivoxSettings 2>$null)) {
        if (-not (git -C $repo ls-tree --name-only $c -- $VivoxSettings)) { continue } # deleted in this commit
        $json = (git -C $repo show "${c}:$VivoxSettings") -join "`n" | ConvertFrom-Json
        foreach ($entry in @($json.m_Dictionary.m_DictionaryValues)) {
            if ($entry.key -ne 'tokenKey') { continue }
            if (($entry.value | ConvertFrom-Json).m_Value) { $hits.Add("SECRET $c ${VivoxSettings}: Vivox token key") }
        }
    }
    # Binary blobs are invisible to grep (a UTF-16 file from PowerShell `>` looks binary): only known media may be binary.
    $commit = $null
    foreach ($line in @(git -C $repo -c core.quotePath=false log --all --numstat --format=%h 2>$null)) {
        if ($line -match '^[0-9a-f]{7,}$') { $commit = $line; continue }
        if ($line -match '^-\t-\t(.+)$' -and $Matches[1] -notmatch $BinaryOk) { $hits.Add("SECRET $commit $($Matches[1]): binary file git cannot scan") }
    }
    $paths = @(git -C $repo -c core.quotePath=false log --all --format= --name-only 2>$null) | Where-Object { $_ } | Sort-Object -Unique
    foreach ($p in $paths) { if ($p -match $BadNames) { $hits.Add("SECRET history ${p}: secret-looking file name") } }
    return @($hits | Sort-Object -Unique)
}

function VivoxSettings([string]$key) {
    $entry = '            {{
                "type": "System.String",
                "key": "{0}",
                "value": "{{\"m_Value\":\"{1}\"}}"
            }}'
    return "{`n    `"m_Dictionary`": {`n        `"m_DictionaryValues`": [`n" +
        (($entry -f 'tokenKey', $key), ($entry -f 'tokenIssuer', 'x-1') -join ",`n").Replace('\n', "`n") +
        "`n        ]`n    }`n}"
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

        # Vivox test-mode credentials, in the exact multi-line format Unity writes (key and value on separate lines).
        $vivoxDir = Join-Path $dirty 'ProjectSettings\Packages\com.unity.services.vivox'
        New-Item -ItemType Directory -Force $vivoxDir | Out-Null
        Set-Content -Path (Join-Path $vivoxDir 'Settings.json') -Value (VivoxSettings ('Zx' * 12)) -Encoding ASCII
        git -C $dirty add -A
        git -C $dirty commit -q -m 'vivox test mode'
        $vivox = (git -C $dirty rev-parse --short HEAD).Trim()

        # A token saved by PowerShell 5.1 `>` is UTF-16: git sees a binary file and cannot grep it.
        Set-Content -Path (Join-Path $dirty 'tok.txt') -Value ('ghp_' + ('a' * 36)) -Encoding Unicode
        git -C $dirty add -A
        git -C $dirty commit -q -m 'token'
        $utf16 = (git -C $dirty rev-parse --short HEAD).Trim()

        # The clean repo holds what this project really has: an empty Vivox key and binary images.
        $cleanVivox = Join-Path $clean 'ProjectSettings\Packages\com.unity.services.vivox'
        New-Item -ItemType Directory -Force $cleanVivox | Out-Null
        Set-Content -Path (Join-Path $cleanVivox 'Settings.json') -Value (VivoxSettings '') -Encoding ASCII
        [IO.File]::WriteAllBytes((Join-Path $clean 'shot.png'), [byte[]](0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 1, 2))
        git -C $clean add -A
        git -C $clean commit -q -m 'settings and image'

        $dirtyHits = @(Find-Secrets $dirty)
        $cleanHits = @(Find-Secrets $clean)
        $expected = @(
            "^SECRET $leak config\.txt: AWS key$",
            "^SECRET $vivox ProjectSettings/Packages/com\.unity\.services\.vivox/Settings\.json: Vivox token key$",
            "^SECRET $utf16 tok\.txt: binary file git cannot scan$"
        )
        foreach ($pattern in $expected) {
            if (-not ($dirtyHits | Where-Object { $_ -match $pattern })) {
                Write-Host "SELFTEST FAILED: no hit matching $pattern (got: $($dirtyHits -join '; '))" -ForegroundColor Red
                return $false
            }
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
