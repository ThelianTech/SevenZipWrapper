namespace SevenZipWrapper.Tests.FormatTests;

/// <summary>
/// Tests extraction of ARJ archives.
/// </summary>
public class ArjFormatTests : TestBase
{
    [Fact]
    public void ExtractToStream_Arj_OpensAndHasEntries()
    {
        byte[] archive = LoadResource("ansimate-arj.arj");
        using MemoryStream ms = new(archive);
        using ArchiveFile archiveFile = new(ms, SevenZipFormat.Arj);

        Assert.NotEmpty(archiveFile.Entries);

        // Extract first non-folder entry to stream to verify extraction works
        ArchiveEntry entry = archiveFile.Entries.First(e => !e.IsFolder);
        using MemoryStream entryStream = new();
        entry.Extract(entryStream);

        Assert.True(entryStream.Length > 0);
    }

    [Fact]
    public void Format_IsArj_WhenOpenedFromStream()
    {
        byte[] data = LoadResource("ansimate-arj.arj");
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms);

        Assert.Equal(SevenZipFormat.Arj, archive.Format);
    }
}