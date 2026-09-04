# API contracts

`ArchiveFile` is disposable and owns its native archive and library handles. Existing path and stream constructors remain available. Advanced opening uses `ArchiveFile.Open(pathOrStream, new ArchiveOpenOptions { ... })`, avoiding ambiguous optional constructor overloads.

Inputs must be readable and seekable. The default stream constructor transfers ownership, including cleanup if construction fails. `LeaveOpen = true` preserves the caller stream on failure and disposal. Probing restores the initial position in a `finally` block; later native operations may move it. Non-seekable inputs are rejected rather than copied into memory or a temporary file.

Entry enumeration opens the native archive lazily, returns an immutable cache, and participates in the same per-instance gate as extraction. Entries share their owner's gate and reject extraction after owner disposal. `Dispose` waits for active work, prevents new operations, closes the native archive, and releases resources in dependency order. Reentrant calls from callbacks, including inherited async execution contexts, throw `InvalidOperationException`; do not synchronously wait for unrelated work that itself needs the same archive.

## Errors and results

`SevenZipException` is the archive-specific base. `ArchiveExtractionException` and `SevenZipNativeException` expose an `ArchiveFailure` through `Failure`. The shared `FailureKind` distinguishes archive/data/CRC/method/password/path/conflict/resource/native/output failures. Native HRESULT and operation result are preserved when available. Unknown native outcomes stay `Unknown` rather than being guessed as corruption or a wrong password.

Argument validation, disposal, cancellation, and original exceptions thrown by caller callbacks/streams preserve .NET semantics. Native callbacks capture caller exceptions, abort safely and rethrow after unwinding. Missing metadata retains a default value; incompatible or overflowing metadata produces a controlled native diagnostic. Metadata is not converted via culture-dependent strings.

Rich result APIs handle archive extraction failures; invalid API arguments, failure to open/enumerate an archive, cancellation and caller exceptions can still throw. `ExtractionResult.Entries` is immutable. `Succeeded` considers both operation-level and per-entry failure information.

## Async and passwords

`ExtractAsync` and `ExtractWithResultAsync` use `Task.Run` over the synchronous core. `ArchiveEntry` also offers async stream/file extraction. Options carry cancellation. Same-instance async work serializes; use separate instances for true parallel work.

Password support remains **experimental** under the explicitly permitted 1.0 fallback. `ArchiveOpenOptions.Password` supplies open-time credentials, including requests while opening encrypted headers; extraction options can override the stored password. Bulk and entry extraction share credential handling. Null means no supplied password; an empty string is a supplied value. Native missing-password and explicit wrong-password outcomes are distinguishable, but the full real-archive header/content correctness matrix is not certified as stable. A CRC/data failure is not automatically labeled a wrong password.

Passwords are omitted from option formatting and diagnostic records. Managed strings are retained for the archive lifetime as needed; secure erasure is not promised.

For a single caller-owned output stream, use `entry.ExtractWithOptions(stream, options)` for extraction policies. The named companion preserves the existing `entry.Extract(stream, null)` password call without overload ambiguity. A configured whole-archive count limit is checked before materializing entry metadata; a rich result may therefore have an operation-level resource failure and no entry list.
