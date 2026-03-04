namespace SevenZipWrapper.Tests.FormatTests;

/// <summary>
/// Tests extraction of LZH archives.
/// </summary>
public class LzhFormatTests : TestBase
{
    [Fact]
    public void ExtractToStream_Lzh_WithoutFolder_MatchesMD5()
    {
        byte[] archive = LoadResource("lzh.lzh");
        AssertExtractToStream(archive, TestEntriesWithoutFolder, SevenZipFormat.Lzh);
    }

    [Fact]
    public void Format_IsLzh_WhenOpenedWithExplicitFormat()
    {
        byte[] data = LoadResource("lzh.lzh");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms, SevenZipFormat.Lzh);

        Assert.Equal(SevenZipFormat.Lzh, archive.Format);
    }
}