# SevenZipWrapper

A modern .NET 10 managed wrapper around the native [7z.dll](https://www.7-zip.org/) COM interface for extracting archives. Supports **40+ archive formats** including 7z, ZIP, RAR, TAR, GZip, XZ, Zstd, ISO, and more.

> **Windows x64 only** — this library uses COM interop with the native 7z.dll (v26.00), which is bundled automatically.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![7z.dll](https://img.shields.io/badge/7z.dll-v26.00-green)](https://www.7-zip.org/)

---

## Features

- **40+ archive formats** — 7z, ZIP, RAR/RAR5, TAR, GZip, BZip2, XZ, Zstd, ISO, CAB, WIM, NSIS, and many more
- **Automatic format detection** — from file extension or stream magic-byte signature
- **Extract all, extract single, or extract selectively** — with a `Func<ArchiveEntry, string?>` callback
- **Stream support** — open archives from a `Stream` and extract entries to a `Stream`
- **Modern .NET 10** — nullable annotations, `FrozenDictionary`, `ReadOnlySpan<byte>`, `NativeLibrary`, sealed classes, no legacy CAS/serialization
- **Zero-copy signature matching** — `stackalloc` + `SequenceEqual` for format detection
- **Immutable public API** — `IReadOnlyList<ArchiveEntry>` with `init`-only properties

---

## Requirements

| Requirement | Details |
|---|---|
| **Platform** | Windows x64 |
| **Runtime** | .NET 10.0+ |
| **Native dependency** | `7z.dll` v26.00 (bundled — copied to output automatically) |

---

## Installation

```
dotnet add package SevenZipWrapper
```

The NuGet package bundles the native `x64\7z.dll` and copies it to your output directory automatically via an MSBuild `.targets` file.

### Manual setup

If you're building from source, ensure `x64\7z.dll` (v26.00) is present in your output directory. The library searches the following paths in order:

1. `{AppBase}\7z-x64.dll`
2. `{AppBase}\bin\7z-x64.dll`
3. `{AppBase}\bin\x64\7z.dll`
4. `{AppBase}\x64\7z.dll`
5. `C:\Program Files\7-Zip\7z.dll` (system fallback)

---

## Quick Start

### Extract all files from an archive

```csharp
using SevenZipWrapper;

using var archive = new ArchiveFile("archive.7z");
archive.Extract("output-folder");
```

### Extract all with overwrite

```csharp

using var archive = new ArchiveFile("archive.7z");
archive.Extract("output-folder", overwrite: true);
```

### List entries and their properties

```csharp
using var archive = new ArchiveFile("archive.zip");

foreach (ArchiveEntry entry in archive.Entries)
{
    Console.WriteLine($"{entry.FileName}  {entry.Size} bytes  Folder={entry.IsFolder}");
}
```

### Extract a single entry to a file

```csharp

using var archive = new ArchiveFile("archive.7z");
archive.Entries[0].Extract("extracted-file.txt");
```

### Extract a single entry to a stream

```csharp
using var archive = new ArchiveFile("archive.7z");
using var memoryStream = new MemoryStream();

archive.Entries[0].Extract(memoryStream);

byte[] data = memoryStream.ToArray();

### Selective extraction via callback

Extract only `.txt` files to a custom location:

**(Put In Code Block)** — language: csharp

using var archive = new ArchiveFile("archive.7z");

archive.Extract(entry =>
    entry.FileName?.EndsWith(".txt") == true
        ? Path.Combine("output", entry.FileName)
        : null  // returning null skips the entry
);
```

### Open from a stream

```csharp

using var fileStream = File.OpenRead("archive.zip");
using var archive = new ArchiveFile(fileStream, SevenZipFormat.Zip);

foreach (ArchiveEntry entry in archive.Entries)
{
    Console.WriteLine(entry.FileName);
}
```

### Auto-detect format from stream signature

```csharp

using var fileStream = File.OpenRead("archive.bin");
using var archive = new ArchiveFile(fileStream); // format auto-detected from magic bytes

Console.WriteLine($"Detected format: {archive.Format}");
```

---

## API Reference

### `ArchiveFile`

The main entry point. Implements `IDisposable` — always wrap in a `using` statement.

| Member | Description |
|---|---|
| `ArchiveFile(string archiveFilePath, string? libraryFilePath = null)` | Open an archive from a file path. Format is auto-detected from extension or signature. |
| `ArchiveFile(Stream archiveStream, SevenZipFormat? format = null, string? libraryFilePath = null)` | Open an archive from a stream. Pass `format` explicitly or let it auto-detect from signature. |
| `SevenZipFormat Format { get; }` | The detected or explicitly specified archive format. |
| `IReadOnlyList<ArchiveEntry> Entries { get; }` | All entries in the archive. The archive is opened lazily on first access. |
| `void Extract(string outputFolder, bool overwrite = false, string? password = null)` | Extract all entries to a directory. |
| `void Extract(Func<ArchiveEntry, string?> getOutputPath, string? password = null)` | Extract entries selectively. Return `null` from the callback to skip an entry. |
| `void Dispose()` | Releases the native COM resources and closes the archive stream. |

### `ArchiveEntry`

Represents a single file or folder inside an archive. Properties are read-only.

| Property | Type | Description |
|---|---|---|
| `FileName` | `string?` | Relative path within the archive. |
| `IsFolder` | `bool` | `true` if the entry is a directory. |
| `Size` | `ulong` | Uncompressed size in bytes. |
| `PackedSize` | `ulong` | Compressed size in bytes. |
| `IsEncrypted` | `bool` | `true` if the entry is password-protected. |
| `CRC` | `uint` | CRC-32 checksum. |
| `CreationTime` | `DateTime` | When the entry was created. |
| `LastWriteTime` | `DateTime` | When the entry was last modified. |
| `LastAccessTime` | `DateTime` | When the entry was last accessed. |
| `Attributes` | `uint` | File system attributes. |
| `Method` | `string?` | Compression method (e.g., `LZMA2`, `Deflate`). |
| `Comment` | `string?` | Entry comment, if any. |
| `HostOS` | `string?` | OS that created the entry. |
| `IsSplitBefore` | `bool` | Part of this entry exists in a previous split volume. |
| `IsSplitAfter` | `bool` | Part of this entry exists in a subsequent split volume. |

| Method | Description |
|---|---|
| `void Extract(string fileName, bool preserveTimestamp = true)` | Extract to a file on disk. Optionally preserves the original last-write time. |
| `void Extract(Stream stream, string? password = null)` | Extract to a stream. |

### `SevenZipFormat`

Enum of all supported archive formats. Notable members:

| Format | Extension | Notes |
|---|---|---|
| `SevenZip` | `.7z` | Solid compression, high ratio |
| `Zip` | `.zip` | Most common archive format |
| `Rar` / `Rar5` | `.rar` | Auto-detected via signature (not extension) |
| `GZip` | `.gz` | Single-file compression |
| `Tar` | `.tar` | Unix tape archive |
| `XZ` | `.xz` | LZMA2-based compression |
| `Zstd` | `.zst` | Facebook's Zstandard (7z 23.01+) |
| `Iso` | `.iso` | Disk images |
| `Cab` | `.cab` | Windows Cabinet |
| `Wim` | `.wim` | Windows Imaging Format |
| `BZip2` | `.bz2` | Block-sorting compression |
| `Lzma` | `.lzma` | Raw LZMA stream |

See the full list of 40+ formats in [`SevenZipFormat.cs`](SevenZipWrapper/SevenZipFormat.cs).

### `SevenZipException`

Thrown when a 7-Zip operation fails (archive not found, unknown format, extraction error, etc.).

---

## Supported Archive Formats

SevenZipWrapper supports all formats recognized by 7z.dll v26.00:

7z, ARJ, BZip2, CAB, CHM, Compound, CPIO, Deb, GZip, ISO, LZH, LZMA, LZMA86, Lzw, MachO, Mub, NSIS, NTFS, FAT, MBR, APM, PE, ELF, RAR, RAR5, RPM, Split, SWF, SWFc, TAR, UDF, WIM, XAR, XZ, ZIP, Zstd, CramFS, SquashFS, DMG, HFS, VHD, FLV, Msi, Ppmd, TE, UEFIc, UEFIs

---

## Benchmarks

Benchmarked on AMD Ryzen 7 5800X (8C/16T) · Windows 11 24H2 · .NET 10.0.2 · x64 Release · BenchmarkDotNet 0.15.2

| Benchmark | Mean | Allocated | vs .NET Core 3.1 (old lib) |
|---|---|---|---|
| **EnumerateEntries** | 1,390 µs | 9,144 B | Memory ↓ 14% 🟢 |
| **ExtractFirstEntry** | 2,998 µs | 189,948 B | Speed ↓ 6.6% faster, Memory ↓ 1.3% 🟢 |
| **ExtractLastEntry** | 4,447 µs | 183,653 B | Speed ↓ 1.5% faster, Memory ↓ 1.3% 🟢 |
| **ExtractAll** | 5,255 µs | 280,461 B | Memory ↓ 1.6% 🟢 (speed within noise) |

> Full results and methodology: [`SevenZipWrapper.Benchmark/BenchmarkResults.md`](SevenZipWrapper.Benchmark/BenchmarkResults.md)

---

## Known Limitations

- **Password-protected archives** — not yet fully supported. The `password` parameter exists on some extraction methods but has not been tested against encrypted archives, and `ArchiveEntry.Extract(string fileName)` does not accept a password. Full password support is planned for a future release.

---

## Migration from SevenZipExtractor

If you're coming from the original [SevenZipExtractor](https://github.com/adoconnection/SevenZipExtractor) package:

| Change | Old (`SevenZipExtractor`) | New (`SevenZipWrapper`) |
|---|---|---|
| **Namespace** | `SevenZipExtractor` | `SevenZipWrapper` |
| **Entry type** | `Entry` | `ArchiveEntry` |
| **Entries collection** | `IList<Entry>` | `IReadOnlyList<ArchiveEntry>` |
| **Target framework** | .NET Framework 4.5 / .NET Standard 2.0 | .NET 10 |
| **Platform** | x86 + x64 | x64 only |
| **7z.dll version** | Older (CVE-affected) | v26.00 (2026-02-12) |
| **Selective extract** | `Func<Entry, string>` | `Func<ArchiveEntry, string?>` (return `null` to skip) |
| **Extract overwrite** | Not supported | `overwrite: true` parameter |
| **Zstd support** | ❌ | ✅ |
| **Nullable annotations** | ❌ | ✅ Full |

---

## Project Structure

```
SevenZipWrapper/
├── ArchiveFile.cs              # Main public API
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
    └── 7z.dll                 # Native 7z.dll v26.00 (bundled)
```
---

## Building from Source

```
git clone https://github.com/ThelianTech/SevenZipExtractor.git
cd SevenZipExtractor
git checkout net10-update
dotnet build SevenZipWrapper -c Release
```

### Run tests

```
dotnet test SevenZipWrapper.Tests -c Release
```

### Run benchmarks

```
dotnet run --project SevenZipWrapper.Benchmark -c Release
```
---

## License

[MIT](LICENSE)

---

## Acknowledgements

- [Igor Pavlov](https://www.7-zip.org/) — 7-Zip and the 7z.dll native library
- [adoconnection/SevenZipExtractor](https://github.com/adoconnection/SevenZipExtractor) — original .NET wrapper this project is based on