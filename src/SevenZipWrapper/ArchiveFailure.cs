namespace SevenZipWrapper;

/// <summary>Stable categories shared by exceptions and extraction results.</summary>
public enum FailureKind
{
    Unknown, InvalidArchive, UnsupportedFormat, UnsupportedMethod, DataError, CrcError,
    MissingPassword, InvalidPassword, UnsafePath, DestinationConflict, ResourceLimitExceeded,
    NativeLibraryFailure, NativeInteropFailure, OutputFailure
}

/// <summary>Diagnostic context for an archive operation. Never contains credentials.</summary>
public sealed record ArchiveFailure(FailureKind Kind, string Message, string? EntryName = null,
    int? NativeHResult = null, int? NativeOperationResult = null);

public sealed class ArchiveExtractionException(ArchiveFailure failure, Exception? innerException = null)
    : SevenZipException(failure, innerException);

public sealed class SevenZipNativeException(ArchiveFailure failure, Exception? innerException = null)
    : SevenZipException(failure, innerException);

public enum ExtractionStatus { Succeeded, Skipped, Failed }

public sealed record EntryExtractionResult(ArchiveEntry Entry, ExtractionStatus Status,
    ArchiveFailure? Failure = null, string? SkipReason = null);

public sealed class ExtractionResult
{
    internal ExtractionResult(IEnumerable<EntryExtractionResult> entries, ArchiveFailure? failure = null)
    {
        Entries = Array.AsReadOnly(entries.ToArray());
        Failure = failure;
    }

    public IReadOnlyList<EntryExtractionResult> Entries { get; }
    public ArchiveFailure? Failure { get; }
    public bool Succeeded => Failure is null && Entries.All(e => e.Status != ExtractionStatus.Failed);
}
