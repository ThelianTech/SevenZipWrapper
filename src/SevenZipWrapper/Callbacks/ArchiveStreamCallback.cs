namespace SevenZipWrapper.Callbacks;

using SevenZipWrapper.Interop;
/// <summary>
/// Callback for extracting a single archive entry to a <see cref="Stream"/>.
/// </summary>
internal sealed class ArchiveStreamCallback(uint fileNumber, Stream stream, string? password = null)
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
        if (index != fileNumber || askExtractMode != AskMode.Extract)
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