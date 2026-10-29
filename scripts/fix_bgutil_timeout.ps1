param(
    [Parameter(Mandatory = $true)]
    [string]$ModulePath
)

$ErrorActionPreference = 'Stop'
$resolvedPath = (Resolve-Path -LiteralPath $ModulePath).Path
if ([IO.Path]::GetFileName($resolvedPath) -ne 'getpot_bgutil_script.py') {
    throw 'Expected getpot_bgutil_script.py.'
}
$encoding = [Text.UTF8Encoding]::new($false, $true)
$source = [IO.File]::ReadAllText($resolvedPath, $encoding)
$newline = if ($source.Contains("`r`n")) { "`r`n" } else { "`n" }
$original = @'
        stdout, _, returncode = Popen.run(
            [self._jsrt_path, *self._jsrt_args(), script_path, '--version'],
            env=self._jsrt_envs(), timeout=self._GET_SCRIPT_VSN_TIMEOUT,
            text=True, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE)
'@ -replace "`r?`n", $newline
$replacement = @'
        try:
            stdout, _, returncode = Popen.run(
                [self._jsrt_path, *self._jsrt_args(), script_path, '--version'],
                env=self._jsrt_envs(), timeout=self._GET_SCRIPT_VSN_TIMEOUT,
                text=True, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE)
        except subprocess.TimeoutExpired:
            self.logger.warning(
                f'{self._JSRT_NAME} provider version check timed out; '
                'trying the next available provider', once=True)
            return False
'@ -replace "`r?`n", $newline

if ($source.Contains($replacement)) {
    Write-Output 'bgutil timeout fix is already applied.'
    return
}
if (($source.Split(@($original), [StringSplitOptions]::None).Count - 1) -ne 1) {
    throw 'Unsupported bgutil source; no changes were made.'
}
[IO.File]::WriteAllText($resolvedPath, $source.Replace($original, $replacement), $encoding)
Write-Output "Applied bgutil timeout fix: $resolvedPath"
