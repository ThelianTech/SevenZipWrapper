#requires -Version 7.0
<#
.SYNOPSIS
Builds the full local artifact set: tests, benchmarks, package, provenance, and verification.
Optionally pushes the verified package to NuGet.
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
    $runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N')
    $testDirectory = Join-Path $repository "artifacts/test-results/$runId"
    $benchmarkDirectory = Join-Path $repository "artifacts/benchmarks/$runId"
    $verificationDirectory = Join-Path $repository "artifacts/package-verification/$runId"
    $provenanceDirectory = Join-Path $repository "artifacts/provenance/$runId"
    $versionArguments = @()
    if ($Version) { $versionArguments += "-p:Version=$Version" }

    Invoke-DotNet (@('restore', 'SevenZipWrapper.slnx', '--locked-mode') + $versionArguments)
    Invoke-DotNet (@('clean', 'SevenZipWrapper.slnx', '-c', 'Release', '--verbosity', 'minimal') + $versionArguments)
    Invoke-DotNet (@('build', 'SevenZipWrapper.slnx', '-c', 'Release', '--no-restore') + $versionArguments)
    Invoke-DotNet (@('test', 'src/SevenZipWrapper.Tests/SevenZipWrapper.Tests.csproj',
        '-c', 'Release', '--no-build', '--no-restore', '--collect:XPlat Code Coverage',
        '--logger', 'trx;LogFileName=tests.trx', '--results-directory', $testDirectory) + $versionArguments)
    Invoke-DotNet @('run', '--project', 'src/SevenZipWrapper.Benchmark', '-c', 'Release',
        '--no-build', '--no-restore', '--', '--filter', '*ExtractAll*', '--job', 'Dry',
        '--artifacts', $benchmarkDirectory)
    # BenchmarkDotNet may finish normally even when individual benchmarks fail.
    $benchmarkReport = Join-Path $benchmarkDirectory 'results/Benchmarks-report-full.json'
    $benchmarkResults = (Get-Content -LiteralPath $benchmarkReport -Raw | ConvertFrom-Json).Benchmarks
    if (@($benchmarkResults).Count -ne 4 -or @($benchmarkResults | Where-Object { $null -eq $_.Statistics }).Count -gt 0) {
        throw 'The four extraction benchmark smoke runs did not all produce results.'
    }
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
    & (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackagePath $packagePath -Version $Version -ArtifactsDirectory $verificationDirectory

    # Preserve the audited native provenance; do not download or replace the bundled engine.
    New-Item -ItemType Directory -Path $provenanceDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'native-provenance.json') -Destination $provenanceDirectory
    Copy-Item -LiteralPath (Join-Path $repository 'licenses/7zip-License.txt') -Destination $provenanceDirectory
    Copy-Item -LiteralPath (Join-Path $repository 'THIRD-PARTY-NOTICES.txt') -Destination $provenanceDirectory
    $verificationReports = @(Get-ChildItem -LiteralPath $verificationDirectory -Filter verification.json -Recurse)
    if ($verificationReports.Count -ne 1) { throw 'Expected one package verification report for this run.' }
    $summary = [ordered]@{
        completedUtc = [DateTime]::UtcNow.ToString('o')
        runId = $runId
        package = $packagePath
        packageVersion = [string]$metadata.PackageVersion
        packageSha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
        testResults = $testDirectory
        benchmarks = $benchmarkDirectory
        benchmarkScope = 'Four extraction Dry smoke runs; not comparative performance measurements.'
        verification = $verificationReports[0].FullName
        provenance = $provenanceDirectory
    }
    $summaryJson = $summary | ConvertTo-Json -Depth 4
    $summaryJson | Set-Content -LiteralPath (Join-Path $provenanceDirectory 'build-artifacts.json')
    $summaryJson | Set-Content -LiteralPath (Join-Path $repository 'artifacts/latest-build.json')

    Write-Host "Verified package: $packagePath"
    Write-Host "Artifact index: $(Join-Path $repository 'artifacts/latest-build.json')"
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
