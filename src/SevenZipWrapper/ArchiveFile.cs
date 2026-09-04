namespace SevenZipWrapper;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SevenZipWrapper.Callbacks;
using SevenZipWrapper.Interop;

/// <summary>Opens, enumerates, and extracts archives through serialized native operations.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class ArchiveFile : IDisposable
{
    private readonly SevenZipHandle _sevenZipHandle;
    private readonly IInArchive _archive;
    private readonly InStreamWrapper _archiveStream;
    private readonly OperationGate _operationGate = new();
    private readonly string? _openPassword;
    private IReadOnlyList<ArchiveEntry>? _entries;
    private bool _disposed;
    private bool _opened;

    public SevenZipFormat Format { get; }
    public ArchiveFile(string archiveFilePath, string? libraryFilePath = null)
        : this(archiveFilePath, null, new ArchiveOpenOptions { LibraryFilePath = libraryFilePath }) { }
    public ArchiveFile(Stream archiveStream, SevenZipFormat? format = null, string? libraryFilePath = null)
        : this(null, archiveStream ?? throw new ArgumentNullException(nameof(archiveStream)),
            new ArchiveOpenOptions { Format = format, LibraryFilePath = libraryFilePath }) { }
    public static ArchiveFile Open(string archiveFilePath, ArchiveOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(archiveFilePath, null, options);
    }
    public static ArchiveFile Open(Stream archiveStream, ArchiveOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        ArgumentNullException.ThrowIfNull(options);
        return new(null, archiveStream, options);
    }
    private ArchiveFile(string? filePath, Stream? source, ArchiveOpenOptions options)
    {
        SevenZipHandle? library = null;
        IInArchive? archive = null;
        InStreamWrapper? wrapper = null;
        Stream? stream = source;
        bool ownsStream = source is null || !options.LeaveOpen;
        try
        {
            if (source is null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(filePath, "archiveFilePath");
                if (!File.Exists(filePath)) throw new SevenZipException("Archive file not found.");
                stream = File.OpenRead(filePath);
            }
            if (!stream!.CanRead || !stream.CanSeek)
                throw new ArgumentException("Archive streams must be readable and seekable.", "archiveStream");
            SevenZipFormat? format = options.Format;
            if (format is null)
            {
                if (TryGuessFormatFromSignature(stream, out var fromSignature)) format = fromSignature;
                else if (filePath is not null && TryGuessFormatFromExtension(Path.GetExtension(filePath), out var fromExtension)) format = fromExtension;
                else throw new SevenZipException(new ArchiveFailure(FailureKind.UnsupportedFormat, "Unable to determine archive format."));
            }
            if (!Formats.FormatGuidMapping.TryGetValue(format.Value, out Guid classId))
                throw new SevenZipNativeException(new(FailureKind.UnsupportedFormat, "The specified archive format is unsupported."));
            library = InitializeLibrary(options.LibraryFilePath);
            archive = library.CreateInArchive(classId);
            wrapper = new InStreamWrapper(stream, leaveOpen: !ownsStream);
            _sevenZipHandle = library; _archive = archive; _archiveStream = wrapper;
            _openPassword = options.Password; Format = format.Value;
        }
        catch
        {
            try { if (archive is not null && Marshal.IsComObject(archive)) Marshal.ReleaseComObject(archive); }
            finally
            {
                try { if (wrapper is not null) wrapper.Dispose(); else if (ownsStream) stream?.Dispose(); }
                finally { library?.Dispose(); }
            }
            throw;
        }
    }
    internal IDisposable EnterOperation(CancellationToken cancellationToken = default) => _operationGate.Enter(cancellationToken);
    public IReadOnlyList<ArchiveEntry> Entries
    {
        get { using var operation = EnterOperation(); return LoadEntries(); }
    }
    private IReadOnlyList<ArchiveEntry> LoadEntries(int? maximumEntryCount = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_entries is not null)
        {
            ValidateEntryCount((uint)_entries.Count, maximumEntryCount);
            return _entries;
        }
        _archiveStream.State.Reset();
        if (!_opened)
        {
            ulong checkPos = 32 * 1024;
            var openCallback = new ArchivePasswordCallback(_openPassword);
            int open = _archive.Open(_archiveStream, ref checkPos, openCallback);
            _opened = open == 0;
            _archiveStream.State.ThrowIfCaptured();
            openCallback.State.ThrowIfCaptured();
            if (openCallback.State.Failure is { } passwordFailure) throw new SevenZipNativeException(passwordFailure);
            NativeStatus.Check(open, "archive open", FailureKind.InvalidArchive);
        }
        uint itemsCount;
        try { itemsCount = _archive.GetNumberOfItems(); }
        catch (COMException ex) { throw new SevenZipNativeException(new(FailureKind.NativeInteropFailure, "Unable to enumerate archive entries.", NativeHResult: ex.HResult), ex); }
        ValidateEntryCount(itemsCount, maximumEntryCount);
        List<ArchiveEntry> entries = new();
        for (uint i = 0; i < itemsCount; i++)
        {
            entries.Add(new ArchiveEntry(this, i)
            {
                FileName = GetProperty<string>(i, ItemPropId.Path),
                IsFolder = GetProperty<bool>(i, ItemPropId.IsFolder),
                IsEncrypted = GetProperty<bool>(i, ItemPropId.Encrypted),
                Size = GetProperty<ulong>(i, ItemPropId.Size),
                PackedSize = GetProperty<ulong>(i, ItemPropId.PackedSize),
                CreationTime = GetProperty<DateTime>(i, ItemPropId.CreationTime),
                LastWriteTime = GetProperty<DateTime>(i, ItemPropId.LastWriteTime),
                LastAccessTime = GetProperty<DateTime>(i, ItemPropId.LastAccessTime),
                CRC = GetProperty<uint>(i, ItemPropId.CRC),
                Attributes = GetProperty<uint>(i, ItemPropId.Attributes),
                Comment = GetProperty<string>(i, ItemPropId.Comment),
                HostOS = GetProperty<string>(i, ItemPropId.HostOS),
                Method = GetProperty<string>(i, ItemPropId.Method),
                IsSplitBefore = GetProperty<bool>(i, ItemPropId.SplitBefore),
                IsSplitAfter = GetProperty<bool>(i, ItemPropId.SplitAfter)
            });
        }
        _entries = Array.AsReadOnly(entries.ToArray());
        return _entries;
    }
    private static void ValidateEntryCount(uint count, int? maximum)
    {
        if (count > int.MaxValue || maximum is { } limit && count > limit)
            throw new SevenZipException(new ArchiveFailure(FailureKind.ResourceLimitExceeded, "Archive entry count exceeds the configured or managed collection limit."));
    }
    public void Dispose() => _operationGate.Dispose(() =>
    {
        _disposed = true;
        try { if (_opened) _archive.Close(); }
        catch (COMException ex) { throw new SevenZipNativeException(new(FailureKind.NativeInteropFailure, "Unable to close archive.", NativeHResult: ex.HResult), ex); }
        finally
        {
            try { Marshal.ReleaseComObject(_archive); }
            finally
            {
                try { _archiveStream.Dispose(); }
                finally { _sevenZipHandle.Dispose(); }
            }
        }
    });
    private static SevenZipHandle InitializeLibrary(string? libraryFilePath)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new SevenZipNativeException(new(FailureKind.NativeLibraryFailure, "SevenZipWrapper requires a Windows x64 process."));
        if (string.IsNullOrWhiteSpace(libraryFilePath)) libraryFilePath = ResolveLibraryPath();
        if (libraryFilePath is null || !File.Exists(libraryFilePath))
            throw new SevenZipNativeException(new(FailureKind.NativeLibraryFailure, "Native archive library not found."));
        return new SevenZipHandle(libraryFilePath);
    }
    private static string? ResolveLibraryPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        ReadOnlySpan<string> candidates =
        [
            Path.Combine(baseDir, "7z.dll"), Path.Combine(baseDir, "7z-x64.dll"),
            Path.Combine(baseDir, "bin", "7z-x64.dll"), Path.Combine(baseDir, "bin", "x64", "7z.dll"),
            Path.Combine(baseDir, "x64", "7z.dll")
        ];
        foreach (string candidate in candidates) if (File.Exists(candidate)) return candidate;
        return null;
    }
    private static bool TryGuessFormatFromExtension(string? fileExtension, out SevenZipFormat format)
    {
        format = SevenZipFormat.Undefined;
        if (string.IsNullOrWhiteSpace(fileExtension)) return false;
        string extension = fileExtension.TrimStart('.').Trim();
        if (extension.Equals("rar", StringComparison.OrdinalIgnoreCase)) return false;
        return Formats.ExtensionFormatMapping.TryGetValue(extension, out format);
    }
    private static bool TryGuessFormatFromSignature(Stream stream, out SevenZipFormat format)
    {
        Span<byte> buffer = stackalloc byte[Formats.MaxSignatureLength];
        int bytesRead = ReadSignature(stream, buffer);
        ReadOnlySpan<byte> signature = buffer[..bytesRead];
        // Empty ZIP archives begin with the end-of-central-directory signature.
        if (signature.StartsWith(new byte[] { 0x50, 0x4B, 0x05, 0x06 }))
        { format = SevenZipFormat.Zip; return true; }
        foreach (KeyValuePair<SevenZipFormat, byte[]> pair in Formats.FileSignatures)
        {
            if (signature.Length >= pair.Value.Length && signature[..pair.Value.Length].SequenceEqual(pair.Value))
            { format = pair.Key; return true; }
        }
        format = SevenZipFormat.Undefined;
        return false;
    }
    private static int ReadSignature(Stream stream, Span<byte> buffer)
    {
        long originalPosition = stream.Position;
        try
        {
            int count = 0;
            while (count < buffer.Length)
            {
                int read = stream.Read(buffer[count..]);
                if (read == 0) break;
                count += read;
            }
            return count;
        }
        finally { stream.Position = originalPosition; }
    }
    private T? GetProperty<T>(uint fileIndex, ItemPropId propId)
    {
        PropVariant variant = new();
        try
        {
            _archive.GetProperty(fileIndex, propId, ref variant);
            return MetadataConverter.Convert<T>(variant.GetObject(), variant.VarType, allowNumericString: propId == ItemPropId.Method);
        }
        catch (COMException ex)
        { throw new SevenZipNativeException(new(FailureKind.NativeInteropFailure, "Unable to read archive metadata.", NativeHResult: ex.HResult), ex); }
        finally { variant.Clear(); }
    }
}
