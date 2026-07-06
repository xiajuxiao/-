param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$KeepApiKey
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "TranslatorTool\TranslatorTool.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$packageName = "TranslatorTool-$Runtime"
$packageRoot = Join-Path $artifactsRoot $packageName
$zipPath = Join-Path $artifactsRoot "$packageName.zip"

if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -o $packageRoot

$installerSource = Join-Path $PSScriptRoot "install.ps1"
Copy-Item -LiteralPath $installerSource -Destination (Join-Path $packageRoot "install.ps1") -Force

$readme = @"
TranslatorTool one-click package

First install:
1. Extract the whole folder.
2. Right-click install.ps1 and run it with PowerShell.
3. The script checks Python, installs Argos Translate, and installs the en->zh offline model.
4. TranslatorTool.exe starts after installation.

Daily use:
- Double-click TranslatorTool.exe.

Notes:
- This is a self-contained .NET package. The target PC does not need a separate .NET install.
- Offline translation needs Python + Argos Translate. install.ps1 handles them.
- Online AI translation / AI OCR needs an API Key in app settings.
"@
Set-Content -LiteralPath (Join-Path $packageRoot "DEPLOY_README.txt") -Value $readme -Encoding UTF8

$appSettingsPath = Join-Path $packageRoot "appsettings.json"
if ((Test-Path $appSettingsPath) -and -not $KeepApiKey) {
    $settings = Get-Content -LiteralPath $appSettingsPath -Raw | ConvertFrom-Json
    if ($settings.PSObject.Properties.Name -contains "AiApiKey") {
        $settings.AiApiKey = ""
    }
    if ($settings.PSObject.Properties.Name -contains "ArgosPythonPath") {
        $settings.ArgosPythonPath = "python"
    }
    $settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $appSettingsPath -Encoding UTF8
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -Force

Write-Host "Package folder: $packageRoot"
Write-Host "Zip package:    $zipPath"
