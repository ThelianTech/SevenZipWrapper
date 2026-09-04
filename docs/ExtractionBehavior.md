# Extraction behavior

All extraction facades use the same synchronous core. Normal APIs throw an `ArchiveExtractionException` for a required extraction failure. `ExtractWithResult` and its async counterpart report structured per-entry statuses and an optional operation-level failure. Always check `Succeeded`; intentional policy skips count as successful handling, while failed required entries do not. Preflight failure may prevent all writes. Entries not attempted after fail-fast are marked skipped with an explicit reason.

`Overwrite = false` skips an ordinary existing file and uses exclusive creation at the actual write point. `Overwrite = true` may replace an existing ordinary file. Duplicate archive targets still fail, including case and normalization aliases; file/directory conflicts fail. Selector-based multi-entry extraction also rejects duplicate selected targets. Explicit single-entry destinations come from the caller and are not derived from the archive name.

## Rooted path protection

Rooted extraction normalizes directory boundaries with Windows case semantics. Traversal outside the root, rooted names, drive-relative names, UNC entry names, alternate data streams, reserved device names, trailing-dot/space aliases and unsafe reparse destinations are rejected. `UnsafePathPolicy.Skip` reports unsafe entries rather than extracting them. Harmless interior `.`/`..` normalization is allowed but participates in duplicate detection. No automatic renaming or unsafe mode exists.

The Windows output helper opens descendants one component at a time relative to held directory handles, inspects actual handles before truncation, and holds directory leases against relocation. It rejects existing hard-linked overwrite targets and does not restore archive links. Another process independently creating a new outside hard link is outside the archive-path guarantee; filesystem sharing cannot prevent that external operation.

## Limits and cancellation

`ExtractionOptions` offers `MaxEntryCount`, `MaxEntrySize`, `MaxTotalSize`, `MaxCompressionRatio`, and `MaxPathDepth`. Defaults do not impose application quotas. Sizes and counts use checked arithmetic. Preflight checks metadata; byte limits are also checked before each output write. Ratio checks use reported packed size, so their precision depends on format metadata. A positive expanded size with a reported zero packed size exceeds a configured ratio limit. Depth counts archive path components. No CPU, disk-space reservation, or OS memory policy is supplied.

Cancellation checks occur before work, during gate waits, between entries, in native callbacks, and during output writes. A long native call may take time to reach a cancellation point. Cancellation raises `OperationCanceledException`, including from result APIs.

## Partial output

This library is not a filesystem transaction. Earlier successful files and created directories may remain after failure. It attempts to mark the incomplete current owned file for deletion using its verified handle. This does not restore an overwritten original. Caller-provided output streams remain caller-owned and are not rolled back or truncated after failure.

`ContinueAndReport` permits explicit best effort for archive and wrapper-owned output failures. Caller callback exceptions and cancellation stop the operation and retain their normal exception semantics. Progress counts completed non-directory files and executes synchronously with the operation, on a worker thread for async APIs. A progress exception does not erase an already completed file.
