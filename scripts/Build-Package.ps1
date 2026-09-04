#requires -Version 7.0
<#
.SYNOPSIS
Builds, tests, packs, and verifies the release package; optionally pushes it to NuGet.
.EXAMPLE
./scripts/Build-Package.ps1
.EXAMPLE
./scripts/Build-Package.ps1 -Version 1.0.1-preview.1
.EXAMPLE
./scripts/Build-Package.ps1 -Publish
Requires NUGET_API_KEY in the process environment. Publishing occurs only after all checks pass.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$Publish,
    [string]$Source = 'https://api.nuget.org/v3/index.json'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
    throw 'Release packaging requires Windows x64.'
}
if ($Publish -and [string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
    throw 'Set NUGET_API_KEY in this PowerShell session before using -Publish.'
}

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $repository
try {
    $project = 'src/SevenZipWrapper/SevenZipWrapper.csproj'
    $packageDirectory = Join-Path $repository 'artifacts/packages'
    $versionArguments = @()
    if ($Version) { $versionArguments += "-p:Version=$Version" }

    Invoke-DotNet (@('restore', 'SevenZipWrapper.slnx', '--locked-mode') + $versionArguments)
    Invoke-DotNet (@('clean', 'SevenZipWrapper.slnx', '-c', 'Release', '--verbosity', 'minimal') + $versionArguments)
    Invoke-DotNet (@('build', 'SevenZipWrapper.slnx', '-c', 'Release', '--no-restore') + $versionArguments)
    Invoke-DotNet (@('test', 'src/SevenZipWrapper.Tests/SevenZipWrapper.Tests.csproj',
        '-c', 'Release', '--no-build', '--no-restore', '--collect:XPlat Code Coverage',
        '--logger', 'trx;LogFileName=tests.trx', '--results-directory', 'artifacts/test-results') + $versionArguments)
    Invoke-DotNet (@('pack', $project, '-c', 'Release', '--no-build', '--no-restore',
        '-o', $packageDirectory) + $versionArguments)

    # Use evaluated build properties, including Directory.Build.props and any override.
    $metadataJson = & dotnet msbuild $project -nologo -p:Configuration=Release '-getProperty:PackageId,PackageVersion' @versionArguments
    if ($LASTEXITCODE -ne 0) { throw 'Unable to evaluate package metadata from the build.' }
    $metadata = ($metadataJson | ConvertFrom-Json).Properties
    if ([string]::IsNullOrWhiteSpace($metadata.PackageId) -or [string]::IsNullOrWhiteSpace($metadata.PackageVersion)) {
        throw 'The build did not provide PackageId and PackageVersion.'
    }
    $packagePath = Join-Path $packageDirectory "$($metadata.PackageId).$($metadata.PackageVersion).nupkg"
    & (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackagePath $packagePath -Version $Version

    Write-Host "Verified package: $packagePath"
    if ($Publish) {
        # The pinned SDK reads NUGET_API_KEY directly, avoiding a secret in command arguments.
        Invoke-DotNet @('nuget', 'push', $packagePath, '--source', $Source)
        Write-Host "Published $($metadata.PackageId) $($metadata.PackageVersion) to $Source"
    }
    else {
        Write-Host 'Package built locally. To publish after verification, run this script with -Publish.'
    }
}
finally {
    Pop-Location
}
