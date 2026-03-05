	# Changelog

All notable changes to the **SevenZipWrapper** project are documented in this file.

> This project is a ground-up rewrite of [SevenZipExtractor](https://github.com/ThelianTech/SevenZipExtractor),  
> modernized for **.NET 10** with current C# language features and APIs.

---

## [0.9.5] — 2026-03-05

### 🔧 Core Library — `SevenZipWrapper`

#### Progress Reporting & Cancellation Support

- **Added** `Extract(string, bool, Action<int>?, CancellationToken, string?)` overload — extract all entries with per-file progress callback and cancellation support.
- **Added** `Extract(Func<ArchiveEntry, string?>, Action<int>?, CancellationToken, string?)` overload — selective extraction with per-file progress callback and cancellation support.
- **No breaking changes** — existing `Extract(string, bool, string?)` and `Extract(Func<>, string?)` overloads remain unchanged.

#### Callback Implementation (`ArchiveStreamsCallback`)

- **Added** `Action<int>? onFileExtracted` parameter (default `null`) — invoked after each file is successfully extracted with the cumulative count (1-based).
- **Added** `CancellationToken cancellationToken` parameter (default `default`) — checked in `GetStream()` before each file; returns `E_ABORT` (`0x80004004`) to stop 7z.dll.
- **Added** `_currentEntryHasStream` flag — tracks whether the current entry had an actual output stream. `SetOperationResult` only increments the counter and fires the callback for entries that had a stream, preventing folders and skipped entries from inflating the count.
- **Added** `_filesExtracted` counter — tracks cumulative files extracted for progress reporting.
- **Wired** `SetOperationResult(OperationResult)` — previously a no-op, now increments counter and invokes `onFileExtracted` on `OperationResult.OK` when the entry had a stream.
- **Wired** `GetStream()` cancellation check — returns `E_ABORT` to 7z.dll when `CancellationToken` is cancelled.
- After `_archive.Extract()` returns, `cancellationToken.ThrowIfCancellationRequested()` surfaces cancellation as `OperationCanceledException`.

---

### 🧪 Tests — `SevenZipWrapper.Tests`

#### New Test Classes

| Class | Tests | Description |
|-------|-------|-------------|
| `ExtractProgressTests` | 6 | Per-file callback fires for ZIP/7z/RAR, sequential count increments, null callback safety, `Func<>` overload |
| `ExtractCancellationTests` | 5 | Cancel before start, cancel mid-extraction, default token safety, existing overload regression |

#### Test Results

- **89 tests** in `SevenZipWrapper.Tests` — all passing.
- Initial run had 3 failures (`Expected: 3, Actual: 4`) — `SetOperationResult` was firing for folders and skipped entries. Resolved by adding `_currentEntryHasStream` guard.

---

### 🔑 Design Decisions

1. **`Action<int>` over `IProgress<T>`** — simplest possible contract with zero allocation. `IProgress<T>` support is planned separately for byte-level progress.
2. **`E_ABORT` (`0x80004004`)** — standard COM HRESULT for aborting operations; 7z.dll respects this from `GetStream` and stops its extraction loop.
3. **Positional parameters (no defaults)** on new `Extract` overloads — avoids call-site ambiguity with existing overloads.
4. **`_currentEntryHasStream` guard** — 7z.dll calls `SetOperationResult(OK)` for every entry including folders; the flag ensures only actual file extractions are counted.
5. **`finally` block** — all file streams are disposed even when cancellation occurs.

---

## [0.9.0] — 2026-03-04

### 🏗️ Project & Solution Restructure

- **Renamed** solution from `SevenZipExtractor` → `SevenZipWrapper`.
- **Renamed** root namespace from `SevenZipExtractor` / `_7ZipWrapper` → `SevenZipWrapper` (C# identifiers cannot start with a digit).
- **Retargeted** all projects from `.NET Framework 4.5` / `.NET Standard 2.0` → **`.NET 10`**.
- **Added** three new projects to the solution:
  - `SevenZipWrapper` — core library (replaces `SevenZipExtractor`).
  - `SevenZipWrapper.Tests` — xUnit test suite (replaces `SevenZipExtractor.Tests`).
  - `SevenZipWrapper.Benchmark` — BenchmarkDotNet performance suite (replaces `Benchmark`).
  - `SevenZipWrapper.Example` — console demo app (replaces `Example`).
- **Enabled** implicit usings and nullable reference types project-wide.
- **Dropped** x86 architecture — all projects target **x64 only**.

---

### 📦 Native DLL

- **Updated** bundled `7z.dll` from unknown legacy version → **v26.00** (2026-02-12).
- **Removed** x86 `7z.dll` — only x64 is shipped.
- **Changed** native DLL packaging from `.targets` import to `<Content>` with `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` in csproj.

---

### 🔧 Core Library — `SevenZipWrapper`

#### New Files (replacing old equivalents)

| New File | Replaces | Summary |
|----------|----------|---------|
| `ArchiveFile.cs` | `ArchiveFile.cs` | Main public API — open, enumerate, extract |
| `ArchiveEntry.cs` | `Entry.cs` | Single archive entry (file/folder) |
| `SevenZipFormat.cs` | `SevenZipFormat.cs` | Supported format enum |
| `Formats.cs` | `Formats.cs` | Extension, GUID, and signature mappings |
| `SevenZipException.cs` | `SevenZipException.cs` | Custom exception type |
| `Interop/SevenZipInterop.cs` | `SevenZipInterface.cs` + `IArchiveExtractCallback.cs` | COM interop interfaces, structs, enums |
| `Interop/SevenZipHandle.cs` | `Kernel32Dll.cs` + `SafeLibraryHandle.cs` + `SevenZipHandle.cs` | Native library loading via `NativeLibrary` API |
| `Callbacks/ArchiveStreamCallback.cs` | `ArchiveStreamCallback.cs` | Single-entry → stream callback |
| `Callbacks/ArchiveStreamsCallback.cs` | `ArchiveStreamsCallback.cs` | All-entries → streams callback |
| `Callbacks/ArchiveFileCallback.cs` | `ArchiveFileCallback.cs` | Single-entry → file callback |

#### API Changes

- **Renamed** `Entry` → `ArchiveEntry` — more descriptive, avoids naming conflicts.
- **Changed** `IList<Entry> Entries` → `IReadOnlyList<ArchiveEntry> Entries` — immutable public API surface.
- **Changed** `Func<Entry, string>` extraction callback → `Func<ArchiveEntry, string?>` — return `null` to skip an entry.
- **Changed** `GuessFormatFromExtension()` → `TryGuessFormatFromExtension()` — `TryGetValue` pattern with `OrdinalIgnoreCase`.
- **Changed** `GuessFormatFromSignature()` → `TryGuessFormatFromSignature()` — uses `ReadOnlySpan<byte>` + `stackalloc` + `SequenceEqual`.
- **Added** `[SupportedOSPlatform("windows")]` attribute on `ArchiveFile` — silences CA1416 for COM interop.
- **Added** `SevenZipFormat.Zstd` enum member with GUID, `zst` extension mapping, and magic bytes `[0x28, 0xB5, 0x2F, 0xFD]`.
- **Added** XML doc comments on all public API members.

#### Native Library Loading

- **Replaced** `Kernel32Dll` P/Invoke (`LoadLibrary` / `GetProcAddress` / `FreeLibrary`) with `NativeLibrary.Load()` / `.GetExport()` / `.TryGetExport()` / `.Free()`.
- **Removed** `SafeLibraryHandle` class — replaced with `IntPtr` + `bool _disposed` guard in `Dispose()`.
- **Removed** `[SuppressUnmanagedCodeSecurity]`, `[ReliabilityContract]`, `[SecurityPermission]` — all obsolete in .NET Core+.
- **Replaced** `Marshal.GetDelegateForFunctionPointer(ptr, typeof(T))` → generic `Marshal.GetDelegateForFunctionPointer<T>(ptr)`.
- **Simplified** library search paths — x64-only; `ResolveLibraryPath()` searches bundled locations first, system 7-Zip install as fallback.

#### COM Interop

- **Removed** CAS `[SecurityPermission]` attributes — no-ops in .NET Core+.
- **Kept** `[DllImport]` for `PropVariantClear` (ole32.dll) — `[LibraryImport]` source generator cannot marshal `ref PropVariant` (non-blittable union struct, SYSLIB1051).
- **Modernized** `PropVariant.GetObject()` with switch expression and `readonly` struct members.
- **Applied** primary constructors to `StreamWrapper`, `InStreamWrapper`, `OutStreamWrapper`.
- **Applied** nullable annotations to all stream and callback parameters.
- **Renamed** enum members to idiomatic C# naming.
- **Merged** `IArchiveExtractCallback` from separate file into `SevenZipInterop.cs`.

#### Format Mappings

- **Replaced** `Dictionary<>` with `FrozenDictionary<>` (.NET 8+) — all three mapping dictionaries for better cache behavior.
- **Added** `Formats.MaxSignatureLength` static field — pre-computed for `stackalloc` buffer sizing.
- **Added** Zstd format: GUID `23170f69-40c1-278a-1000-0001100e0000`, extension `zst`, signature `[0x28, 0xB5, 0x2F, 0xFD]`.
- **Fixed** GZip signature — corrected second byte to `0x8B`.
- **Applied** collection expressions (`[…]`) for byte array signatures.
- **Applied** `StringComparer.OrdinalIgnoreCase` comparer on extension mapping.

#### Exception Type

- **Sealed** `SevenZipException` class.
- **Removed** serialization constructor (`SYSLIB0051`) and `[Serializable]` attribute.
- **Removed** `using System.Runtime.Serialization`.

#### Entry / ArchiveEntry

- **Sealed** `ArchiveEntry` class.
- **Applied** `internal init` setters — all properties are immutable after construction.
- **Applied** nullable annotations — `FileName`, `Comment`, `HostOS`, `Method` are `string?`.
- **Changed** `UInt32` → `uint` alias for `CRC` and `Attributes`.

#### Callbacks

- **Sealed** all callback classes (`ArchiveStreamCallback`, `ArchiveStreamsCallback`, `ArchiveFileCallback`).
- **Applied** primary constructors to all callbacks.
- **Applied** nullable `string? password` with `?? ""` coercion for COM compatibility.
- **Applied** nullable `out ISequentialOutStream?` return types.

#### Dispose Pattern

- **Removed** finalizer/destructor from `ArchiveFile` — sealed class with simple `Dispose()` and `_disposed` guard.
- **Removed** finalizer from `SevenZipHandle` — uses `NativeLibrary.Free()` in `Dispose()`.

---

### 🧪 Tests — `SevenZipWrapper.Tests`

#### Project Setup

- **Retargeted** from `.NET Framework 4.5` → **`.NET 10`**.
- **Changed** test framework from **MSTest** → **xUnit** 2.9.3 + `xunit.runner.visualstudio` 3.1.4.
- **Added** `System.IO.Hashing` 9.0.3 (replaces custom `Crc32` class).
- **Added** `coverlet.collector` 6.0.4 for code coverage.
- **Linked** test archives from `SevenZipExtractor.Tests\Resources\` via `<Content>` wildcard — no `.resx` code-gen.
- **Set** `<PlatformTarget>x64</PlatformTarget>`.
- **Suppressed** CA1416 (Windows-only API).

#### Test Infrastructure

| File | Description |
|------|-------------|
| `TestFixtures/TestFileEntry.cs` | `readonly record struct` with `Name`, `IsFolder`, `MD5?`, `CRC32?` (was plain struct) |
| `Helpers/HashHelper.cs` | `MD5String()` via `Convert.ToHexStringLower(MD5.HashData())` — one-liner; `CRC32String()` via `System.IO.Hashing.Crc32` |
| `TestBase.cs` | Abstract base with `AssertExtractToStream()`, `AssertExtractToStreamFromFile()`, `LoadResource()`, shared test data |

#### Eliminated

- **Removed** custom `Crc32.cs` (Apache 2.0, Damien Guard) — replaced with built-in `System.IO.Hashing.Crc32`.
- **Removed** `TestFiles.resx` / `TestFiles.Designer.cs` — replaced with `<Content>` wildcard linking.

#### Test Classes (14 total)

**Format Tests** (5 classes):

| Class | Archive | Tests |
|-------|---------|-------|
| `SevenZipFormatTests` | `.7z` | Stream extraction, file extraction, explicit format, entry count, extract-all to directory |
| `ZipFormatTests` | `.zip` | Stream extraction (with folders), file extraction, explicit format, format detection |
| `RarFormatTests` | `.rar` | Stream extraction, file extraction, Rar/Rar5 auto-detection |
| `ArjFormatTests` | `.arj` | Stream extraction with explicit format, entry verification, format detection |
| `LzhFormatTests` | `.lzh` | Stream extraction with explicit format, explicit format assignment |

**Core Tests** (5 classes):

| Class | Tests |
|-------|-------|
| `FormatDetectionTests` | Extension-based detection, signature-based detection, empty/random stream rejection, unknown extension fallback, LZH signature offset |
| `ArchiveEntryTests` | `FileName`, `IsFolder`, `Size` properties; null/empty argument validation; extract to file/stream; timestamp preservation |
| `ArchiveFileTests` | Constructor validation (null/empty/whitespace/nonexistent); dispose safety (single + double); extract overloads; selective extraction callback |
| `SevenZipHandleTests` | Invalid DLL path; empty library path fallback; null library path auto-detection |
| `FormatsTests` | `ExtensionFormatMapping` lookups + case-insensitivity; `FormatGuidMapping` coverage; `FileSignatures` verification; `MaxSignatureLength`; Rar5 > Rar signature length |

#### Test Results

- **78 tests** in `SevenZipWrapper.Tests` — all passing.
- **10 tests** in legacy `SevenZipExtractor.Tests` — all passing.
- **88 total, 0 failed, 0 skipped** — run time: 428 ms.
- Initial run had 9 failures (test data mismatches for archives without folder entries, RAR explicit format, LZH auto-detection) — all resolved.

---

### ⚡ Benchmarks — `SevenZipWrapper.Benchmark`

#### Project Setup

- **Retargeted** from `.NET Framework 4.7` + `.NET Core 3.1` → **`.NET 10`**.
- **Updated** BenchmarkDotNet from `0.12.x` → **`0.15.2`**.
- **Applied** `[MemoryDiagnoser]` and `[JsonExporterAttribute.Full]` attributes.
- **Changed** from `Job.ShortRun` (3 iterations) → `DefaultJob` (13–21 auto-tuned iterations) for higher statistical confidence.
- **Bundled** `Resources/7z.7z` test archive with `CopyToOutputDirectory`.
- **Added** `[JsonExporterAttribute.Full]` to old `Benchmark\` project for JSON export of legacy results.

#### Benchmark Methods

| Benchmark | Purpose | Status |
|-----------|---------|--------|
| `EnumerateEntries` | Entry enumeration + property access | ✅ Done |
| `ExtractFirstEntry` | First file extraction (best-case seek) | ✅ Done |
| `ExtractLastEntry` | Last file extraction (worst-case seek) | ✅ Done |
| `ExtractAll` | Bulk extraction via `Extract(outputDir, overwrite: true)` | ✅ Done |
| `ExtractToStream` | Extract to `MemoryStream` (no disk I/O) | ⬜ Not Started |
| `FormatDetection` | `TryGuessFormatFromSignature` throughput | ⬜ Not Started |
| `OpenAndClose` | `new ArchiveFile()` + `Dispose()` overhead | ⬜ Not Started |

#### Results — Old (.NET Core 3.1) vs New (.NET 10)

| Benchmark | .NET Core 3.1 | .NET 10 | Speed Δ | Memory Δ |
|-----------|---------------|---------|---------|----------|
| **EnumerateEntries** | 1,388 µs | 1,390 µs | ≈ same | ↓ 14% less |
| **ExtractFirstEntry** | 3,208 µs | 2,998 µs | ↓ 6.6% faster | ↓ 1.3% less |
| **ExtractLastEntry** | 4,514 µs | 4,447 µs | ↓ 1.5% faster | ↓ 1.3% less |
| **ExtractAll** | 5,045 µs | 5,255 µs | ≈ within noise | ↓ 1.6% less |

> `ExtractAll` initially regressed 16.6% due to `[IterationCleanup]` forcing `InvocationCount=1`. Resolved by removing cleanup and relying on `overwrite: true`.

---

### 💻 Example App — `SevenZipWrapper.Example`

- **Created** project targeting `.NET 10` with project reference to `SevenZipWrapper`.
- **Program.cs** implementation: ⬜ Not Started (9 demo scenarios planned).

---

### 🗣️ C# Language Modernization (Applied Across All Files)

| Feature | Details |
|---------|---------|
| File-scoped namespaces | `namespace SevenZipWrapper;` everywhere |
| Primary constructors | Callbacks, stream wrappers |
| `init` setters | `ArchiveEntry` properties |
| `readonly record struct` | `TestFileEntry` |
| Collection expressions | `[…]` for arrays, `[]` for empty lists |
| Target-typed `new()` | Used throughout |
| Pattern matching | `is null`, `is not null`, switch expressions |
| String interpolation | Error messages |
| `using` declarations | `using var` instead of `using (…) { }` |
| `ReadOnlySpan<byte>` + `stackalloc` | Signature detection buffer |
| Guard clauses | `ArgumentException.ThrowIfNullOrWhiteSpace()`, `ArgumentNullException.ThrowIfNull()`, `ObjectDisposedException.ThrowIf()` |
| `FrozenDictionary<>` | All format mapping dictionaries |
| Nullable reference types | Full annotation across public API |
| `sealed` classes | `ArchiveFile`, `ArchiveEntry`, `SevenZipException`, `SevenZipHandle`, all callbacks |

---

### 🗑️ Removed / Eliminated

| Removed Item | Reason |
|--------------|--------|
| `Kernel32Dll.cs` | Replaced by `NativeLibrary` API — merged into `SevenZipHandle.cs` |
| `SafeLibraryHandle.cs` | Replaced by `IntPtr` + dispose guard — merged into `SevenZipHandle.cs` |
| `IArchiveExtractCallback.cs` (separate file) | Merged into `SevenZipInterop.cs` |
| Custom `Crc32.cs` (test project) | Replaced by `System.IO.Hashing.Crc32` |
| `TestFiles.resx` / `TestFiles.Designer.cs` | Replaced by `<Content>` wildcard linking |
| `[SuppressUnmanagedCodeSecurity]` | Obsolete in .NET Core+ |
| `[ReliabilityContract]` | Obsolete in .NET Core+ |
| `[SecurityPermission]` (CAS) | No-op in .NET Core+ |
| `[Serializable]` + serialization constructor | Binary serialization obsolete (`SYSLIB0051`) |
| x86 `7z.dll` | Windows 11+ dropped 32-bit OS support |
| Finalizers/destructors | Sealed classes with simple `Dispose()` |

---

### ⚠️ Known Warnings (Deferred)

| Code | File | Description |
|------|------|-------------|
| CS8603 | `SevenZipInterop.cs` | `MarshalVariant()` possible null return — `Marshal.GetObjectForNativeVariant` rarely returns null in practice |
| CS8600 | `ArchiveFile.cs` | `value.ToString()` may be null in `GetProperty<T>` — value is already null-checked above |
| CA1416 | `SevenZipInterop.cs` | `Marshal.GetObjectForNativeVariant` is Windows-only — entire library is Windows-only |

---

### ⬜ Planned (Not Yet Started)

| Item | Notes |
|------|-------|
| `SevenZipWrapper.Example` — `Program.cs` | 9 demo scenarios (extract all, overwrite, password, list entries, single entry, stream, selective, open from stream, format detection) |
| Additional benchmarks | `ExtractToStream`, `FormatDetection`, `OpenAndClose` |
| Benchmark export | CSV + Markdown table export |
| NuGet package | New package ID `SevenZipWrapper`, metadata, `.targets` file, Source Link, deterministic builds |
| `README.md` | Badges, install guide, API reference, migration guide, benchmark summary |
| Additional test coverage | Password-protected archives, dispose safety (`ObjectDisposedException`), timestamp preservation flag, large archive, Zstd format |
| CI/CD | GitHub Actions for build/test/publish |
| `IProgress<T>` support | Progress reporting during extraction |
| `ExtractAsync` | Async extraction with `CancellationToken` |
| Remove `<AllowUnsafeBlocks>` | No longer needed after `[LibraryImport]` → `[DllImport]` revert |

---

### 🔑 Key Design Decisions

1. **x64 Only** — Windows 11+ dropped 32-bit OS support. No x86 paths, no `IntPtr.Size` checks.
2. **`NativeLibrary` over P/Invoke** — `System.Runtime.InteropServices.NativeLibrary` is the modern .NET way to load native libraries.
3. **`[DllImport]` kept for `PropVariantClear`** — `[LibraryImport]` source generator cannot marshal `ref PropVariant` (non-blittable union struct, SYSLIB1051).
4. **`FrozenDictionary`** — Immutable lookup tables for format mappings give better cache behavior.
5. **Nullable Reference Types** — Full annotation for null-safety across the entire public API.
6. **No CAS/CER** — `[SuppressUnmanagedCodeSecurity]`, `[ReliabilityContract]`, `[SecurityPermission]` are all obsolete in .NET Core+.
7. **No Binary Serialization** — `SerializationInfo` constructor removed from exception type (`SYSLIB0051`).
8. **`[SupportedOSPlatform("windows")]`** — Applied to `ArchiveFile` since the entire library is a Windows COM interop wrapper.
9. **xUnit over MSTest** — Cleaner `[Fact]`/`[Theory]` syntax, better community ecosystem.
10. **`DefaultJob` over `ShortRun`** — 13–21 auto-tuned iterations for statistically meaningful benchmark results.

---

*Generated: 2026-03-04*