namespace SevenZipWrapper.Callbacks;
using SevenZipWrapper.Interop;
internal sealed class ArchiveFileCallback : ArchiveStreamsCallback, IDisposable
{
    private readonly FileStream?[] _owned;
    internal ArchiveFileCallback(uint fileNumber, string fileName) : this(fileNumber, fileName, new FileStream?[1]) { }
    private ArchiveFileCallback(uint fileNumber, string fileName, FileStream?[] owned)
        : base(index =>
        {
            if (index != fileNumber) return null;
            string? directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            return owned[0] ??= File.Create(fileName);
        }) => _owned = owned;
    public void Dispose() => _owned[0]?.Dispose();
}
