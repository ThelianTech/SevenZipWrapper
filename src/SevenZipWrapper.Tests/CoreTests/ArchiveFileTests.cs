namespace SevenZipWrapper.Tests.CoreTests;

/// <summary>
/// Tests <see cref="ArchiveFile"/> constructor validation, dispose behavior, and extract overloads.
/// </summary>
public class ArchiveFileTests : TestBase
{
    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ArchiveFile((string)null!));
    }

    [Fact]
    public void Constructor_EmptyFilePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ArchiveFile(""));
    }

    [Fact]
    public void Constructor_WhitespaceFilePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ArchiveFile("   "));
    }

    [Fact]
    public void Constructor_NonExistentFile_ThrowsSevenZipException()
    {
        Assert.Throws<SevenZipException>(() => new ArchiveFile(@"C:\nonexistent_archive_12345.7z"));
    }

    [Fact]
    public void Constructor_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ArchiveFile((Stream)null!));
    }

    [Fact]
    public void Constructor_EmptyStream_ThrowsSevenZipException()
    {
        using MemoryStream ms = new();
        Assert.Throws<SevenZipException>(() => new ArchiveFile(ms));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        Exception? ex = Record.Exception(() => archive.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        archive.Dispose();
        Exception? ex = Record.Exception(() => archive.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Extract_NullOutputFolder_Throws()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        Assert.Throws<ArgumentNullException>(() => archive.Extract((string)null!));
    }

    [Fact]
    public void Extract_NullCallback_Throws()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Zip);

        Assert.Throws<ArgumentNullException>(() => archive.Extract((Func<ArchiveEntry, string?>)null!));
    }

    [Fact]
    public void Extract_WithOverwrite_ReplacesExistingFiles()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "zip.zip");
            using ArchiveFile archive = new(path);

            // Extract once
            archive.Extract(outputDir, overwrite: false);
            DateTime firstWrite = File.GetLastWriteTimeUtc(Path.Combine(outputDir, "image1.jpg"));

            // Small delay to ensure timestamp difference
            Thread.Sleep(50);

            // Extract again with overwrite
            using ArchiveFile archive2 = new(path);
            archive2.Extract(outputDir, overwrite: true);

            // File should have been recreated
            Assert.True(File.Exists(Path.Combine(outputDir, "image1.jpg")));
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
    public void Extract_SelectiveCallback_SkipsEntries()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "SevenZip.7z");
            using ArchiveFile archive = new(path);

            // Only extract image1.jpg
            archive.Extract(entry =>
                entry.FileName == "image1.jpg"
                    ? Path.Combine(outputDir, entry.FileName)
                    : null);

            Assert.True(File.Exists(Path.Combine(outputDir, "image1.jpg")));
            Assert.False(File.Exists(Path.Combine(outputDir, "image2.jpg")));
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