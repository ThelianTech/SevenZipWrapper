namespace SevenZipWrapper.Callbacks;

using SevenZipWrapper.Interop;

/// <summary>
/// Callback for extracting a single archive entry to a file path on disk.
/// </summary>
internal sealed class ArchiveFileCallback(uint fileNumber, string fileName)
    : IArchiveExtractCallback
{
    private OutStreamWrapper? _fileStream;

    public void SetTotal(ulong total)
    {
    }

    public void SetCompleted(ref ulong completeValue)
    {
    }

    public int GetStream(uint index, out ISequentialOutStream? outStream, AskMode askExtractMode)
    {
        if (index != fileNumber || askExtractMode != AskMode.Extract)
        {
            outStream = null;
            return 0;
        }

        string? fileDir = Path.GetDirectoryName(fileName);

        if (!string.IsNullOrEmpty(fileDir))
        {
            Directory.CreateDirectory(fileDir);
        }

        _fileStream = new OutStreamWrapper(File.Create(fileName));
        outStream = _fileStream;
        return 0;
    }

    public void PrepareOperation(AskMode askExtractMode)
    {
    }

    public void SetOperationResult(OperationResult resultEOperationResult)
    {
        _fileStream?.Dispose();
    }
}