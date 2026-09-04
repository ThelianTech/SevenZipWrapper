namespace SevenZipWrapper.Tests.CoreTests;

using System.IO.Compression;
using System.Text;

public sealed class CallbackPathConflictTests
{
    [Theory]
    [InlineData("same.txt", "same.txt", false, false)]
    [InlineData("same.txt", "SAME.TXT", false, false)]
    [InlineData("same.txt", "folder/../same.txt", false, false)]
    [InlineData("folder/same.txt", "folder/./same.txt", false, false)]
    [InlineData("shape", "shape", false, true)]
    [InlineData("shape", "shape", true, false)]
    [InlineData("shape", "shape/child", false, false)]
    [InlineData("shape/child", "shape", false, false)]
    public void SelectedDestinationsConflictBeforeAnyOutputIsCreated(string firstPath, string secondPath,
        bool firstDirectory, bool secondDirectory)
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "SZW_Callback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        string root = Path.Combine(sandbox, "not-created");
        try
        {
            using MemoryStream bytes = new();
            using (ZipArchive zip = new(bytes, ZipArchiveMode.Create, true))
            {
                Add(zip, firstDirectory ? "first/" : "first.bin", firstDirectory);
                Add(zip, secondDirectory ? "second/" : "second.bin", secondDirectory);
            }
            bytes.Position = 0;
            using ArchiveFile archive = new(bytes, SevenZipFormat.Zip);
            ArchiveExtractionException failure = Assert.Throws<ArchiveExtractionException>(() => archive.Extract(entry =>
                Path.Combine(root, entry.FileName!.StartsWith("first", StringComparison.Ordinal) ? firstPath : secondPath)));
            Assert.Equal(FailureKind.DestinationConflict, failure.Failure.Kind);
            Assert.False(Directory.Exists(root));
            Assert.Empty(Directory.GetFiles(sandbox, "*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(sandbox, true); }
    }

    private static void Add(ZipArchive zip, string name, bool directory)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name);
        if (directory) return;
        using Stream output = entry.Open();
        output.Write(Encoding.UTF8.GetBytes(name));
    }
}
