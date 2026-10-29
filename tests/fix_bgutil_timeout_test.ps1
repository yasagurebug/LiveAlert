$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$patchScript = Join-Path $repoRoot 'scripts\fix_bgutil_timeout.ps1'
$testDirectory = Join-Path $repoRoot ('.tmp\bgutil-patch-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$module = Join-Path $testDirectory 'getpot_bgutil_script.py'
$encoding = [Text.UTF8Encoding]::new($false)
$fixture = @'
# unrelated content must survive
        stdout, _, returncode = Popen.run(
            [self._jsrt_path, *self._jsrt_args(), script_path, '--version'],
            env=self._jsrt_envs(), timeout=self._GET_SCRIPT_VSN_TIMEOUT,
            text=True, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE)
        stdout = stdout.strip()
'@

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

try {
    foreach ($newline in @("`n", "`r`n")) {
        $original = ($fixture -replace "`r?`n", $newline) + $newline
        [IO.File]::WriteAllText($module, $original, $encoding)
        & $patchScript -ModulePath $module
        $patched = [IO.File]::ReadAllText($module, $encoding)
        Assert-True ($patched.Contains('except subprocess.TimeoutExpired:')) 'Timeout handler was not added.'
        & $patchScript -ModulePath $module
        Assert-True ([IO.File]::ReadAllText($module, $encoding) -ceq $patched) 'Repeated apply changed the module.'
        & $patchScript -ModulePath $module -Restore
        Assert-True ([IO.File]::ReadAllText($module, $encoding) -ceq $original) 'Restore did not preserve original content/newlines.'
        & $patchScript -ModulePath $module -Restore
        Assert-True ([IO.File]::ReadAllText($module, $encoding) -ceq $original) 'Repeated restore changed the module.'
    }

    [IO.File]::WriteAllText($module, 'unsupported source', $encoding)
    foreach ($restore in @($false, $true)) {
        $rejected = $false
        try { & $patchScript -ModulePath $module -Restore:$restore } catch { $rejected = $true }
        Assert-True $rejected 'Unsupported source was accepted.'
        Assert-True ([IO.File]::ReadAllText($module, $encoding) -ceq 'unsupported source') 'Unsupported source was modified.'
    }
    Write-Output 'PASS: LF/CRLF apply, repeated apply, restore, repeated restore, and unsupported-source rejection.'
} finally {
    Remove-Item -LiteralPath $module -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testDirectory -Force
}
