# SevenZipWrapper

A modern .NET 10 managed wrapper around the native [7z.dll](https://www.7-zip.org/) COM interface for **extracting** archives. Provides mappings for **40+ archive formats** including 7z, ZIP, RAR, TAR, GZip, XZ, Zstd, ISO, and more.

> **Windows x64 only** — this library uses COM interop with the native 7z.dll (v26.02), which is bundled automatically.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![7z.dll](https://img.shields.io/badge/7z.dll-v26.02-green)](https://www.7-zip.org/)

---

## Features

- **Extraction only** — purpose-built for reading and extracting archives (compression/creation is not supported; see [Roadmap](#roadmap))
- **40+ archive format mappings** — 7z, ZIP, RAR/RAR5, TAR, GZip, BZip2, XZ, Zstd, ISO, CAB, WIM, NSIS, and many more
- **Automatic format detection** — from reliable leading signatures, then file-extension fallback; native opening validates the archive
- **Extract all, extract single, or extract selectively** — with a Func<ArchiveEntry, string?> callback
- **Per-file progress reporting** — Action<int> callback fires after each file is extracted
- **Cancellation support** — pass a CancellationToken to stop extraction mid-operation
- **Stream support** — open archives from a Stream and extract entries to a Stream
- **Modern .NET 10** — nullable annotations, FrozenDictionary, ReadOnlySpan<byte>, NativeLibrary, sealed classes, no legacy CAS/serialization
- **Zero-copy signature matching** — stackalloc + SequenceEqual for format detection
- **Immutable public API** — IReadOnlyList<ArchiveEntry> with init-only properties
- **Rooted extraction safeguards** — rejects unsafe paths, reparse points, and conflicting output targets
- **Extraction policies and results** — optional resource limits, explicit best effort, and structured failure reporting
- **Async extraction** — Task-based facades with cooperative cancellation and serialized operations per archive
- **Platform guard** — [SupportedOSPlatform("windows")] attribute provides compile-time warnings on non-Windows targets; runtime loading also requires an x64 process

---

## Requirements

| Requirement | Details |
|---|---|
| **Platform** | Windows x64 |
| **Runtime** | .NET 10.0+ |
| **Native dependency** | 7z.dll v26.02 (bundled — copied to output automatically) |

---

## Installation

```
    dotnet add package SevenZipWrapper
```

The NuGet package bundles the native x64\7z.dll and copies it to build and publish output using NuGet content/contentFiles metadata. A separate 7-Zip installation is not required.

### Manual setup

If you're building from source, ensure x64\7z.dll (v26.02) is present in your output directory. The library searches the following paths in order:

1. {AppBase}\7z.dll
2. {AppBase}\7z-x64.dll
3. {AppBase}\bin\7z-x64.dll
4. {AppBase}\bin\x64\7z.dll
5. {AppBase}\x64\7z.dll

An explicit `libraryFilePath` overrides these candidates. There is no system-installation fallback. The source binary is at `src/SevenZipWrapper/x64/7z.dll`; its provenance is recorded in [native-provenance.json](scripts/native-provenance.json).

---

## Quick Start

### Extract all files from an archive

```csharp
using SevenZipWrapper;

    // Block-scoped
    using (var archive = new ArchiveFile("archive.7z"))
    {
        archive.Extract("output-folder");
    }

    // Declaration-scoped
    using var archive = new ArchiveFile("archive.7z");
    archive.Extract("output-folder");
```

### Extract all with overwrite

```csharp
    // Block-scoped
    using (var archive = new ArchiveFile("archive.7z"))
    {
        archive.Extract("output-folder", overwrite: true);
    }

    // Declaration-scoped
    using var archive = new ArchiveFile("archive.7z");
    archive.Extract("output-folder", overwrite: true);
```

### List entries and their properties

```csharp
    // Block-scoped
    using (var archive = new ArchiveFile("archive.zip"))
    {
        foreach (ArchiveEntry entry in archive.Entries)
        {
            Console.WriteLine($"{entry.FileName}  {entry.Size} bytes  Folder={entry.IsFolder}");
        }
    }

    // Declaration-scoped
    using var archive = new ArchiveFile("archive.zip");

    foreach (ArchiveEntry entry in archive.Entries)
    {
        Console.WriteLine($"{entry.FileName}  {entry.Size} bytes  Folder={entry.IsFolder}");
    }
```

### Extract a single entry to a file

```csharp
    // Block-scoped
    using (var archive = new ArchiveFile("archive.7z"))
    {
        archive.Entries[0].Extract("extracted-file.txt");
    }

    // Declaration-scoped
    using var archive = new ArchiveFile("archive.7z");
    archive.Entries[0].Extract("extracted-file.txt");
```

### Extract a single entry to a stream

```csharp
    // Block-scoped
    byte[] data;

    using (var archive = new ArchiveFile("archive.7z"))
    using (var memoryStream = new MemoryStream())
    {
        archive.Entries[0].Extract(memoryStream);
        data = memoryStream.ToArray();
    }

    // Declaration-scoped
    using var archive = new ArchiveFile("archive.7z");
    using var memoryStream = new MemoryStream();

    archive.Entries[0].Extract(memoryStream);

    byte[] data = memoryStream.ToArray();
```

### Selective extraction via callback

Extract only '.txt' files to a custom location:
```csharp
    // Block-scoped
    using (var archive = new ArchiveFile("archive.7z"))
    {
        archive.Extract(entry =>
            entry.FileName?.EndsWith(".txt") == true
                ? Path.Combine("output", entry.FileName)
                : null  // returning null skips the entry
        );
    }

    // Declaration-scoped
    using var archive = new ArchiveFile("archive.7z");

    archive.Extract(entry =>
        entry.FileName?.EndsWith(".txt") == true
            ? Path.Combine("output", entry.FileName)
            : null  // returning null skips the entry
    );
```

### Extract with progress reporting

Track how many files have been extracted so far:
```csharp
    // Block-scoped
    using (var archive = new ArchiveFile("archive.7z"))
    {
        int totalFiles = archive.Entries.Count(e => !e.IsFolder);

        archive.Extract(
            "output-folder",
            overwrite: true,
            onFileExtracted: count =>
            {
                Console.WriteLine($"Extracted {count} of {totalFiles} files...");
            },
            cancellationToken: CancellationToken.None);
    }

    // Declaration-scoped
    using var archive = new ArchiveFile("archive.7z");

    int totalFiles = archive.Entries.Count(e => !e.IsFolder);

    archive.Extract(
        "output-folder",
        overwrite: true,
        onFileExtracted: count =>
        {
            Console.WriteLine($"Extracted {count} of {totalFiles} files...");
        },
        cancellationToken: CancellationToken.None);
```
### Extract with cancellation

Cancel extraction mid-operation using a CancellationToken:
```csharp
    // Block-scoped
    using (var cts = new CancellationTokenSource())
    {
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        using (var archive = new ArchiveFile("large-archive.7z"))
        {
            try
            {
                archive.Extract(
                    "output-folder",
                    overwrite: true,
                    onFileExtracted: null,
                    cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Extraction was cancelled.");
            }
        }
    }

    // Declaration-scoped
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromSeconds(5));

    using var archive = new ArchiveFile("large-archive.7z");

    try
    {
        archive.Extract(
            "output-folder",
            overwrite: true,
            onFileExtracted: null,
            cancellationToken: cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Extraction was cancelled.");
    }
```
### Selective extraction with progress and cancellation

Combine all features — extract only specific files, report progress, and support cancellation:
```csharp
    // Block-scoped
    using (var cts = new CancellationTokenSource())
    using (var archive = new ArchiveFile("archive.7z"))
    {
        archive.Extract(
            getOutputPath: entry =>
                entry.FileName?.EndsWith(".dll") == true
                    ? Path.Combine("output", entry.FileName)
                    : null,
            onFileExtracted: count => Console.WriteLine($"Files extracted: {count}"),
            cancellationToken: cts.Token);
    }

    // Declaration-scoped
    using var cts = new CancellationTokenSource();
    using var archive = new ArchiveFile("archive.7z");

    archive.Extract(
        getOutputPath: entry =>
            entry.FileName?.EndsWith(".dll") == true
                ? Path.Combine("output", entry.FileName)
                : null,
        onFileExtracted: count => Console.WriteLine($"Files extracted: {count}"),
        cancellationToken: cts.Token);
```
### Open from a stream

```csharp
    // Block-scoped
    using (var fileStream = File.OpenRead("archive.zip"))
    using (var archive = new ArchiveFile(fileStream, SevenZipFormat.Zip))
    {
        foreach (ArchiveEntry entry in archive.Entries)
        {
            Console.WriteLine(entry.FileName);
        }
    }

    // Declaration-scoped
    using var fileStream = File.OpenRead("archive.zip");
    using var archive = new ArchiveFile(fileStream, SevenZipFormat.Zip);

    foreach (ArchiveEntry entry in archive.Entries)
    {
        Console.WriteLine(entry.FileName);
    }
```

### Auto-detect format from stream signature

```csharp
    // Block-scoped
    using (var fileStream = File.OpenRead("archive.bin"))
    using (var archive = new ArchiveFile(fileStream)) // format auto-detected from magic bytes
    {
        Console.WriteLine($"Detected format: {archive.Format}");
    }

    // Declaration-scoped
    using var fileStream = File.OpenRead("archive.bin");
    using var archive = new ArchiveFile(fileStream); // format auto-detected from magic bytes

    Console.WriteLine($"Detected format: {archive.Format}");
```

---

## API Reference

### ArchiveFile

The main entry point. Implements IDisposable — always wrap in a using statement. Operations on one archive, including its entries, are serialized; use separate instances for parallel native work. Input streams must be readable and seekable, are owned by default, and can remain open with `ArchiveOpenOptions.LeaveOpen`.

| Member | Description |
|---|---|
| ArchiveFile(string archiveFilePath, string? libraryFilePath = null) | Open an archive from a file path. Format is detected from a reliable leading signature, then extension fallback. |
| ArchiveFile(Stream archiveStream, SevenZipFormat? format = null, string? libraryFilePath = null) | Open an archive from a stream. Pass format explicitly or let it auto-detect from signature. |
| SevenZipFormat Format { get; } | The detected or explicitly specified archive format. |
| IReadOnlyList<ArchiveEntry> Entries { get; } | All entries in the archive. The archive is opened lazily on first access. |
| void Extract(string outputFolder, bool overwrite = false, string? password = null) | Extract all entries to a directory. |
| void Extract(Func<ArchiveEntry, string?> getOutputPath, string? password = null) | Extract entries selectively. Return null from the callback to skip an entry. |
| void Extract(string outputFolder, bool overwrite, Action<int>? onFileExtracted, CancellationToken cancellationToken, string? password = null) | Extract all entries with per-file progress callback and cancellation support. |
| void Extract(Func<ArchiveEntry, string?> getOutputPath, Action<int>? onFileExtracted, CancellationToken cancellationToken, string? password = null) | Extract selectively with per-file progress callback and cancellation support. |
| void Dispose() | Waits for active work, releases native COM resources, and closes owned input streams. |

| Additional 1.0 member | Description |
|---|---|
| static ArchiveFile Open(string archiveFilePath, ArchiveOpenOptions options) | Open with explicit format, native path, and experimental password options. |
| static ArchiveFile Open(Stream archiveStream, ArchiveOpenOptions options) | Open with the same options plus LeaveOpen for caller-owned input. |
| void Extract(string outputFolder, ExtractionOptions options) | Rooted extraction with resource limits and failure policy. |
| ExtractionResult ExtractWithResult(string outputFolder, ExtractionOptions? options = null) | Return per-entry outcomes and structured archive failures. |
| Task ExtractAsync(string outputFolder, ExtractionOptions? options = null) | Task facade over the same extraction core. |
| Task<ExtractionResult> ExtractWithResultAsync(string outputFolder, ExtractionOptions? options = null) | Async facade returning structured outcomes. |

See [API behavior](docs/ApiBehavior.md) and [extraction policies](docs/ExtractionBehavior.md) for limits, skip/failure behavior, and stream ownership.

#### Progress callback (onFileExtracted)

- Fires after each **file** is successfully extracted (folders and skipped entries are not counted)
- Parameter is the **cumulative count** of files extracted so far (1-based)
- Called on the **calling thread** for synchronous extraction and a worker thread for async facades — callers must dispatch to the UI thread if needed
- Pass null to disable progress reporting

#### Cancellation (cancellationToken)

- Checked before each file, while waiting for the operation gate, and at native callback boundaries
- When cancelled, 7z.dll stops its extraction loop via E_ABORT
- OperationCanceledException is thrown after 7z.dll returns
- Already-extracted files remain on disk
- Pass CancellationToken.None or default when cancellation is not needed

### ArchiveEntry

Represents a single file or folder inside an archive. Properties are read-only.

| Property | Type | Description |
|---|---|---|
| FileName | string? | Relative path within the archive. |
| IsFolder | bool | true if the entry is a directory. |
| Size | ulong | Uncompressed size in bytes. |
| PackedSize | ulong | Compressed size in bytes. |
| IsEncrypted | bool | true if the entry is password-protected. |
| CRC | uint | CRC-32 checksum. |
| CreationTime | DateTime | When the entry was created. |
| LastWriteTime | DateTime | When the entry was last modified. |
| LastAccessTime | DateTime | When the entry was last accessed. |
| Attributes | uint | File system attributes. |
| Method | string? | Compression method (e.g., LZMA2, Deflate). |
| Comment | string? | Entry comment, if any. |
| HostOS | string? | OS that created the entry. |
| IsSplitBefore | bool | Part of this entry exists in a previous split volume. |
| IsSplitAfter | bool | Part of this entry exists in a subsequent split volume. |

| Method | Description |
|---|---|
| void Extract(string fileName, bool preserveTimestamp = true) | Extract to a file on disk. Optionally preserves the original last-write time. |
| void Extract(Stream stream, string? password = null) | Extract to a stream. |

| Additional 1.0 method | Description |
|---|---|
| void Extract(string fileName, ExtractionOptions options, bool preserveTimestamp = true) | File extraction with explicit policies and experimental credentials. |
| void ExtractWithOptions(Stream stream, ExtractionOptions options) | Stream extraction with policies; preserves compatibility with Extract(stream, null). |
| Task ExtractAsync(string fileName, ExtractionOptions? options = null, bool preserveTimestamp = true) | Async file extraction; omitted options retain the legacy overwrite behavior. |
| Task ExtractAsync(Stream stream, ExtractionOptions? options = null) | Async extraction to a caller-owned output stream. |

Entries depend on their parent archive and cannot extract after it is disposed. Caller output streams remain open.

### SevenZipFormats

The `SevenZipFormat` enum lists mapped archive formats. Notable members:

| Format | Extension | Notes |
|---|---|---|
| SevenZip | .7z | Solid compression, high ratio |
| Zip | .zip | Most common archive format |
| Rar / Rar5 | .rar | Auto-detected via signature (not extension) |
| GZip | .gz | Single-file compression |
| Tar | .tar | Unix tape archive |
| XZ | .xz | LZMA2-based compression |
| Zstd | .zst | Facebook's Zstandard (7z 23.01+) |
| Iso | .iso | Disk images |
| Cab | .cab | Windows Cabinet |
| Wim | .wim | Windows Imaging Format |
| BZip2 | .bz2 | Block-sorting compression |
| Lzma | .lzma | Raw LZMA stream |

See the full list of 40+ formats in [SevenZipFormat.cs](src/SevenZipWrapper/SevenZipFormat.cs).

### SevenZipException

Thrown when a 7-Zip operation fails (archive not found, unknown format, extraction error, etc.). The `Failure` property contains structured details; `ArchiveExtractionException` and `SevenZipNativeException` derive from this base type. Caller callback/stream exceptions retain their original identity. Rich extraction results report archive failures, while cancellation and caller exceptions still propagate.

---

## Supported Archive Formats

SevenZipWrapper exposes mappings for the following formats through 7z.dll v26.02. Mapped formats, automatic detection, and tested compatibility are distinct; representative native fixtures cover ZIP, 7z, RAR, ARJ, and LZH. See the [compatibility matrix](docs/CompatibilityAndFormats.md) for details:

7z, ARJ, BZip2, CAB, CHM, Compound, CPIO, Deb, GZip, ISO, LZH, LZMA, LZMA86, Lzw, MachO, Mslz, Mub, NSIS, NTFS, FAT, MBR, APM, PE, ELF, RAR, RAR5, RPM, Split, SWF, SWFc, TAR, UDF, WIM, XAR, XZ, ZIP, Zstd, CramFS, SquashFS, DMG, HFS, VHD, FLV, Ppmd, TE, UEFIc, UEFIs

---

## Benchmarks

> These tables retain the historical measurements. They are not measurements of the 1.0 implementation. The current four extraction Dry runs are smoke checks only; the large LT_Nemesis.7z fixture is not included in this checkout.

Benchmarked on AMD Ryzen 7 5800X (8C/16T) · Windows 11 24H2 · .NET 10.0.2 · x64 Release · BenchmarkDotNet 0.15.2

### Small Archive — 7z.7z

| Benchmark | Mean | Allocated | vs .NET Core 3.1 (old lib) |
|---|---|---|---|
| **EnumerateEntries** | 1,541 µs | 9,464 B | ↓ 12% faster · ↓ 11% memory 🟢 |
| **ExtractFirstEntry** | 3,217 µs | 190,268 B | ↓ 8.1% faster · ↓ 1.1% memory 🟢 |
| **ExtractLastEntry** | 4,958 µs | 183,973 B | ↓ 9.4% faster · ↓ 1.1% memory 🟢 |
| **ExtractAll** | 6,123 µs | 280,805 B | ↓ 12% faster · ↓ 2.1% memory 🟢 |

### Large Archive — LT_Nemesis.7z (~1,980 files)

| Benchmark | Mean | Allocated | vs .NET Core 3.1 (old lib) |
|---|---|---|---|
| **EnumerateEntries** | 5,244 µs | 1.82 MB | ↓ 25% faster · ↓ 5.5% memory 🟢 |
| **ExtractFirstEntry** | 7,657 µs | 2.91 MB | ↓ 27% faster · ↓ 3.5% memory 🟢 |
| **ExtractLastEntry** | 5,744 µs | 1.82 MB | ↓ 36% faster · ↓ 5.6% memory 🟢 |
| **ExtractAll** | 2,073 ms | 127.82 MB | ↓ 12% faster · ↓ 2.0% memory 🟢 |

### New Feature Overhead

| Feature | Speed Overhead | Memory Overhead | Verdict |
|---|---|---|---|
| **Progress callback** | ≈ 0% | +88 B | ✅ Zero cost |
| **CancellationToken** | ≈ 0% | 0 B | ✅ Zero cost |
| **Both combined** | ≈ 0% | +72 B | ✅ Zero cost |
| **Selective extract** | ≈ 0% | 0 B | ✅ Zero cost |

These historical runs reported faster execution and lower allocations than the old-library baseline. Fresh comparative measurements are required before applying those performance claims to the current implementation.

> Full results, methodology, and environment details: [BenchmarkResults.md](docs/BenchmarkResults.md)

---

## Known Limitations

- **Extraction only** — this wrapper supports reading and extracting archives. Archive creation/compression is not supported (see [Roadmap](#roadmap)).
- **Serialized native work** — operations on one ArchiveFile instance and its entries share an operation gate. Separate instances are required for parallel extraction; reentrant operations on the same instance are rejected.
- **Async uses synchronous native I/O** — Task-based extraction is available, but the native engine remains synchronous and cancellation is cooperative.
- **No rollback transaction** — completed files can remain after a later failure, and overwritten originals are not restored. Callers own staging and rollback.
- **Password-protected archives** — explicitly experimental. Opening and extraction options accept credentials, but the complete header/content encryption matrix is not certified. Full certification remains future work.

---

## Roadmap

| Feature | Target |
|---|---|
| **Async/await extraction** | Implemented in 1.0.0 as Task-based facades |
| **Full password support** | Experimental in 1.0.0; full certification remains future work |
| **Archive creation/compression** | Post-1.0.0 (under consideration) |

---

## Migration from SevenZipExtractor

If you're coming from the original [SevenZipExtractor](https://github.com/adoconnection/SevenZipExtractor) package:

| Change | Old (SevenZipExtractor) | New (SevenZipWrapper) |
|---|---|---|
| **Target framework** | .NET Framework 4.5 / .NET Standard 2.0 | .NET 10 |
| **Platform** | x86 + x64 | x64 only |
| **7z.dll version** | Older (CVE-affected) | v26.02 (bundled; see native provenance) |
| **Namespace** | SevenZipExtractor | SevenZipWrapper |
| **Entry type** | Entry | ArchiveEntry |
| **Entries collection** | IList<Entry> | IReadOnlyList<ArchiveEntry> |
| **Selective extract** | Func<Entry, string> | Func<ArchiveEntry, string?> (return null to skip) |
| **Extract overwrite** | Not supported | overwrite: true parameter |
| **Progress reporting** | ❌ | ✅ Action<int> per-file callback |
| **Cancellation** | ❌ | ✅ CancellationToken support |
| **Zstd support** | ❌ | ✅ |
| **Nullable annotations** | ❌ | ✅ Full |

---

## Project Structure

The library layout below is under `src/SevenZipWrapper/`. Sibling projects are `src/SevenZipWrapper.Tests/`, `src/SevenZipWrapper.Example/`, and `src/SevenZipWrapper.Benchmark/`. Repository-wide files include `Directory.Build.props` (version and build defaults), `global.json` (SDK pin), `scripts/` (packaging), `.github/workflows/` (CI), `docs/`, and `licenses/`.

```
    SevenZipWrapper/
    ├── ArchiveFile.cs              # Main public API
    ├── ArchiveFile.Extraction.cs   # Shared extraction engine
    ├── ArchiveOpenOptions.cs       # Opening options and input ownership
    ├── ExtractionOptions.cs        # Extraction policies and limits
    ├── ArchiveFailure.cs           # Structured failures and results
    ├── OperationGate.cs            # Operation/disposal coordination
    ├── ExtractionBudget.cs         # Resource accounting
    ├── RootedPath.cs               # Archive path validation
    ├── RootedOutputSession.cs      # Handle-relative output handling
    ├── ArchiveEntry.cs             # Archive entry (file/folder)
    ├── SevenZipFormat.cs           # Supported format enum
    ├── SevenZipException.cs        # Custom exception type
    ├── Formats.cs                  # FrozenDictionary format mappings
    ├── Callbacks/
    │   ├── ArchiveStreamCallback.cs    # Single entry → Stream
    │   ├── ArchiveStreamsCallback.cs   # All entries → Stream list
    │   └── ArchiveFileCallback.cs     # Single entry → file
    ├── Interop/
    │   ├── SevenZipInterop.cs     # COM interfaces, PropVariant, stream wrappers
    │   └── SevenZipHandle.cs      # Native library loading via NativeLibrary API
    └── x64/
        └── 7z.dll                 # Native 7z.dll v26.02 (bundled)
```
---

## Building from Source

Use Windows x64 and the .NET SDK pinned in `global.json`. Package and assembly versioning comes from the root `Directory.Build.props`; dependency versions are recorded in project `packages.lock.json` files.

- Clone the repository and build the solution:
```
    git clone https://github.com/ThelianTech/SevenZipWrapper.git
    cd SevenZipWrapper
    dotnet restore SevenZipWrapper.slnx --locked-mode
    dotnet build SevenZipWrapper.slnx -c Release --no-restore
```

### Run tests

```
    dotnet test src/SevenZipWrapper.Tests/SevenZipWrapper.Tests.csproj -c Release
```

### Run benchmarks

```
    dotnet run --project src/SevenZipWrapper.Benchmark -c Release -- --filter '*ExtractAll*' --job Dry --artifacts artifacts/benchmarks
```
---

### Build and publish a NuGet package

From the repository root in PowerShell 7:

```powershell
# Locked restore, clean Release build, tests, pack, and isolated consumer verification.
./scripts/Build-Package.ps1

# Optional temporary version override; otherwise uses Directory.Build.props.
./scripts/Build-Package.ps1 -Version 1.0.1-preview.1
```

The verified `.nupkg` is written to `artifacts/packages`. Nothing is uploaded by default.
To explicitly rebuild, verify, and publish to NuGet.org:

```powershell
$env:NUGET_API_KEY = Read-Host 'NuGet API key' -MaskInput
try {
    ./scripts/Build-Package.ps1 -Publish
}
finally {
    Remove-Item Env:NUGET_API_KEY -ErrorAction SilentlyContinue
}
```

`dotnet pack` creates the package and `dotnet nuget push` uploads it. The existing
`Verify-Package.ps1` checks an already-built package, including native provenance and
isolated consumer build/publish extraction. The CI workflow performs verification without
uploading to NuGet. See [release instructions](docs/ReleaseProcess.md) for details.

The September 4, 2026 local verification passed all **209 tests**, a clean Release build
with **zero warnings/errors**, package inspection, and consumer build/publish smoke checks.

---

## License

[MIT](LICENSE)

The bundled native engine has separate [7-Zip license terms](licenses/7zip-License.txt); see [third-party notices](THIRD-PARTY-NOTICES.txt).

---

## Acknowledgements

- [Igor Pavlov](https://www.7-zip.org/) — 7-Zip and the 7z.dll native library
- [adoconnection/SevenZipExtractor](https://github.com/adoconnection/SevenZipExtractor) — original .NET wrapper this project is based on as well as used their test files that were included in the old Unit Test Project.
