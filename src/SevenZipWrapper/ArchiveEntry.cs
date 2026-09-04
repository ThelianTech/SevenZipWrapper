namespace SevenZipWrapper;

/// <summary>
/// Represents a single entry (file or folder) inside a 7-Zip archive.
/// </summary>
public sealed class ArchiveEntry
{
    private readonly ArchiveFile _owner;
    private readonly uint _index;
    internal uint Index => _index;

    internal ArchiveEntry(ArchiveFile owner, uint index)
    {
        _owner = owner;
        _index = index;
    }

    /// <summary>
    /// Name of the file with its relative path within the archive.
    /// </summary>
    public string? FileName { get; internal init; }

    /// <summary>
    /// <see langword="true"/> if the entry is a folder; <see langword="false"/> if it is a file.
    /// </summary>
    public bool IsFolder { get; internal init; }

    /// <summary>
    /// Uncompressed size of the entry in bytes.
    /// </summary>
    public ulong Size { get; internal init; }

    /// <summary>
    /// Compressed size of the entry in bytes.
    /// </summary>
    public ulong PackedSize { get; internal init; }

    /// <summary>
    /// Date and time the entry was created.
    /// </summary>
    public DateTime CreationTime { get; internal init; }

    /// <summary>
    /// Date and time the entry was last modified.
    /// </summary>
    public DateTime LastWriteTime { get; internal init; }

    /// <summary>
    /// Date and time the entry was last accessed.
    /// </summary>
    public DateTime LastAccessTime { get; internal init; }

    /// <summary>
    /// CRC-32 hash of the entry.
    /// </summary>
    public uint CRC { get; internal init; }

    /// <summary>
    /// File attributes of the entry.
    /// </summary>
    public uint Attributes { get; internal init; }

    /// <summary>
    /// <see langword="true"/> if the entry is encrypted; otherwise <see langword="false"/>.
    /// </summary>
    public bool IsEncrypted { get; internal init; }

    /// <summary>
    /// Comment associated with the entry, if any.
    /// </summary>
    public string? Comment { get; internal init; }

    /// <summary>
    /// Compression method used for the entry.
    /// </summary>
    public string? Method { get; internal init; }

    /// <summary>
    /// Host operating system that created the entry.
    /// </summary>
    public string? HostOS { get; internal init; }

    /// <summary>
    /// <see langword="true"/> if parts of this file exist in previous split archive volumes.
    /// </summary>
    public bool IsSplitBefore { get; internal init; }

    /// <summary>
    /// <see langword="true"/> if parts of this file exist in subsequent split archive volumes.
    /// </summary>
    public bool IsSplitAfter { get; internal init; }

    /// <summary>
    /// Extracts this entry to a file on disk.
    /// </summary>
    /// <param name="fileName">The destination file path.</param>
    /// <param name="preserveTimestamp">If <see langword="true"/>, sets the file's last-write time to <see cref="LastWriteTime"/>.</param>
    public void Extract(string fileName, bool preserveTimestamp = true) =>
        _owner.ExtractEntry(this, fileName, new ExtractionOptions { Overwrite = true }, preserveTimestamp);

    /// <summary>Extracts this entry to a caller-owned stream.</summary>
    public void Extract(Stream stream, string? password = null) =>
        _owner.ExtractEntry(this, stream, new ExtractionOptions { Password = password });

    /// <summary>Extracts this entry to a file using the specified extraction policy.</summary>
    public void Extract(string fileName, ExtractionOptions options, bool preserveTimestamp = true) =>
        _owner.ExtractEntry(this, fileName, options, preserveTimestamp);

    /// <summary>Extracts to a caller-owned stream using the specified extraction policy.</summary>
    public void ExtractWithOptions(Stream stream, ExtractionOptions options) =>
        _owner.ExtractEntry(this, stream, options);

    /// <summary>Schedules extraction to a file through the archive's serialized operation gate.</summary>
    public Task ExtractAsync(string fileName, ExtractionOptions? options = null, bool preserveTimestamp = true) =>
        Task.Run(() => _owner.ExtractEntry(this, fileName, options ?? new ExtractionOptions { Overwrite = true }, preserveTimestamp),
            options?.CancellationToken ?? default);

    /// <summary>Schedules extraction to a caller-owned stream through the archive's serialized operation gate.</summary>
    public Task ExtractAsync(Stream stream, ExtractionOptions? options = null) =>
        Task.Run(() => _owner.ExtractEntry(this, stream, options ?? new()), options?.CancellationToken ?? default);
}