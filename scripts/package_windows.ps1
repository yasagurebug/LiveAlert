param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$PublishDir = '',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\LiveAlert.Windows\LiveAlert.Windows.csproj'
$installerScript = Join-Path $repoRoot 'installer\LiveAlert.Windows.iss'

[xml]$projectXml = Get-Content -Encoding UTF8 $projectPath
$versionNode = $projectXml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
$version = if ($versionNode) { [string]$versionNode.Version } else { '' }
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Could not read <Version> from LiveAlert.Windows.csproj.'
}

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $publishRelativePath = 'src\LiveAlert.Windows\bin\{0}\net8.0-windows\{1}\publish' -f $Configuration, $RuntimeIdentifier
    $PublishDir = Join-Path $repoRoot $publishRelativePath
}

if (-not $SkipPublish) {
    $publishArgs = @(
        'publish'
        $projectPath
        '-c'
        $Configuration
        '-r'
        $RuntimeIdentifier
        '--self-contained'
        'false'
        '-o'
        $PublishDir
        '-p:DebugType=None'
        '-p:DebugSymbols=false'
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet publish failed.'
    }
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory not found: $PublishDir"
}

$isccPath = $null
$isccCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
if ($isccCommand) {
    $isccPath = $isccCommand.Source
}

if (-not $isccPath) {
    foreach ($candidate in @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )) {
        if (Test-Path $candidate) {
            $isccPath = $candidate
            break
        }
    }
}

if (-not $isccPath) {
    throw 'ISCC.exe was not found. Install Inno Setup 6.'
}

$isccArgs = @(
    "/DAppVersion=$version"
    "/DPublishDir=$PublishDir"
    $installerScript
)

& $isccPath @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Inno Setup compilation failed.'
}

Write-Host "Installer created from: $PublishDir"
Write-Host "Output directory: $(Join-Path (Split-Path $installerScript -Parent) 'output')"
Write-Host 'If .NET 8 Desktop Runtime is missing, the installer downloads and installs it automatically.'
