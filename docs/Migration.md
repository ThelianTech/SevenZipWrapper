# Migrating from 0.9.x to 1.0

Common constructors and extraction calls remain source-compatible. Behavioral hardening is intentional:

- Rooted extraction rejects unsafe names, reparse destinations and duplicate targets instead of writing outside the root or silently replacing archive entries. Use `UnsafePathPolicy.Skip` only when an intentional skip is appropriate.
- `Overwrite = false` retains skip-existing behavior with exclusive creation. Overwrite does not authorize archive-internal duplicates. Explicit entry-file calls retain their historical overwrite default; options-based entry-file calls default to no overwrite.
- Native HRESULT, per-entry CRC/data/method failures, output failures and limits now make normal extraction fail. Catch the base `SevenZipException` or inspect specialized exceptions and their `Failure` records. Use `ExtractWithResult` for per-entry accounting.
- Partial outputs are non-transactional. If a workflow needs all-or-nothing installation, extract into a caller-owned staging directory and perform the application commit separately.
- Stream constructors still own input streams by default. Use `ArchiveFile.Open(stream, new ArchiveOpenOptions { LeaveOpen = true })` to retain ownership. Unsupported non-seekable/unreadable streams now fail immediately.
- Operations and disposal serialize per instance, including entries. Nested callback calls into that instance throw. Separate archives allow parallelism.
- Async methods use the same core with cooperative cancellation. No custom scheduler or true native asynchronous I/O is implied.
- Signature evidence wins against contradictory extensions. `Undefined`, `Msi`, and invalid enum values fail as unsupported rather than leaking mapping exceptions. See the format matrix for verified versus engine-exposed support.
- Native loading uses explicit or application-local paths and no longer falls back to an unrelated system 7-Zip installation. The package supplies its controlled Windows x64 engine.
- Password options include archive-open credentials, but support remains experimental until the complete encrypted fixture matrix is verified.

The supported release boundary is Windows x64 with .NET 10. Existing historical changelog and benchmark entries describe the releases they measured, not current release certification.
