#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$PackageDirectory = (Join-Path $PSScriptRoot '../artifacts/packages'),
    [string]$Configuration = 'Release',
    [string]$Version,
    [string]$ArtifactsDirectory = (Join-Path $PSScriptRoot '../artifacts/package-verification')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
    throw 'Package verification requires Windows x64.'
}

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}

$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$libraryProject = Join-Path $repository 'src/SevenZipWrapper/SevenZipWrapper.csproj'
# Evaluate the build, including Directory.Build.props, rather than duplicating its version.
$metadataArguments = @('msbuild', $libraryProject, '-nologo',
    "-property:Configuration=$Configuration", '-getProperty:PackageId,PackageVersion')
if ($Version) { $metadataArguments += "-property:Version=$Version" }
$metadataJson = & dotnet @metadataArguments
if ($LASTEXITCODE -ne 0) { throw 'Unable to evaluate package metadata from the build.' }
$buildMetadata = ($metadataJson | ConvertFrom-Json).Properties
$packageId = [string]$buildMetadata.PackageId
$packageVersion = [string]$buildMetadata.PackageVersion
if ([string]::IsNullOrWhiteSpace($packageId) -or [string]::IsNullOrWhiteSpace($packageVersion)) {
    throw 'The build did not provide PackageId and PackageVersion.'
}
if (-not $PackagePath) {
    $PackagePath = Join-Path $PackageDirectory "$packageId.$packageVersion.nupkg"
}
if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Package for build version '$packageVersion' not found: $PackagePath. Pack the matching build first."
}
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$provenance = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'native-provenance.json') | ConvertFrom-Json
$run = Join-Path ([IO.Path]::GetFullPath($ArtifactsDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null

function Assert-Native([string]$Path) {
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($hash -ne $provenance.sha256) { throw "Native SHA-256 mismatch: $Path" }
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 64 -or [BitConverter]::ToUInt16($bytes, 0) -ne 0x5A4D) { throw 'Native binary has no DOS PE header.' }
    $pe = [BitConverter]::ToInt32($bytes, 0x3C)
    if ($pe -lt 0 -or $pe -gt $bytes.Length - 6 -or [BitConverter]::ToUInt32($bytes, $pe) -ne 0x00004550) { throw 'Native binary has an invalid PE header.' }
    $machine = [BitConverter]::ToUInt16($bytes, $pe + 4).ToString('X4')
    if ($machine -ne $provenance.peMachine) { throw "Wrong native machine: $machine" }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path).FileVersion
    if ($version -ne $provenance.version) { throw "Wrong native version: $version" }
}
Assert-Native (Join-Path $repository $provenance.bundledPath)

$expected = @(
    '_rels/.rels', 'SevenZipWrapper.nuspec', 'README.md',
    'content/x64/7z.dll', 'contentFiles/any/net10.0/x64/7z.dll',
    'lib/net10.0/SevenZipWrapper.dll', '[Content_Types].xml',
    'package/services/metadata/core-properties/nuget.psmdcp',
    'THIRD-PARTY-NOTICES.txt', 'licenses/7zip-License.txt'
)
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    if (@($names | Select-Object -Unique).Count -ne $names.Count) { throw 'Duplicate package entries.' }
    foreach ($name in $names) { if ($name -notin $expected) { throw "Unexpected package content: $name" } }
    foreach ($name in $expected) { if ($name -notin $names) { throw "Missing package content: $name" } }
    foreach ($name in @('content/x64/7z.dll', 'contentFiles/any/net10.0/x64/7z.dll')) {
        $output = Join-Path $run ($name.Replace('/', '_'))
        [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($name), $output)
        Assert-Native $output
    }
    $licenseOutput = Join-Path $run '7zip-License.txt'
    [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('licenses/7zip-License.txt'), $licenseOutput)
    if ((Get-FileHash -LiteralPath $licenseOutput -Algorithm SHA256).Hash -ne $provenance.licenseSha256) { throw 'Native license text does not match the verified upstream notice.' }
    $reader = [IO.StreamReader]::new($zip.GetEntry('SevenZipWrapper.nuspec').Open())
    try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $nuspec.package.metadata
    if ($metadata.id -ne $packageId) { throw "Package identity does not match the build: expected '$packageId'." }
    if ([string]$metadata.version -ne $packageVersion) {
        throw "Package version '$($metadata.version)' does not match build version '$packageVersion'."
    }
    $contentRule = @($metadata.contentFiles.files | Where-Object { $_.include -eq 'any/net10.0/x64/7z.dll' })
    if ($contentRule.Count -ne 1 -or $contentRule[0].copyToOutput -ne 'true') { throw 'Native contentFiles copy-to-output metadata is missing.' }
} finally { $zip.Dispose() }

$consumer = Join-Path $run 'consumer'
New-Item -ItemType Directory -Path $consumer | Out-Null
$cache = Join-Path $run 'isolated-packages'
$source = [Security.SecurityElement]::Escape([IO.Path]::GetDirectoryName($package))
@"
<configuration>
  <packageSources><clear /><add key="produced-package" value="$source" /></packageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config')
# Deliberately avoid inherited source-tree settings and project references.
'<Project />' | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.props')
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><PlatformTarget>x64</PlatformTarget>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NuGetAudit>false</NuGetAudit><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="SevenZipWrapper" Version="[$packageVersion]" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj')
@'
using System.IO.Compression;
using System.Runtime.Versioning;
using SevenZipWrapper;
[assembly: SupportedOSPlatform("windows")]
var root = Path.Combine(AppContext.BaseDirectory, "smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var archivePath = Path.Combine(root, "fixture.zip");
const string payload = "Verified through the produced NuGet package.";
using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
using (var writer = new StreamWriter(zip.CreateEntry("nested/hello.txt").Open())) writer.Write(payload);
using (var archive = new ArchiveFile(archivePath))
{
    if (archive.Format != SevenZipFormat.Zip || archive.Entries.Count != 1) throw new Exception("Packaged archive open failed.");
    using var output = new MemoryStream();
    archive.Entries[0].Extract(output);
    if (System.Text.Encoding.UTF8.GetString(output.ToArray()) != payload) throw new Exception("Packaged stream extraction failed.");
    archive.Extract(Path.Combine(root, "output"));
    if (File.ReadAllText(Path.Combine(root, "output", "nested", "hello.txt")) != payload) throw new Exception("Packaged directory extraction failed.");
}
Console.WriteLine("PASS: package consumer resolved managed and native assets, opened ZIP, and extracted to stream and directory.");
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs')
$project = Join-Path $consumer 'Consumer.csproj'
$config = Join-Path $consumer 'NuGet.Config'
Invoke-DotNet @('restore', $project, '--configfile', $config, '--packages', $cache, '--use-lock-file')
Invoke-DotNet @('restore', $project, '--configfile', $config, '--packages', $cache, '--locked-mode')
Invoke-DotNet @('build', $project, '--no-restore', '-c', $Configuration)
$build = Join-Path $consumer "bin/$Configuration/net10.0"
Assert-Native (Join-Path $build 'x64/7z.dll')
Invoke-DotNet @((Join-Path $build 'Consumer.dll'))
$published = Join-Path $run 'publish'
Invoke-DotNet @('publish', $project, '--no-restore', '-c', $Configuration, '--self-contained', 'false', '-o', $published)
Assert-Native (Join-Path $published 'x64/7z.dll')
Invoke-DotNet @((Join-Path $published 'Consumer.dll'))
@{
    package = [IO.Path]::GetFileName($package)
    packageSha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    packageVersion = $packageVersion
    nativeSha256 = $provenance.sha256
    nativeVersion = $provenance.version
    nativeMachine = $provenance.peMachine
    inspectedEntries = $names
    cleanConsumerRestore = 'passed with isolated cache and only produced-package source'
    buildSmoke = 'passed'
    publishSmoke = 'passed'
    verifiedUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'verification.json')
Write-Host "Package verification passed. Evidence: $run"
