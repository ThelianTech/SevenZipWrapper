namespace SevenZipWrapper.Callbacks;

using SevenZipWrapper.Interop;

/// <summary>
/// Callback for extracting all archive entries to a list of <see cref="Stream"/>s.
/// A <see langword="null"/> entry in the list means that entry should be skipped.
/// </summary>
internal sealed class ArchiveStreamsCallback(IList<Stream?> streams, string? password = null)
    : IArchiveExtractCallback, ICryptoGetTextPassword
{
    private readonly string _password = password ?? "";

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
        if (askExtractMode != AskMode.Extract)
        {
            outStream = null;
            return 0;
        }

        Stream? stream = streams[(int)index];

        if (stream is null)
        {
            outStream = null;
            return 0;
        }

        outStream = new OutStreamWrapper(stream);
        return 0;
    }

    public void PrepareOperation(AskMode askExtractMode)
    {
    }

    public void SetOperationResult(OperationResult resultEOperationResult)
    {
    }
}