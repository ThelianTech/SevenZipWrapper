namespace SevenZipWrapper.Tests.CoreTests;

/// <summary>
/// Tests <see cref="ArchiveEntry"/> property reading and extraction.
/// </summary>
public class ArchiveEntryTests : TestBase
{
    [Fact]
    public void Entry_FileName_IsPopulated()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        Assert.All(archive.Entries, entry =>
            Assert.False(string.IsNullOrEmpty(entry.FileName)));
    }

    [Fact]
    public void Entry_IsFolder_IdentifiesFolders()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        ArchiveEntry? folder = archive.Entries.FirstOrDefault(e => e.IsFolder);
        Assert.NotNull(folder);
        Assert.True(folder.IsFolder);
    }

    [Fact]
    public void Entry_Size_IsNonZero_ForFiles()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        IEnumerable<ArchiveEntry> files = archive.Entries.Where(e => !e.IsFolder);

        Assert.All(files, entry =>
            Assert.True(entry.Size > 0, $"{entry.FileName} has zero size."));
    }

    [Fact]
    public void Entry_Extract_NullStream_Throws()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);

        Assert.Throws<ArgumentNullException>(() => entry.Extract((Stream)null!));
    }

    [Fact]
    public void Entry_Extract_NullFileName_Throws()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);

        Assert.Throws<ArgumentNullException>(() => entry.Extract((string)null!));
    }

    [Fact]
    public void Entry_Extract_EmptyFileName_Throws()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);

        Assert.Throws<ArgumentException>(() => entry.Extract(""));
    }

    [Fact]
    public void Entry_ExtractToFile_CreatesFile()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            byte[] data = LoadResource("zip.zip");
            using MemoryStream ms = new(data);
            using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

            ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);
            string outputPath = Path.Combine(outputDir, entry.FileName!);

            entry.Extract(outputPath);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Entry_ExtractToStream_ProducesData()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);

        using MemoryStream output = new();
        entry.Extract(output);

        Assert.True(output.Length > 0);
    }

    [Fact]
    public void Entry_ExtractToFile_PreservesTimestamp()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            byte[] data = LoadResource("zip.zip");
            using MemoryStream ms = new(data);
            using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

            ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);
            string outputPath = Path.Combine(outputDir, entry.FileName!);

            entry.Extract(outputPath, preserveTimestamp: true);

            Assert.True(File.Exists(outputPath));

            // The preserved timestamp should match the entry's LastWriteTime
            if (entry.LastWriteTime != default)
            {
                DateTime fileTime = File.GetLastWriteTime(outputPath);
                Assert.Equal(entry.LastWriteTime, fileTime, TimeSpan.FromSeconds(2));
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }
}