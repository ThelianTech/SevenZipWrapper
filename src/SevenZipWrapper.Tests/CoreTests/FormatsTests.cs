namespace SevenZipWrapper.Tests.CoreTests;

/// <summary>
/// Tests the static <see cref="Formats"/> dictionary lookups (no 7z.dll required).
/// </summary>
public class FormatsTests
{
    [Theory]
    [InlineData("7z", SevenZipFormat.SevenZip)]
    [InlineData("zip", SevenZipFormat.Zip)]
    [InlineData("rar", SevenZipFormat.Rar)]
    [InlineData("tar", SevenZipFormat.Tar)]
    [InlineData("gz", SevenZipFormat.GZip)]
    [InlineData("bz2", SevenZipFormat.BZip2)]
    [InlineData("xz", SevenZipFormat.XZ)]
    [InlineData("lzh", SevenZipFormat.Lzh)]
    [InlineData("arj", SevenZipFormat.Arj)]
    [InlineData("cab", SevenZipFormat.Cab)]
    [InlineData("iso", SevenZipFormat.Iso)]
    [InlineData("zst", SevenZipFormat.Zstd)]
    [InlineData("vhd", SevenZipFormat.Vhd)]
    public void ExtensionFormatMapping_ContainsExpectedFormat(string extension, SevenZipFormat expected)
    {
        Assert.True(Formats.ExtensionFormatMapping.TryGetValue(extension, out SevenZipFormat format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData("7Z")]
    [InlineData("ZIP")]
    [InlineData("Tar")]
    [InlineData("ZST")]
    public void ExtensionFormatMapping_IsCaseInsensitive(string extension)
    {
        Assert.True(Formats.ExtensionFormatMapping.ContainsKey(extension));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("pdf")]
    public void ExtensionFormatMapping_ReturnsFalse_ForUnknownExtension(string extension)
    {
        Assert.False(Formats.ExtensionFormatMapping.ContainsKey(extension));
    }

    [Fact]
    public void FormatGuidMapping_ContainsAllSignatureFormats()
    {
        foreach (SevenZipFormat format in Formats.FileSignatures.Keys)
        {
            Assert.True(
                Formats.FormatGuidMapping.ContainsKey(format),
                $"FormatGuidMapping is missing GUID for {format}");
        }
    }

    [Fact]
    public void FormatGuidMapping_ContainsZstd()
    {
        Assert.True(Formats.FormatGuidMapping.ContainsKey(SevenZipFormat.Zstd));
    }

    [Fact]
    public void FileSignatures_ContainsSevenZip()
    {
        Assert.True(Formats.FileSignatures.ContainsKey(SevenZipFormat.SevenZip));
        Assert.Equal([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], Formats.FileSignatures[SevenZipFormat.SevenZip]);
    }

    [Fact]
    public void FileSignatures_ContainsZstd()
    {
        Assert.True(Formats.FileSignatures.ContainsKey(SevenZipFormat.Zstd));
        Assert.Equal([0x28, 0xB5, 0x2F, 0xFD], Formats.FileSignatures[SevenZipFormat.Zstd]);
    }

    [Fact]
    public void MaxSignatureLength_MatchesLongestSignature()
    {
        int expected = Formats.FileSignatures.Values.Max(v => v.Length);
        Assert.Equal(expected, Formats.MaxSignatureLength);
    }

    [Fact]
    public void FileSignatures_Rar5Signature_IsLongerThanRar()
    {
        // RAR5 must be checked before RAR since RAR5 signature is a superset
        Assert.True(
            Formats.FileSignatures[SevenZipFormat.Rar5].Length > Formats.FileSignatures[SevenZipFormat.Rar].Length);
    }
}