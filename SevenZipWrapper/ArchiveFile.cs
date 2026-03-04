namespace SevenZipWrapper;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using SevenZipWrapper.Callbacks;
using SevenZipWrapper.Interop;

/// <summary>
/// Opens, enumerates, and extracts entries from archive files using the 7z.dll COM interface.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ArchiveFile : IDisposable
{
    private readonly SevenZipHandle _sevenZipHandle;
    private readonly IInArchive _archive;
    private readonly InStreamWrapper _archiveStream;
    private IReadOnlyList<ArchiveEntry>? _entries;
    private bool _disposed;

    /// <summary>
    /// The detected or specified archive format.
    /// </summary>
    public SevenZipFormat Format { get; }

    /// <summary>
    /// Opens an archive from a file path. The format is guessed from the file extension or signature.
    /// </summary>
    /// <param name="archiveFilePath">Path to the archive file.</param>
    /// <param name="libraryFilePath">Optional explicit path to <c>7z.dll</c>. If <see langword="null"/>, auto-detected.</param>
    /// <exception cref="SevenZipException">The file does not exist or the format cannot be determined.</exception>
    public ArchiveFile(string archiveFilePath, string? libraryFilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveFilePath);

        _sevenZipHandle = InitializeLibrary(libraryFilePath);

        if (!File.Exists(archiveFilePath))
        {
            throw new SevenZipException("Archive file not found.");
        }

        string extension = Path.GetExtension(archiveFilePath);

        if (!TryGuessFormatFromExtension(extension, out SevenZipFormat format)
            && !TryGuessFormatFromSignature(archiveFilePath, out format))
        {
            throw new SevenZipException($"'{Path.GetFileName(archiveFilePath)}' is not a known archive type.");
        }

        Format = format;
        _archive = _sevenZipHandle.CreateInArchive(Formats.FormatGuidMapping[format])
                   ?? throw new SevenZipException("Failed to create IInArchive instance.");
        _archiveStream = new InStreamWrapper(File.OpenRead(archiveFilePath));
    }

    /// <summary>
    /// Opens an archive from a <see cref="Stream"/>. If <paramref name="format"/> is <see langword="null"/>,
    /// the format is guessed from the stream signature.
    /// </summary>
    /// <param name="archiveStream">The archive stream to read from.</param>
    /// <param name="format">Optional explicit archive format. If <see langword="null"/>, auto-detected from signature.</param>
    /// <param name="libraryFilePath">Optional explicit path to <c>7z.dll</c>. If <see langword="null"/>, auto-detected.</param>
    /// <exception cref="SevenZipException">The format cannot be determined or the archive cannot be opened.</exception>
    public ArchiveFile(Stream archiveStream, SevenZipFormat? format = null, string? libraryFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);

        _sevenZipHandle = InitializeLibrary(libraryFilePath);

        if (format is null)
        {
            if (!TryGuessFormatFromSignature(archiveStream, out SevenZipFormat guessedFormat))
            {
                throw new SevenZipException("Unable to guess archive format automatically.");
            }

            format = guessedFormat;
        }

        Format = format.Value;
        _archive = _sevenZipHandle.CreateInArchive(Formats.FormatGuidMapping[format.Value])
                   ?? throw new SevenZipException("Failed to create IInArchive instance.");
        _archiveStream = new InStreamWrapper(archiveStream);
    }

    /// <summary>
    /// Gets the list of entries in the archive. The archive is opened on first access.
    /// </summary>
    public IReadOnlyList<ArchiveEntry> Entries
    {
        get
        {
            if (_entries is not null)
            {
                return _entries;
            }

            ulong checkPos = 32 * 1024;
            int open = _archive.Open(_archiveStream, ref checkPos, null);

            if (open != 0)
            {
                throw new SevenZipException("Unable to open archive.");
            }

            uint itemsCount = _archive.GetNumberOfItems();
            List<ArchiveEntry> entries = new((int)itemsCount);

            for (uint i = 0; i < itemsCount; i++)
            {
                entries.Add(new ArchiveEntry(_archive, i)
                {
                    FileName = GetPropertySafe<string>(i, ItemPropId.Path),
                    IsFolder = GetProperty<bool>(i, ItemPropId.IsFolder),
                    IsEncrypted = GetProperty<bool>(i, ItemPropId.Encrypted),
                    Size = GetProperty<ulong>(i, ItemPropId.Size),
                    PackedSize = GetProperty<ulong>(i, ItemPropId.PackedSize),
                    CreationTime = GetPropertySafe<DateTime>(i, ItemPropId.CreationTime),
                    LastWriteTime = GetPropertySafe<DateTime>(i, ItemPropId.LastWriteTime),
                    LastAccessTime = GetPropertySafe<DateTime>(i, ItemPropId.LastAccessTime),
                    CRC = GetPropertySafe<uint>(i, ItemPropId.CRC),
                    Attributes = GetPropertySafe<uint>(i, ItemPropId.Attributes),
                    Comment = GetPropertySafe<string>(i, ItemPropId.Comment),
                    HostOS = GetPropertySafe<string>(i, ItemPropId.HostOS),
                    Method = GetPropertySafe<string>(i, ItemPropId.Method),
                    IsSplitBefore = GetPropertySafe<bool>(i, ItemPropId.SplitBefore),
                    IsSplitAfter = GetPropertySafe<bool>(i, ItemPropId.SplitAfter)
                });
            }

            _entries = entries;
            return _entries;
        }
    }

    /// <summary>
    /// Extracts all entries to the specified output folder.
    /// </summary>
    /// <param name="outputFolder">Destination directory path.</param>
    /// <param name="overwrite">If <see langword="true"/>, overwrites existing files.</param>
    /// <param name="password">Optional password for encrypted archives.</param>
    public void Extract(string outputFolder, bool overwrite = false, string? password = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

        Extract(entry =>
        {
            string fileName = Path.Combine(outputFolder, entry.FileName ?? string.Empty);

            if (entry.IsFolder)
            {
                return fileName;
            }

            return !File.Exists(fileName) || overwrite ? fileName : null;
        },
        password);
    }

    /// <summary>
    /// Extracts entries using a callback to determine each entry's output path.
    /// Return <see langword="null"/> from <paramref name="getOutputPath"/> to skip an entry.
    /// </summary>
    /// <param name="getOutputPath">A function that receives an <see cref="ArchiveEntry"/> and returns the output path, or <see langword="null"/> to skip.</param>
    /// <param name="password">Optional password for encrypted archives.</param>
    public void Extract(Func<ArchiveEntry, string?> getOutputPath, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(getOutputPath);

        List<Stream?> fileStreams = [];

        try
        {
            foreach (ArchiveEntry entry in Entries)
            {
                string? outputPath = getOutputPath(entry);

                if (outputPath is null)
                {
                    fileStreams.Add(null);
                    continue;
                }

                if (entry.IsFolder)
                {
                    Directory.CreateDirectory(outputPath);
                    fileStreams.Add(null);
                    continue;
                }

                string? directoryName = Path.GetDirectoryName(outputPath);

                if (!string.IsNullOrWhiteSpace(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                fileStreams.Add(File.Create(outputPath));
            }

            _archive.Extract(null, 0xFFFFFFFF, 0, new ArchiveStreamsCallback(fileStreams, password));
        }
        finally
        {
            foreach (Stream? stream in fileStreams)
            {
                stream?.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _archiveStream.Dispose();
        Marshal.ReleaseComObject(_archive);
        _sevenZipHandle.Dispose();

        _disposed = true;
    }

    // -----------------------------------------
    //  Private helpers
    // -----------------------------------------

    private static SevenZipHandle InitializeLibrary(string? libraryFilePath)
    {
        if (string.IsNullOrWhiteSpace(libraryFilePath))
        {
            libraryFilePath = ResolveLibraryPath();
        }

        if (libraryFilePath is null || !File.Exists(libraryFilePath))
        {
            throw new SevenZipException("7z.dll not found.");
        }

        try
        {
            return new SevenZipHandle(libraryFilePath);
        }
        catch (Exception e)
        {
            throw new SevenZipException("Unable to initialize SevenZipHandle.", e);
        }
    }

    private static string? ResolveLibraryPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        ReadOnlySpan<string> candidates =
        [
            Path.Combine(baseDir, "7z-x64.dll"),
            Path.Combine(baseDir, "bin", "7z-x64.dll"),
            Path.Combine(baseDir, "bin", "x64", "7z.dll"),
            Path.Combine(baseDir, "x64", "7z.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.dll")
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryGuessFormatFromExtension(string? fileExtension, out SevenZipFormat format)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            format = SevenZipFormat.Undefined;
            return false;
        }

        string ext = fileExtension.TrimStart('.').Trim();

        // RAR and RAR5 share the .rar extension but have different GUIDs.
        // Must fall through to signature detection to distinguish them.
        if (ext.Equals("rar", StringComparison.OrdinalIgnoreCase))
        {
            format = SevenZipFormat.Undefined;
            return false;
        }

        return Formats.ExtensionFormatMapping.TryGetValue(ext, out format);
    }

    private static bool TryGuessFormatFromSignature(string filePath, out SevenZipFormat format)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return TryGuessFormatFromSignature(stream, out format);
    }

    private static bool TryGuessFormatFromSignature(Stream stream, out SevenZipFormat format)
    {
        int maxLength = Formats.MaxSignatureLength;
        Span<byte> buffer = stackalloc byte[maxLength];

        int bytesRead = stream.Read(buffer);
        stream.Position -= bytesRead;

        if (bytesRead < 2)
        {
            format = SevenZipFormat.Undefined;
            return false;
        }

        ReadOnlySpan<byte> signature = buffer[..bytesRead];

        foreach (KeyValuePair<SevenZipFormat, byte[]> pair in Formats.FileSignatures)
        {
            if (signature.Length >= pair.Value.Length
                && signature[..pair.Value.Length].SequenceEqual(pair.Value))
            {
                format = pair.Key;
                return true;
            }
        }

        format = SevenZipFormat.Undefined;
        return false;
    }

    private T? GetPropertySafe<T>(uint fileIndex, ItemPropId propId)
    {
        try
        {
            return GetProperty<T>(fileIndex, propId);
        }
        catch (InvalidCastException)
        {
            return default;
        }
    }

    private T? GetProperty<T>(uint fileIndex, ItemPropId propId)
    {
        PropVariant propVariant = new();
        _archive.GetProperty(fileIndex, propId, ref propVariant);
        object? value = propVariant.GetObject();

        if (propVariant.VarType == VarEnum.VT_EMPTY)
        {
            propVariant.Clear();
            return default;
        }

        propVariant.Clear();

        if (value is null)
        {
            return default;
        }

        Type type = typeof(T);
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(DateTime))
        {
            return (T)(object)(DateTime)value;
        }

        return (T)Convert.ChangeType(value.ToString(), underlyingType);
    }
}