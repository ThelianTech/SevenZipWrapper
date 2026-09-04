namespace SevenZipWrapper.Tests.CoreTests;

/// <summary>
/// Tests automatic format detection from file extension and stream signature.
/// </summary>
public class FormatDetectionTests : TestBase
{
    [Theory]
    [InlineData("SevenZip.7z", SevenZipFormat.SevenZip)]
    [InlineData("zip.zip", SevenZipFormat.Zip)]
    public void OpenFromFile_DetectsCorrectFormat(string resourceName, SevenZipFormat expected)
    {
        string path = Path.Combine(ResourcesDirectory, resourceName);
        using ArchiveFile archive = new(path);

        Assert.Equal(expected, archive.Format);
    }

    [Theory]
    [InlineData("SevenZip.7z", SevenZipFormat.SevenZip)]
    [InlineData("zip.zip", SevenZipFormat.Zip)]
    [InlineData("rar.rar", null)] // Rar or Rar5 — both are valid
    [InlineData("ansimate-arj.arj", SevenZipFormat.Arj)]
    public void OpenFromStream_DetectsFormatFromSignature(string resourceName, SevenZipFormat? expected)
    {
        byte[] data = LoadResource(resourceName);
        using MemoryStream ms = new(data);
        using ArchiveFile archive = new(ms);

        if (expected is not null)
        {
            Assert.Equal(expected.Value, archive.Format);
        }
        else
        {
            // RAR archives can be detected as Rar or Rar5
            Assert.True(archive.Format is SevenZipFormat.Rar or SevenZipFormat.Rar5);
        }
    }

    [Fact]
    public void OpenFromStream_EmptyStream_Throws()
    {
        using MemoryStream ms = new();
        Assert.Throws<SevenZipException>(() => new ArchiveFile(ms));
    }

    [Fact]
    public void OpenFromStream_RandomBytes_Throws()
    {
        byte[] randomData = new byte[64];
        Random.Shared.NextBytes(randomData);

        using MemoryStream ms = new(randomData);
        Assert.Throws<SevenZipException>(() => new ArchiveFile(ms));
    }

    [Fact]
    public void OpenFromFile_UnknownExtension_FallsToSignature()
    {
        // Copy a known archive to a file with an unknown extension
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.unknown");

        try
        {
            byte[] data = LoadResource("zip.zip");
            File.WriteAllBytes(tempPath, data);

            using ArchiveFile archive = new(tempPath);
            Assert.Equal(SevenZipFormat.Zip, archive.Format);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void OpenFromStream_LzhRequiresExplicitFormat()
    {
        // LZH magic bytes are at offset 2, not offset 0 — signature detection won't find it
        byte[] data = LoadResource("lzh.lzh");
        using MemoryStream ms = new(data);

        Assert.Throws<SevenZipException>(() => new ArchiveFile(ms));
    }
}