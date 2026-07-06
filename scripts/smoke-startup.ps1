param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath
)

$ErrorActionPreference = 'Stop'

$process = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 3

$alive = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
if ($null -eq $alive) {
    throw "TranslatorTool exited during startup smoke test."
}

$process.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 500
if (-not $process.HasExited) {
    Stop-Process -Id $process.Id -Force
}

"Startup smoke test passed. Process stayed alive."
