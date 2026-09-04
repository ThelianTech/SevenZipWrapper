namespace SevenZipWrapper;

/// <summary>Settings for opening an archive. Password support is experimental.</summary>
public sealed class ArchiveOpenOptions
{
    public SevenZipFormat? Format { get; init; }
    public string? LibraryFilePath { get; init; }
    public string? Password { get; init; }
    /// <summary>Keep a caller stream open after disposal. Its final position is not guaranteed.</summary>
    public bool LeaveOpen { get; init; }
}
