$ErrorActionPreference = "Stop"

$python = if ($env:ARGOS_PYTHON) { $env:ARGOS_PYTHON } else { "python" }

Write-Host "Using Python: $python"
& $python -m pip install --upgrade pip
& $python -m pip install --upgrade argostranslate

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

$tempScript = Join-Path $env:TEMP "install_argos_en_zh.py"
Set-Content -LiteralPath $tempScript -Value $installScript -Encoding UTF8
& $python $tempScript
