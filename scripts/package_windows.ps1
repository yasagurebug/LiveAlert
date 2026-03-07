param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$PublishDir = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\LiveAlert.Windows\LiveAlert.Windows.csproj"
$installerScript = Join-Path $repoRoot "installer\LiveAlert.Windows.iss"

[xml]$projectXml = Get-Content -Encoding UTF8 $projectPath
$version = ($projectXml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "LiveAlert.Windows.csproj から Version を取得できません。"
}

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "src\LiveAlert.Windows\bin\$Configuration\net8.0-windows\$RuntimeIdentifier\publish"
}

if (-not $SkipPublish) {
    $publishArgs = @(
        "publish",
        $projectPath,
        "-c", $Configuration,
        "-r", $RuntimeIdentifier,
        "--self-contained", "false",
        "-o", $PublishDir,
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish に失敗しました。"
    }
}

if (-not (Test-Path $PublishDir)) {
    throw "publish フォルダが見つかりません: $PublishDir"
}

$isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$isccPath = if ($isccCommand) { $isccCommand.Source } else { $null }
if (-not $isccPath) {
    foreach ($candidate in @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )) {
        if (Test-Path $candidate) {
            $isccPath = $candidate
            break
        }
    }
}

if (-not $isccPath) {
    throw "ISCC.exe が見つかりません。Inno Setup 6 をインストールしてください。"
}

$isccArgs = @(
    "/DAppVersion=$version",
    "/DPublishDir=$PublishDir",
    $installerScript
)

& $isccPath @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup のコンパイルに失敗しました。"
}

Write-Host "Installer created from: $PublishDir"
Write-Host "Output directory: $(Join-Path (Split-Path $installerScript -Parent) 'output')"
Write-Host "If .NET 8 Desktop Runtime is missing, the installer downloads and installs it automatically."
