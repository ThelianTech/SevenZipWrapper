namespace SevenZipWrapper.Callbacks;

using SevenZipWrapper.Interop;

/// <summary>
/// Callback for extracting all archive entries to a list of <see cref="Stream"/>s.
/// A <see langword="null"/> entry in the list means that entry should be skipped.
/// </summary>
internal sealed class ArchiveStreamsCallback(
    IList<Stream?> streams,
    string? password = null,
    Action<int>? onFileExtracted = null,
    CancellationToken cancellationToken = default)
    : IArchiveExtractCallback, ICryptoGetTextPassword
{
    private readonly string _password = password ?? "";
    private int _filesExtracted;
    private bool _currentEntryHasStream;

    public void SetTotal(ulong total)
    {
    }

    public void SetCompleted(ref ulong completeValue)
    {
    }

    public int CryptoGetTextPassword(out string password)
    {
        password = _password;
        return 0;
    }

    public int GetStream(uint index, out ISequentialOutStream? outStream, AskMode askExtractMode)
    {
        // Check cancellation before starting each file.
        // Returning E_ABORT tells 7z.dll to stop the extraction loop.
        if (cancellationToken.IsCancellationRequested)
        {
            _currentEntryHasStream = false;
            outStream = null;
            return unchecked((int)0x80004004); // E_ABORT
        }

        if (askExtractMode != AskMode.Extract)
        {
            _currentEntryHasStream = false;
            outStream = null;
            return 0;
        }

        Stream? stream = streams[(int)index];

        if (stream is null)
        {
            _currentEntryHasStream = false;
            outStream = null;
            return 0;
        }

        _currentEntryHasStream = true;
        outStream = new OutStreamWrapper(stream);
        return 0;
    }

    public void PrepareOperation(AskMode askExtractMode)
    {
    }

    public void SetOperationResult(OperationResult resultEOperationResult)
    {
        // 7z.dll calls SetOperationResult after every entry, including folders
        // and skipped entries. Only count entries that had an actual output stream
        // and completed successfully.
        if (resultEOperationResult == OperationResult.OK && _currentEntryHasStream)
        {
            _filesExtracted++;
            onFileExtracted?.Invoke(_filesExtracted);
        }
    }
}