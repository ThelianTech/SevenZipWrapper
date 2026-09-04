namespace SevenZipWrapper;

public enum UnsafePathPolicy { Throw, Skip }
public enum ExtractionFailureMode { FailFast, ContinueAndReport }

/// <summary>Optional extraction policy. No application-specific quotas are imposed by default.</summary>
public sealed record ExtractionOptions
{
    public bool Overwrite { get; init; }
    public UnsafePathPolicy UnsafePathPolicy { get; init; }
    public ExtractionFailureMode FailureMode { get; init; }
    public string? Password { get; init; }
    public int? MaxEntryCount { get; init; }
    public ulong? MaxEntrySize { get; init; }
    public ulong? MaxTotalSize { get; init; }
    public double? MaxCompressionRatio { get; init; }
    public int? MaxPathDepth { get; init; }
    public Action<int>? Progress { get; init; }
    public CancellationToken CancellationToken { get; init; }

    // Record-generated ToString would disclose Password.
    public override string ToString() => nameof(ExtractionOptions);

    internal void Validate()
    {
        if (!Enum.IsDefined(UnsafePathPolicy)) throw new ArgumentOutOfRangeException(nameof(UnsafePathPolicy));
        if (!Enum.IsDefined(FailureMode)) throw new ArgumentOutOfRangeException(nameof(FailureMode));
        if (MaxEntryCount < 0) throw new ArgumentOutOfRangeException(nameof(MaxEntryCount));
        if (MaxPathDepth < 0) throw new ArgumentOutOfRangeException(nameof(MaxPathDepth));
        if (MaxCompressionRatio is { } ratio && (!double.IsFinite(ratio) || ratio <= 0))
            throw new ArgumentOutOfRangeException(nameof(MaxCompressionRatio));
    }
}
