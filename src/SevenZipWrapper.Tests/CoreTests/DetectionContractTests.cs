namespace SevenZipWrapper.Tests.CoreTests;

public sealed class DetectionContractTests : TestBase
{
    [Theory]
    [InlineData("7573746172")] // TAR's ustar marker is not a leading signature.
    [InlineData("4344303031")] // ISO volume-descriptor identifier is not at offset zero.
    [InlineData("2D6C68")] // LZH method marker is not at offset zero.
    [InlineData("7801730D626260")] // Generic compressed bytes do not identify DMG.
    [InlineData("636F6E6563746978")] // VHD cookie placement depends on its variant.
    [InlineData("504B")]
    [InlineData("504B0000")]
    public void UnreliableLeadingBytesDoNotSelectAFormat(string hex)
    {
        using MemoryStream stream = new(Convert.FromHexString(hex).Concat(new byte[32]).ToArray());
        SevenZipException failure = Assert.ThrowsAny<SevenZipException>(() => ArchiveFile.Open(stream, new ArchiveOpenOptions { LeaveOpen = true }));
        Assert.Equal(FailureKind.UnsupportedFormat, failure.Failure.Kind);
    }

    [Fact]
    public void EmptyZipIsDetectedFromItsEndRecord()
    {
        using MemoryStream stream = new();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true)) { }
        stream.Position = 0;
        using ArchiveFile archive = ArchiveFile.Open(stream, new ArchiveOpenOptions { LeaveOpen = true });
        Assert.Equal(SevenZipFormat.Zip, archive.Format);
        Assert.Empty(archive.Entries);
    }

    [Fact]
    public void ReliableSignatureWinsOverContradictoryExtension()
    {
        string path = Path.Combine(Path.GetTempPath(), "SZW_Detection_" + Guid.NewGuid().ToString("N") + ".7z");
        File.WriteAllBytes(path, LoadResource("zip.zip"));
        try
        {
            using ArchiveFile archive = ArchiveFile.Open(path, new ArchiveOpenOptions());
            Assert.Equal(SevenZipFormat.Zip, archive.Format);
            Assert.NotEmpty(archive.Entries);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExplicitFormatOverridesContentDetection()
    {
        using MemoryStream stream = new(LoadResource("zip.zip"));
        using ArchiveFile archive = ArchiveFile.Open(stream, new ArchiveOpenOptions { Format = SevenZipFormat.SevenZip, LeaveOpen = true });
        Assert.Equal(SevenZipFormat.SevenZip, archive.Format);
        SevenZipException failure = Assert.ThrowsAny<SevenZipException>(() => _ = archive.Entries);
        Assert.Equal(FailureKind.InvalidArchive, failure.Failure.Kind);
    }

    [Fact]
    public void ExplicitPathFormatOverridesMisleadingExtension()
    {
        string path = Path.Combine(Path.GetTempPath(), "SZW_Detection_" + Guid.NewGuid().ToString("N") + ".zip");
        File.WriteAllBytes(path, LoadResource("lzh.lzh"));
        try
        {
            using ArchiveFile archive = ArchiveFile.Open(path, new ArchiveOpenOptions { Format = SevenZipFormat.Lzh });
            Assert.Equal(SevenZipFormat.Lzh, archive.Format);
            Assert.NotEmpty(archive.Entries);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(SevenZipFormat.Undefined)]
    [InlineData(SevenZipFormat.Msi)] // Public value exists, but has no explicit native mapping.
    [InlineData((SevenZipFormat)(-1))]
    [InlineData((SevenZipFormat)int.MaxValue)]
    public void InvalidExplicitFormatProducesControlledFailure(SevenZipFormat format)
    {
        using MemoryStream stream = new(LoadResource("zip.zip"));
        SevenZipException failure = Assert.ThrowsAny<SevenZipException>(() =>
            ArchiveFile.Open(stream, new ArchiveOpenOptions { Format = format, LeaveOpen = true }));
        Assert.Equal(FailureKind.UnsupportedFormat, failure.Failure.Kind);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void SignatureDetectionHandlesOneByteReadsAndRestoresPosition()
    {
        byte[] archiveBytes = LoadResource("SevenZip.7z");
        using OneByteReadStream stream = new(archiveBytes);
        using ArchiveFile archive = ArchiveFile.Open(stream, new ArchiveOpenOptions { LeaveOpen = true });
        Assert.Equal(SevenZipFormat.SevenZip, archive.Format);
        Assert.Equal(0, stream.Position);
        Assert.InRange(stream.BytesRead, 6, Formats.MaxSignatureLength);
        Assert.NotEmpty(archive.Entries);
    }

    [Fact]
    public void DetectionRestoresNonzeroStartingPosition()
    {
        byte[] data = new byte[] { 0, 0, 0 }.Concat(LoadResource("SevenZip.7z")).ToArray();
        using OneByteReadStream stream = new(data) { Position = 3 };
        using ArchiveFile archive = ArchiveFile.Open(stream, new ArchiveOpenOptions { LeaveOpen = true });
        Assert.Equal(SevenZipFormat.SevenZip, archive.Format);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void DetectionMappingsOnlyReferenceDefinedMappedFormats()
    {
        SevenZipFormat[] intended = Enum.GetValues<SevenZipFormat>()
            .Where(f => f is not SevenZipFormat.Undefined and not SevenZipFormat.Msi).ToArray();
        foreach (SevenZipFormat format in intended)
            Assert.True(Formats.FormatGuidMapping.TryGetValue(format, out Guid guid) && guid != Guid.Empty, format.ToString());
        Assert.Equal(Formats.FormatGuidMapping.Count, Formats.FormatGuidMapping.Values.Distinct().Count());
        foreach ((string extension, SevenZipFormat format) in Formats.ExtensionFormatMapping)
        {
            Assert.False(string.IsNullOrWhiteSpace(extension));
            Assert.True(Enum.IsDefined(format));
            Assert.True(Formats.FormatGuidMapping.ContainsKey(format));
        }
        foreach ((SevenZipFormat format, byte[] signature) in Formats.FileSignatures)
        {
            Assert.True(Enum.IsDefined(format));
            Assert.True(Formats.FormatGuidMapping.ContainsKey(format));
            Assert.NotEmpty(signature);
        }
        string[] signatures = Formats.FileSignatures.Values.Select(Convert.ToHexString).ToArray();
        Assert.Equal(signatures.Length, signatures.Distinct(StringComparer.Ordinal).Count());
        Assert.False(Formats.FormatGuidMapping.ContainsKey(SevenZipFormat.Undefined));
        Assert.False(Formats.FormatGuidMapping.ContainsKey(SevenZipFormat.Msi));
    }

    private sealed class OneByteReadStream(byte[] data) : MemoryStream(data)
    {
        internal int BytesRead { get; private set; }
        public override int Read(Span<byte> buffer)
        {
            byte[] one = new byte[Math.Min(1, buffer.Length)];
            int count = Read(one, 0, one.Length);
            one.AsSpan(0, count).CopyTo(buffer);
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, Math.Min(1, count));
            BytesRead += read;
            return read;
        }
    }
}
