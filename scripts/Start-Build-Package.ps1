#requires -Version 5.1
<#
.SYNOPSIS
Explorer-friendly launcher. Right-click this file and choose Run with PowerShell.
Runs Build-Package.ps1 using PowerShell 7 and keeps the window open afterward.
#>
$ErrorActionPreference = 'Stop'
try {
    $shellCommand = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    $shellPath = if ($shellCommand) { $shellCommand.Source } else { $null }
    if (-not $shellPath) {
        $installedShell = Join-Path $env:ProgramFiles 'PowerShell\7\pwsh.exe'
        if (Test-Path -LiteralPath $installedShell) { $shellPath = $installedShell }
    }
    if (-not $shellPath) {
        throw @'
PowerShell 7 was not found. Windows' Run with PowerShell action uses Windows PowerShell 5.1.
Install PowerShell 7, then run this launcher again. Installation command:
    winget install --id Microsoft.PowerShell --exact --source winget
After installation, close and reopen Explorer/your terminal if needed to refresh PATH.
'@
    }
    Write-Host "Using $shellPath"
    & $shellPath -NoProfile -File (Join-Path $PSScriptRoot 'Build-Package.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Package build failed with exit code $LASTEXITCODE. See the error above." }
}
catch {
    Write-Host "`n$($_.Exception.Message)" -ForegroundColor Red
}
finally {
    [void](Read-Host 'Press Enter to close this window')
}
