namespace SevenZipWrapper.Tests.FormatTests;

/// <summary>
/// Tests extraction of 7z archives.
/// </summary>
public class SevenZipFormatTests : TestBase
{
    [Fact]
    public void ExtractToStream_SevenZip_WithoutFolder_MatchesMD5()
    {
        byte[] archive = LoadResource("SevenZip.7z");
        AssertExtractToStream(archive, TestEntriesWithoutFolder);
    }

    [Fact]
    public void ExtractToStream_SevenZip_ExplicitFormat_MatchesMD5()
    {
        byte[] archive = LoadResource("SevenZip.7z");
        AssertExtractToStream(archive, TestEntriesWithoutFolder, SevenZipFormat.SevenZip);
    }

    [Fact]
    public void ExtractFromFile_SevenZip_MatchesMD5()
    {
        string path = Path.Combine(ResourcesDirectory, "SevenZip.7z");
        AssertExtractToStreamFromFile(path, TestEntriesWithoutFolder);
    }

    [Fact]
    public void Format_IsSevenZip_WhenOpenedFromFile()
    {
        string path = Path.Combine(ResourcesDirectory, "SevenZip.7z");
        using ArchiveFile archive = new(path);

        Assert.Equal(SevenZipFormat.SevenZip, archive.Format);
    }

    [Fact]
    public void Entries_Count_MatchesExpected()
    {
        byte[] data = LoadResource("SevenZip.7z");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.SevenZip);

        Assert.Equal(TestEntriesWithoutFolder.Count, archive.Entries.Count);
    }

    [Fact]
    public void ExtractAll_ToDirectory_CreatesFiles()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "SevenZip.7z");
            using ArchiveFile archive = new(path);
            archive.Extract(outputDir);

            Assert.True(File.Exists(Path.Combine(outputDir, "image1.jpg")));
            Assert.True(File.Exists(Path.Combine(outputDir, "image2.jpg")));
            Assert.True(File.Exists(Path.Combine(outputDir, "testFolder", "image3.jpg")));
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