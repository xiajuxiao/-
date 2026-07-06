param(
    [switch]$NoLaunch,
    [switch]$NoDesktopShortcut
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @()
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

function Get-PythonInfo {
    $candidates = @()

    if ($env:ARGOS_PYTHON) {
        $candidates += $env:ARGOS_PYTHON
    }

    foreach ($command in @("python", "python3", "py")) {
        $resolved = Get-Command $command -ErrorAction SilentlyContinue
        if ($resolved) {
            $candidates += $command
        }
    }

    $localPrograms = Join-Path $env:LOCALAPPDATA "Programs\Python"
    if (Test-Path $localPrograms) {
        $candidates += Get-ChildItem -LiteralPath $localPrograms -Recurse -Filter python.exe -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    }

    $candidates += @(
        "$env:ProgramFiles\Python311\python.exe",
        "$env:ProgramFiles\Python312\python.exe",
        "${env:ProgramFiles(x86)}\Python311\python.exe",
        "${env:ProgramFiles(x86)}\Python312\python.exe"
    )

    foreach ($candidate in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
        try {
            $version = & $candidate -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}'); print(sys.executable)" 2>$null
            if ($LASTEXITCODE -eq 0 -and $version.Count -ge 2) {
                $parts = $version[0].Split(".")
                if ([int]$parts[0] -eq 3 -and [int]$parts[1] -ge 10) {
                    return [pscustomobject]@{
                        Command = $candidate
                        Version = $version[0]
                        ExePath = $version[1]
                    }
                }
            }
        }
        catch {
        }
    }

    return $null
}

function Install-Python {
    Write-Step "Python 3.10+ was not found. Installing Python 3.11 with winget"

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "winget was not found. Install Python 3.10+ manually, then run install.ps1 again."
    }

    $args = @(
        "install",
        "--id", "Python.Python.3.11",
        "--exact",
        "--source", "winget",
        "--scope", "user",
        "--accept-package-agreements",
        "--accept-source-agreements"
    )

    try {
        Invoke-External -FilePath $winget.Source -Arguments $args
    }
    catch {
        Write-Host "User-scope Python install failed. Retrying with default scope." -ForegroundColor Yellow
        $args = @(
            "install",
            "--id", "Python.Python.3.11",
            "--exact",
            "--source", "winget",
            "--accept-package-agreements",
            "--accept-source-agreements"
        )
        Invoke-External -FilePath $winget.Source -Arguments $args
    }

    $env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH", "User")
}

function Ensure-Argos {
    param([Parameter(Mandatory = $true)][string]$Python)

    Write-Step "Installing or updating Argos Translate"
    Invoke-External -FilePath $Python -Arguments @("-m", "pip", "install", "--upgrade", "pip")
    Invoke-External -FilePath $Python -Arguments @("-m", "pip", "install", "--upgrade", "argostranslate")

    Write-Step "Checking Argos en->zh and zh->en offline models"

    $installScript = @'
import argostranslate.package
import argostranslate.settings
import argostranslate.translate

argostranslate.settings.chunk_type = argostranslate.settings.ChunkType.MINISBD

pairs = [("en", "zh"), ("zh", "en")]

def has_model(from_code, to_code):
    installed_languages = argostranslate.translate.get_installed_languages()
    from_language = next((lang for lang in installed_languages if lang.code == from_code), None)
    to_language = next((lang for lang in installed_languages if lang.code == to_code), None)
    if from_language is None or to_language is None:
        return False
    try:
        from_language.get_translation(to_language)
        return True
    except Exception:
        return False

print("Updating Argos package index...")
argostranslate.package.update_package_index()
available_packages = argostranslate.package.get_available_packages()

for from_code, to_code in pairs:
    name = f"{from_code}->{to_code}"
    if has_model(from_code, to_code):
        print(f"Argos {name} model already installed.")
        continue

    package = next((pkg for pkg in available_packages if pkg.from_code == from_code and pkg.to_code == to_code), None)
    if package is None:
        raise RuntimeError(f"No Argos {name} package found in package index.")

    print(f"Downloading {package}...")
    package_path = package.download()
    print(f"Installing {package_path}...")
    argostranslate.package.install_from_path(package_path)

    if not has_model(from_code, to_code):
        raise RuntimeError(f"Argos {name} model installation completed but validation failed.")

print("Argos offline models installed.")
'@

    $tempScript = Join-Path $env:TEMP "translator_tool_install_argos_en_zh.py"
    Set-Content -LiteralPath $tempScript -Value $installScript -Encoding UTF8
    Invoke-External -FilePath $Python -Arguments @($tempScript)
}

function Update-AppSettings {
    param([Parameter(Mandatory = $true)][string]$PythonExe)

    $settingsPath = Join-Path $PSScriptRoot "appsettings.json"
    if (-not (Test-Path $settingsPath)) {
        Write-Host "appsettings.json was not found. Skipping settings update." -ForegroundColor Yellow
        return
    }

    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $settings.ArgosPythonPath = $PythonExe
    $settings.EnableOfflineTranslation = $true
    $settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    Write-Host "ArgosPythonPath set to: $PythonExe"
}

function New-DesktopShortcut {
    $exe = Join-Path $PSScriptRoot "TranslatorTool.exe"
    if (-not (Test-Path $exe)) {
        return
    }

    $desktop = [Environment]::GetFolderPath("DesktopDirectory")
    $shortcutPath = Join-Path $desktop "TranslatorTool.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $PSScriptRoot
    $shortcut.IconLocation = $exe
    $shortcut.Save()
    Write-Host "Desktop shortcut created: $shortcutPath"
}

function Start-App {
    $exe = Join-Path $PSScriptRoot "TranslatorTool.exe"
    if (-not (Test-Path $exe)) {
        throw "TranslatorTool.exe was not found. Make sure install.ps1 is in the package root."
    }
    Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot
}

Write-Step "Checking system"
if (-not [Environment]::Is64BitOperatingSystem) {
    throw "This package is win-x64 and requires 64-bit Windows."
}

$pythonInfo = Get-PythonInfo
if (-not $pythonInfo) {
    Install-Python
    $pythonInfo = Get-PythonInfo
}

if (-not $pythonInfo) {
    throw "Python is still unavailable after installation. Open a new PowerShell window and run install.ps1 again."
}

Write-Host "Python: $($pythonInfo.Version) - $($pythonInfo.ExePath)"

Ensure-Argos -Python $pythonInfo.Command
Update-AppSettings -PythonExe $pythonInfo.ExePath

if (-not $NoDesktopShortcut) {
    Write-Step "Creating shortcut"
    New-DesktopShortcut
}

Write-Step "Install complete"
if (-not $NoLaunch) {
    Start-App
    Write-Host "TranslatorTool started."
}
else {
    Write-Host "Launch skipped."
}
