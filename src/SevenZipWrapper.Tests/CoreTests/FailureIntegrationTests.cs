namespace SevenZipWrapper.Tests.CoreTests;

public class FailureIntegrationTests : TestBase
{
    [Fact]
    public void ProgressExceptionIsRethrownUnchangedAfterNativeReturn()
    {
        using var input = new MemoryStream(LoadResource("zip.zip"));
        using var archive = new ArchiveFile(input, SevenZipFormat.Zip);
        var sentinel = new InvalidOperationException("caller failure");
        string root = Path.Combine(Path.GetTempPath(), "SZW_Failure_" + Guid.NewGuid().ToString("N"));
        try
        {
            var actual = Assert.Throws<InvalidOperationException>(() =>
                archive.Extract(root, false, _ => throw sentinel, default));
            Assert.Same(sentinel, actual);
            // All output handles must be closed before the caller observes the exception.
            foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void OutputStreamExceptionIsRethrownUnchanged()
    {
        using var input = new MemoryStream(LoadResource("zip.zip"));
        using var archive = new ArchiveFile(input, SevenZipFormat.Zip);
        var sentinel = new IOException("caller output failure");
        using var output = new ThrowingOutput(sentinel);
        Assert.Same(sentinel, Assert.Throws<IOException>(() =>
            archive.Entries.First(e => !e.IsFolder).Extract(output, null)));
    }

    [Fact]
    public void EntriesAfterDisposalThrowManagedException()
    {
        using var input = new MemoryStream(LoadResource("zip.zip"));
        var archive = new ArchiveFile(input, SevenZipFormat.Zip);
        _ = archive.Entries;
        archive.Dispose();
        Assert.Throws<ObjectDisposedException>(() => archive.Entries);
    }

    private sealed class ThrowingOutput(Exception failure) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw failure;
    }
}
