# Release process and 1.0 traceability

## Supported release gate

Set the release version once in the repository-root `Directory.Build.props` using `Version`.
All projects inherit it; the SDK derives the NuGet package version and assembly/file
versions from that value. Build, publish, and pack use the same source. Prerelease suffixes
remain in package/informational versions; assembly/file versions are numeric.
For a temporary override, pass `-p:Version=...` consistently to build and pack, and pass
the same value as `-Version ...` to `Verify-Package.ps1`. `pack --no-build` must use
assemblies built with that same version.

The verification script evaluates `PackageId` and `PackageVersion` from MSBuild, including
`Directory.Build.props`, and finds the matching package in `artifacts/packages` by default.
Use `-PackageDirectory` for another output folder, or `-PackagePath` for an explicit file;
its embedded identity and version must still match the build. `-Configuration` defaults to Release.

Use Windows x64 and the exact SDK in `global.json`. Dependencies are committed in each project's `packages.lock.json`. The workflow in `.github/workflows/ci.yml` performs locked restore, clean Release build with warnings as errors, tests with coverage, pack, strict package inspection, and isolated consumer build/publish smoke tests. It retains evidence without publishing anything.

```powershell
dotnet restore SevenZipWrapper.slnx --locked-mode
dotnet clean SevenZipWrapper.slnx -c Release
dotnet build SevenZipWrapper.slnx -c Release --no-restore
dotnet test src/SevenZipWrapper.Tests/SevenZipWrapper.Tests.csproj -c Release --no-build --no-restore --collect:"XPlat Code Coverage" --logger "trx;LogFileName=tests.trx" --results-directory artifacts/test-results
dotnet pack src/SevenZipWrapper/SevenZipWrapper.csproj -c Release --no-build --no-restore -o artifacts/packages
./scripts/Verify-Package.ps1
```

Normal Windows permissions are sufficient for the test fixtures. An agent filesystem sandbox may reject handle-relative native I/O even where the underlying Windows account has permission; tests must exercise the actual Windows host rather than replacing the safety primitive to accommodate the sandbox.

`NU5100` is suppressed only for this library's deliberate native content asset; it is not a managed assembly reference. Other meaningful compiler/analyzer/restore warnings fail the build. The original content/contentFiles package mechanism is retained because clean consumer build and framework-dependent publish both copied and resolved the native DLL correctly. No speculative runtime-asset migration was performed.

The package verifier uses an isolated NuGet cache with only the produced package source, checks an exact content whitelist, verifies native checksum/version/x64 PE architecture and upstream license text, then opens and extracts a generated ZIP through the packaged library from both build and publish output. It fails on unexpected files or a mismatched native binary. The script does not download a native engine during CI.

The bundled DLL is unmodified 7-Zip 26.02. Its checksum was matched to `7z.dll` extracted from the official x64 installer without running the installer. `scripts/native-provenance.json` records upstream artifact identities and verification data. [Official release](https://github.com/ip7z/7zip/releases/tag/26.02). SDK pinning and dependency locking follow the [.NET SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) and [NuGet lock-file](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) contracts.

## Decision evidence

Paths below are relative to the repository. Core source is under `src/SevenZipWrapper`; behavioral tests are under `src/SevenZipWrapper.Tests/CoreTests` unless otherwise noted.

| Decision | Implementation evidence | Direct verification | Documentation | Status |
|---|---|---|---|---|
| 1 Safe roots | RootedPath, RootedOutputSession, ArchiveFile.Extraction | RootedPathTests, ExtractionIntegrityTests (actual hostile ZIP, junction mutation, rename, outside sentinels) | ExtractionBehavior | Complete |
| 2 Whole-operation outcomes | canonical ExtractCore, callbacks, ExtractionResult | ExtractionIntegrityTests, FailureIntegrationTests, native CRC fixture | ExtractionBehavior, ApiBehavior | Complete |
| 3 Resource policies | ExtractionBudget, ExtractionOptions, enumeration count checks | quota and runtime-byte cases in ExtractionIntegrityTests; metadata overflow in NativeBoundaryTests | ExtractionBehavior | Complete |
| 4 Passwords | ArchiveOpenOptions, ArchivePasswordCallback, shared extraction credentials | native request null/empty and wrong-password result mapping tests | ApiBehavior, README experimental label | Explicitly permitted experimental fallback; full encrypted-fixture certification deferred |
| 5 Streams/lifetime | ArchiveFile construction/disposal, stream wrappers, LeaveOpen | ArchiveLifecycleTests, FailureIntegrationTests | ApiBehavior, Migration | Complete |
| 6 Serialized concurrency | OperationGate, owner-bound ArchiveEntry | ArchiveLifecycleTests, AsyncContractTests with actual native work | ApiBehavior | Complete |
| 7 Formats | Formats, validated opening, reliable leading probe | DetectionContractTests, FormatsTests, FormatDetectionTests; FormatTests fixtures | CompatibilityAndFormats | Complete |
| 8 Native boundary | NativeBoundary, PropVariant, SevenZipHandle, callbacks | NativeBoundaryTests, FailureIntegrationTests, real extraction suites | ApiBehavior | Complete |
| 9 Release engineering | global.json, lock files, Directory.Build.props, CI, Verify-Package.ps1 | local clean pipeline, coverage, package and consumer checks | This document | Complete; remote workflow has not been dispatched |
| 10 Async/cancellation | Task facades over ExtractCore and shared gate | AsyncContractTests, ExtractCancellationTests | ApiBehavior, ExtractionBehavior | Complete |
| 11 Documentation | README and focused docs, unreleased changelog | final link/claim reconciliation | README, Migration, this document | Complete |
| 12 Risk-driven tests | one primary test project plus separate package verifier | security, corruption, quotas, callbacks, streams, async/concurrency, mappings and package cases | This table | Complete; encryption uses Decision 4 fallback |
| 13 Platform/native packaging | Windows x64 guard, net10.0, pinned native DLL, contentFiles | PE/version/hash checks and clean build/publish consumer | CompatibilityAndFormats, third-party notices | Complete |
| 14 Destination conflicts | normalized preflight, exclusive native creation, physical target tracking | RootedPathTests, CallbackPathConflictTests, ExtractionIntegrityTests | ExtractionBehavior, Migration | Complete |
| 15 Failure/result model | ArchiveFailure, exception families, EntryExtractionResult | NativeBoundaryTests, ExtractionIntegrityTests, AsyncContractTests | ApiBehavior, Migration | Complete |
| 16 Final scope gate | required decisions above; no installer/staging/rollback layer | final clean restore/build/test/pack/consumer evidence | This document | Complete with the permitted Decision 4 fallback |

## Benchmarks

The existing four whole-archive extraction variants completed a BenchmarkDotNet Dry job using the bundled `7z.7z` fixture. Results are in `artifacts/benchmarks/results`. One cold-start sample per method is a smoke measurement, not statistically reliable performance evidence or a comparison with old runs. The large historical `LT_Nemesis.7z` fixture is not present; the benchmark now uses the shipped fixture. Output directories are unique per run.

```powershell
dotnet run --project src/SevenZipWrapper.Benchmark -c Release -- --filter '*ExtractAll*' --job Dry --artifacts artifacts/benchmarks
```

Remaining optional work includes fuller encrypted-archive certification, additional format fixtures, broader platform investigation, archive creation, and publication/signing automation. These do not add requirements beyond the locked 1.0 scope and its permitted experimental fallback.

## Final verification record

**READY FOR OWNER PUBLISH STEP**, with encrypted-archive support explicitly experimental under Decision 4. No unresolved required implementation or local verification blocker remains.

Verified on Windows x64 with .NET SDK 10.0.400:

- Locked restore and clean Release build passed; zero compiler/analyzer warnings and errors.
- Full suite: 208 passed, 0 failed, 0 skipped. This includes the final source-compatible null-password call and configured count limit before entry materialization.
- Coverage: 828/912 lines (90.78%) and 508/612 branches (83.00%). No percentage threshold substitutes for the direct regression cases above.
- Final package: `artifacts/packages/SevenZipWrapper.1.0.0.nupkg`.
- Strict package contents, native 26.02 version, x64 PE architecture, checksum and upstream license checks passed.
- Isolated package-only restore, locked restore, consumer build, framework-dependent publish and both extraction smoke runs passed.
- Four existing extraction benchmarks completed their Dry job. Results are smoke observations only; the large historical fixture was unavailable.
- Independent native/lifecycle and path/core reviews produced fixes and passing regressions, including callback exception identity, native failures, duplicate selector targets, handle-based partial-file cleanup and disposal reentrancy.

Evidence: `artifacts/test-results/tests.trx`, `artifacts/test-results/4143b312-5446-4f29-a3bb-58373541848f/coverage.cobertura.xml`, and `artifacts/package-verification/f91133de25fc405a8f481661e567abfc/verification.json`. These generated artifacts are ignored and do not ship in the NuGet package.

The CI workflow is configured and its command sequence was exercised locally; it has not been dispatched on GitHub. Publication remains a separate owner action. Codex did not commit, push, tag or publish. The owner's existing source/docs reorganization was preserved; build outputs already tracked by the repository changed during verification and were not removed from the index.
