namespace SevenZipWrapper.Tests.FormatTests;

/// <summary>
/// Tests extraction of ZIP archives.
/// </summary>
public class ZipFormatTests : TestBase
{
    [Fact]
    public void ExtractToStream_Zip_WithFolder_MatchesMD5()
    {
        byte[] archive = LoadResource("zip.zip");
        AssertExtractToStream(archive, TestEntriesWithFolder);
    }

    [Fact]
    public void ExtractToStream_Zip_ExplicitFormat_MatchesMD5()
    {
        byte[] archive = LoadResource("zip.zip");
        AssertExtractToStream(archive, TestEntriesWithFolder, SevenZipFormat.Zip);
    }

    [Fact]
    public void ExtractFromFile_Zip_MatchesMD5()
    {
        string path = Path.Combine(ResourcesDirectory, "zip.zip");
        AssertExtractToStreamFromFile(path, TestEntriesWithFolder);
    }

    [Fact]
    public void Format_IsZip_WhenOpenedFromStream()
    {
        byte[] data = LoadResource("zip.zip");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms);

        Assert.Equal(SevenZipFormat.Zip, archive.Format);
    }
}