param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDirectory
$PluginId = "__PLUGIN_ID__"
$ProjectName = "PluginTemplate"
$ArtifactsDirectory = Join-Path $ProjectRoot "artifacts"
$PublishDirectory = Join-Path $ArtifactsDirectory "publish"
$RuntimeDirectory = Join-Path $ArtifactsDirectory $PluginId
$ZipPath = Join-Path $ArtifactsDirectory ($PluginId + ".zip")

if (Test-Path $PublishDirectory) {
    Remove-Item $PublishDirectory -Recurse -Force
}

if (Test-Path $RuntimeDirectory) {
    Remove-Item $RuntimeDirectory -Recurse -Force
}

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

dotnet publish (Join-Path $ProjectRoot ($ProjectName + ".csproj")) -c $Configuration -o $PublishDirectory

New-Item -ItemType Directory -Path (Join-Path $RuntimeDirectory "bin") -Force | Out-Null
Copy-Item (Join-Path $ProjectRoot "manifest.json") (Join-Path $RuntimeDirectory "manifest.json")
Copy-Item (Join-Path $PublishDirectory "*") (Join-Path $RuntimeDirectory "bin") -Recurse

foreach ($Directory in @("data", "scripts", "assets")) {
    $SourceDirectory = Join-Path $ProjectRoot $Directory

    if (Test-Path $SourceDirectory) {
        $TargetDirectory = Join-Path $RuntimeDirectory $Directory
        New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null
        Copy-Item (Join-Path $SourceDirectory "*") $TargetDirectory -Recurse -Force
    }
}

Compress-Archive -Path $RuntimeDirectory -DestinationPath $ZipPath -Force

Write-Host "Created plugin runtime directory at $RuntimeDirectory"
Write-Host "Created plugin archive at $ZipPath"
