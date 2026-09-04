namespace SevenZipWrapper.Tests.CoreTests;

using System.IO.Compression;
using System.Text;

public sealed class ExtractionIntegrityTests
{
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("../../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("safe/../../outside.txt")]
    [InlineData("C:\\outside.txt")]
    [InlineData("\\\\server\\share\\outside.txt")]
    [InlineData("\\outside.txt")]
    [InlineData("../rootBackup/outside.txt")]
    public void ActualHostileZipFailsWithoutWritingOutsideRoot(string name)
    {
        using var destination = new Destination();
        byte[] zip = Zip((name, "hostile"));
        using var archive = Open(zip);
        var result = archive.ExtractWithResult(destination.Root, new ExtractionOptions());
        Assert.False(result.Succeeded);
        Assert.Equal(FailureKind.UnsafePath, result.Failure?.Kind);
        Assert.Empty(Directory.GetFiles(destination.Parent, "*", SearchOption.AllDirectories));
        using var normal = Open(zip);
        Assert.Equal(result.Failure!.Kind, Assert.Throws<ArchiveExtractionException>(() =>
            normal.Extract(destination.Root, new ExtractionOptions())).Failure.Kind);
    }

    [Fact]
    public void SkipReportsUnsafeEntryAndExtractsSafeEntries()
    {
        using var destination = new Destination();
        using var archive = Open(Zip(("nested/good.txt", "good"), ("../outside.txt", "bad")));
        var result = archive.ExtractWithResult(destination.Root, new ExtractionOptions { UnsafePathPolicy = UnsafePathPolicy.Skip });
        Assert.True(result.Succeeded);
        Assert.Null(result.Failure);
        Assert.Equal(2, result.Entries.Count);
        Assert.Single(result.Entries, e => e.Status == ExtractionStatus.Succeeded);
        var skipped = Assert.Single(result.Entries, e => e.Status == ExtractionStatus.Skipped);
        Assert.False(string.IsNullOrWhiteSpace(skipped.SkipReason));
        Assert.Equal("good", File.ReadAllText(Path.Combine(destination.Root, "nested", "good.txt")));
        Assert.False(File.Exists(Path.Combine(destination.Parent, "outside.txt")));
    }

    [Theory]
    [InlineData("same.txt", "same.txt")]
    [InlineData("same.txt", "SAME.TXT")]
    [InlineData("folder/same.txt", "folder/./same.txt")]
    [InlineData("folder/same.txt", "folder\\same.txt")]
    [InlineData("shape", "shape/child.txt")]
    public void ArchiveTargetConflictsFailEvenWithOverwrite(string first, string second)
    {
        using var destination = new Destination();
        using var archive = Open(Zip((first, "one"), (second, "two")));
        var result = archive.ExtractWithResult(destination.Root, new ExtractionOptions { Overwrite = true });
        Assert.False(result.Succeeded);
        Assert.Equal(FailureKind.DestinationConflict, result.Failure?.Kind);
        Assert.Empty(Directory.GetFiles(destination.Root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingFileDirectoryShapeConflictPreservesDestination(bool destinationIsDirectory)
    {
        using var destination = new Destination();
        string target = Path.Combine(destination.Root, "shape");
        if (destinationIsDirectory) Directory.CreateDirectory(target);
        else File.WriteAllText(target, "existing");
        using var archive = Open(Zip((destinationIsDirectory ? "shape" : "shape/child.txt", "new")));
        var result = archive.ExtractWithResult(destination.Root, new ExtractionOptions { Overwrite = true });
        Assert.False(result.Succeeded);
        Assert.Equal(FailureKind.DestinationConflict, result.Failure?.Kind);
        if (destinationIsDirectory) Assert.True(Directory.Exists(target));
        else Assert.Equal("existing", File.ReadAllText(target));
    }

    [Theory]
    [InlineData(ExtractionFailureMode.FailFast)]
    [InlineData(ExtractionFailureMode.ContinueAndReport)]
    public void ExistingOutputFailureIsReportedAndContinuationIsExplicit(ExtractionFailureMode mode)
    {
        using var destination = new Destination();
        Directory.CreateDirectory(Path.Combine(destination.Root, "a.txt"));
        File.WriteAllText(Path.Combine(destination.Root, "a.txt", "existing.txt"), "existing");
        using var archive = Open(Zip(("a.txt", "replacement"), ("b.txt", "later")));
        var result = archive.ExtractWithResult(destination.Root, new ExtractionOptions { FailureMode = mode });
        Assert.False(result.Succeeded);
        Assert.Equal("existing", File.ReadAllText(Path.Combine(destination.Root, "a.txt", "existing.txt")));
        Assert.Contains(result.Entries, e => e.Status == ExtractionStatus.Failed && e.Failure?.Kind == FailureKind.DestinationConflict);
        Assert.Equal(mode == ExtractionFailureMode.ContinueAndReport, File.Exists(Path.Combine(destination.Root, "b.txt")));
        if (mode == ExtractionFailureMode.ContinueAndReport)
            Assert.Contains(result.Entries, e => e.Status == ExtractionStatus.Succeeded && e.Entry.FileName == "b.txt");
        using var normal = Open(Zip(("a.txt", "replacement"), ("b.txt", "later")));
        Assert.Equal(FailureKind.DestinationConflict, Assert.Throws<ArchiveExtractionException>(() =>
            normal.Extract(destination.Root, new ExtractionOptions { FailureMode = mode })).Failure.Kind);
    }

    [Fact]
    public void OverwriteFalseReportsExistingFileAsSkippedAndPreservesContents()
    {
        using var destination = new Destination();
        File.WriteAllText(Path.Combine(destination.Root, "a.txt"), "existing");
        using var archive = Open(Zip(("a.txt", "replacement")));
        var result = archive.ExtractWithResult(destination.Root, new ExtractionOptions());
        Assert.True(result.Succeeded);
        var skipped = Assert.Single(result.Entries);
        Assert.Equal(ExtractionStatus.Skipped, skipped.Status);
        Assert.False(string.IsNullOrWhiteSpace(skipped.SkipReason));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(destination.Root, "a.txt")));
    }

    public static IEnumerable<object[]> Limits()
    {
        yield return [new ExtractionOptions { MaxEntryCount = 1 }];
        yield return [new ExtractionOptions { MaxEntrySize = 2 }];
        yield return [new ExtractionOptions { MaxTotalSize = 5 }];
        yield return [new ExtractionOptions { MaxCompressionRatio = 0.5 }];
        yield return [new ExtractionOptions { MaxPathDepth = 1 }];
    }

    [Theory]
    [MemberData(nameof(Limits))]
    public void QuotasFailNormalAndRichExtractionWithSameFailure(ExtractionOptions options)
    {
        using var destination = new Destination();
        byte[] zip = Zip(("nested/a.txt", "1234"), ("b.txt", "5678"));
        using var archive = Open(zip);
        var result = archive.ExtractWithResult(destination.Root, options);
        Assert.False(result.Succeeded);
        Assert.Equal(FailureKind.ResourceLimitExceeded, result.Failure?.Kind);
        using var normal = Open(zip);
        var ex = Assert.Throws<ArchiveExtractionException>(() => normal.Extract(destination.Root, options));
        Assert.Equal(result.Failure!.Kind, ex.Failure.Kind);
        Assert.Empty(Directory.GetFiles(destination.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void RuntimeEntryBudgetRejectsBytesBeyondForgedMetadataBeforeWritingThem()
    {
        using var archive = Open(Zip(("a", "a")));
        var entry = new ArchiveEntry(archive, 0) { FileName = "a", Size = 1, PackedSize = 1 };
        var budget = new ExtractionBudget(new ExtractionOptions { MaxEntrySize = 3 });
        budget.Preflight([entry]);
        using var target = new MemoryStream();
        using var output = budget.Wrap(target, entry);
        output.Write([1, 2], 0, 2);
        var ex = Assert.Throws<ArchiveExtractionException>(() => output.Write([3, 4], 0, 2));
        Assert.Equal(FailureKind.ResourceLimitExceeded, ex.Failure.Kind);
        Assert.Equal(new byte[] { 1, 2 }, target.ToArray());
    }

    [Fact]
    public void RuntimeTotalBudgetAccumulatesAcrossEntries()
    {
        using var archive = Open(Zip(("a", "a")));
        var first = new ArchiveEntry(archive, 0) { FileName = "a", Size = 1, PackedSize = 1 };
        var second = new ArchiveEntry(archive, 1) { FileName = "b", Size = 1, PackedSize = 1 };
        var budget = new ExtractionBudget(new ExtractionOptions { MaxTotalSize = 3 });
        budget.Preflight([first, second]);
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        using var firstOutput = budget.Wrap(a, first);
        using var secondOutput = budget.Wrap(b, second);
        firstOutput.Write([1, 2], 0, 2);
        Assert.Equal(FailureKind.ResourceLimitExceeded, Assert.Throws<ArchiveExtractionException>(() =>
            secondOutput.Write([3, 4], 0, 2)).Failure.Kind);
        Assert.Empty(b.ToArray());
    }

    [Fact]
    public void CallerOverflowExceptionIsNotMisclassifiedAsResourceLimit()
    {
        using var archive = Open(Zip(("a", "a")));
        var entry = new ArchiveEntry(archive, 0) { FileName = "a", Size = 1, PackedSize = 1 };
        var sentinel = new OverflowException("caller stream overflow");
        using var target = new OverflowOutput(sentinel);
        using var output = new ExtractionBudget(new ExtractionOptions()).Wrap(target, entry);
        Assert.Same(sentinel, Assert.Throws<OverflowException>(() => output.Write([1], 0, 1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedOutputIoFailureUsesStructuredFailure(bool failOnFlush)
    {
        using var archive = Open(Zip(("a", "a")));
        var entry = new ArchiveEntry(archive, 0) { FileName = "a", Size = 1, PackedSize = 1 };
        var sentinel = new IOException("owned output failure");
        using var target = new FailingOutput(sentinel, failOnFlush);
        var budget = new ExtractionBudget(new ExtractionOptions());
        using var output = budget.Wrap(target, entry, owned: true);
        var ex = Assert.Throws<ArchiveExtractionException>(() =>
        {
            if (failOnFlush) output.Flush();
            else output.Write([1], 0, 1);
        });
        Assert.Equal(FailureKind.OutputFailure, ex.Failure.Kind);
        Assert.Equal("a", ex.Failure.EntryName);
        Assert.Same(sentinel, ex.InnerException);
        Assert.Same(ex, budget.Failure);
    }

    [Fact]
    public void CallerSevenZipExceptionSurvivesCanonicalEntryExtractionUnchanged()
    {
        using var archive = Open(Zip(("a", "payload")));
        var sentinel = new SevenZipException("caller archive exception");
        using var output = new FailingOutput(sentinel);
        Assert.Same(sentinel, Assert.Throws<SevenZipException>(() => archive.Entries.Single().Extract(output)));
    }

    [Fact]
    public void CorruptEntryExplicitFileExtractionDeletesIncompleteOutput()
    {
        byte[] zip = Zip(("broken.txt", "payload"));
        int payloadOffset = 30 + BitConverter.ToUInt16(zip, 26) + BitConverter.ToUInt16(zip, 28);
        zip[payloadOffset] ^= 0x40;
        using var destination = new Destination();
        using var archive = Open(zip);
        string path = Path.Combine(destination.Root, "caller-name.txt");
        var ex = Assert.Throws<ArchiveExtractionException>(() => archive.Entries.Single().Extract(path));
        Assert.Equal(FailureKind.CrcError, ex.Failure.Kind);
        Assert.Equal("broken.txt", ex.Failure.EntryName);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void CorruptStoredZipNeverReportsSuccessAndRemovesIncompleteOutput()
    {
        byte[] zip = Zip(("broken.txt", "uniquely recognizable payload"));
        int payloadOffset = 30 + BitConverter.ToUInt16(zip, 26) + BitConverter.ToUInt16(zip, 28);
        zip[payloadOffset] ^= 0x40;
        using var destination = new Destination();
        using var archive = Open(zip);
        var result = archive.ExtractWithResult(destination.Root, new ExtractionOptions());
        Assert.False(result.Succeeded);
        Assert.Equal(FailureKind.CrcError, result.Failure?.Kind);
        var failed = Assert.Single(result.Entries, e => e.Status == ExtractionStatus.Failed);
        Assert.NotNull(failed.Failure?.NativeOperationResult);
        Assert.False(File.Exists(Path.Combine(destination.Root, "broken.txt")));
        using var normal = Open(zip);
        Assert.Equal(FailureKind.CrcError, Assert.Throws<ArchiveExtractionException>(() =>
            normal.Extract(destination.Root, new ExtractionOptions())).Failure.Kind);
    }

    private static ArchiveFile Open(byte[] zip) => new(new MemoryStream(zip), SevenZipFormat.Zip);

    private static byte[] Zip(params (string Name, string Content)[] files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var file in files)
            {
                var entry = zip.CreateEntry(file.Name, CompressionLevel.NoCompression);
                using var output = entry.Open();
                output.Write(Encoding.UTF8.GetBytes(file.Content));
            }
        return buffer.ToArray();
    }

    private sealed class Destination : IDisposable
    {
        public string Parent { get; } = Path.Combine(Path.GetTempPath(), "SZW_Integrity_" + Guid.NewGuid().ToString("N"));
        public string Root => Path.Combine(Parent, "root");
        public Destination() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Parent, recursive: true);
    }

    private sealed class OverflowOutput(OverflowException failure) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw failure;
    }

    private sealed class FailingOutput(Exception failure, bool failOnFlush = false) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!failOnFlush) throw failure;
            base.Write(buffer, offset, count);
        }
        public override void Flush()
        {
            if (failOnFlush) throw failure;
            base.Flush();
        }
    }
}
