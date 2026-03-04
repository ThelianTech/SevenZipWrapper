namespace SevenZipWrapper.Tests.FormatTests;

/// <summary>
/// Tests extraction of RAR (pre-RAR5) archives.
/// </summary>
public class RarFormatTests : TestBase
{
    [Fact]
    public void ExtractToStream_Rar_WithoutFolder_MatchesMD5()
    {
        byte[] archive = LoadResource("rar.rar");
        AssertExtractToStream(archive, TestEntriesWithoutFolder);
    }

    [Fact]
    public void ExtractFromFile_Rar_MatchesMD5()
    {
        string path = Path.Combine(ResourcesDirectory, "rar.rar");
        AssertExtractToStreamFromFile(path, TestEntriesWithoutFolder);
    }

    [Fact]
    public void Format_IsRar_WhenOpenedFromStream()
    {
        byte[] data = LoadResource("rar.rar");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms);

        // RAR format detection falls through to signature — should detect Rar or Rar5
        Assert.True(
            archive.Format is SevenZipFormat.Rar or SevenZipFormat.Rar5,
            $"Expected Rar or Rar5 but got {archive.Format}");
    }
}