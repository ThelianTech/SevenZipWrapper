namespace SevenZipWrapper.Tests.CoreTests;

using System.IO.Compression;
using System.Text;

public sealed class AsyncContractTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task EntryExtractionsOnOneArchiveSerializeNativeWork()
    {
        using var archive = Open();
        var entry = Assert.Single(archive.Entries);
        using var firstOutput = new BlockingOutput();
        using var secondOutput = new ObservedOutput();
        Task first = entry.ExtractAsync(firstOutput);
        Assert.True(firstOutput.Entered.Wait(Timeout));
        Task second = entry.ExtractAsync(secondOutput);
        try
        {
            await AssertBlocked(second);
            Assert.False(secondOutput.Entered.IsSet);
        }
        finally { firstOutput.Release.Set(); }
        await Task.WhenAll(first, second).WaitAsync(Timeout);
        Assert.Equal(firstOutput.ToArray(), secondOutput.ToArray());
        Assert.True(secondOutput.Entered.IsSet);
    }

    [Fact]
    public async Task DifferentArchivesExecuteWhileAnotherArchiveIsBlocked()
    {
        using var firstArchive = Open();
        using var secondArchive = Open();
        var firstEntry = Assert.Single(firstArchive.Entries);
        var secondEntry = Assert.Single(secondArchive.Entries);
        using var firstOutput = new BlockingOutput();
        using var secondOutput = new MemoryStream();
        Task first = firstEntry.ExtractAsync(firstOutput);
        Assert.True(firstOutput.Entered.Wait(Timeout));
        try
        {
            await secondEntry.ExtractAsync(secondOutput).WaitAsync(Timeout);
            Assert.NotEmpty(secondOutput.ToArray());
            Assert.False(first.IsCompleted);
        }
        finally { firstOutput.Release.Set(); }
        await first.WaitAsync(Timeout);
    }

    [Fact]
    public async Task DisposeWaitsForNativeExtractionAndRejectsFurtherWork()
    {
        var archive = Open();
        var entry = Assert.Single(archive.Entries);
        using var output = new BlockingOutput();
        Task extraction = entry.ExtractAsync(output);
        Assert.True(output.Entered.Wait(Timeout));
        using var started = new ManualResetEventSlim();
        Task disposal = Task.Run(() => { started.Set(); archive.Dispose(); });
        Assert.True(started.Wait(Timeout));
        try { await AssertBlocked(disposal); }
        finally { output.Release.Set(); }
        await Task.WhenAll(extraction, disposal).WaitAsync(Timeout);
        using var laterOutput = new MemoryStream();
        Assert.Throws<ObjectDisposedException>(() => entry.Extract(laterOutput));
        Assert.Throws<ObjectDisposedException>(() => archive.Entries);
        archive.Dispose();
    }

    [Fact]
    public async Task OwnedInputRecursiveDisposeIsRejectedWithoutDeadlock()
    {
        var input = new ReentrantDisposeStream();
        var archive = new ArchiveFile(input, SevenZipFormat.Zip);
        input.OnDispose = archive.Dispose;
        var disposal = Task.Run(() => Assert.Throws<InvalidOperationException>(archive.Dispose));
        await disposal.WaitAsync(Timeout);
        Assert.False(input.CanRead);
        archive.Dispose();
        Assert.Throws<ObjectDisposedException>(() => archive.Entries);
    }

    [Fact]
    public async Task CancellationCanAbortWaitForAnActiveArchive()
    {
        using var archive = Open();
        var entry = Assert.Single(archive.Entries);
        using var firstOutput = new BlockingOutput();
        using var waitingOutput = new ObservedOutput();
        Task first = entry.ExtractAsync(firstOutput);
        Assert.True(firstOutput.Entered.Wait(Timeout));
        using var cts = new CancellationTokenSource();
        Task waiting = entry.ExtractAsync(waitingOutput, new ExtractionOptions { CancellationToken = cts.Token });
        try
        {
            await AssertBlocked(waiting);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Timeout));
            Assert.False(waitingOutput.Entered.IsSet);
            Assert.False(first.IsCompleted);
        }
        finally { firstOutput.Release.Set(); }
        await first.WaitAsync(Timeout);
    }

    [Fact]
    public async Task AsyncRootAndRichResultUseTheSameSuccessfulExtraction()
    {
        using var destination = new Destination();
        using var archive = Open();
        string normalRoot = Path.Combine(destination.Root, "normal");
        string richRoot = Path.Combine(destination.Root, "rich");
        await archive.ExtractAsync(normalRoot).WaitAsync(Timeout);
        var result = await archive.ExtractWithResultAsync(richRoot).WaitAsync(Timeout);
        Assert.True(result.Succeeded);
        Assert.Equal(ExtractionStatus.Succeeded, Assert.Single(result.Entries).Status);
        Assert.Equal(File.ReadAllBytes(Path.Combine(normalRoot, "entry.txt")),
            File.ReadAllBytes(Path.Combine(richRoot, "entry.txt")));
    }

    [Fact]
    public async Task EntryFileAsyncDefaultsMatchSyncOverwriteAndExplicitPolicyIsRespected()
    {
        using var destination = new Destination();
        using var archive = Open();
        var entry = Assert.Single(archive.Entries);
        string syncPath = Path.Combine(destination.Root, "sync.txt");
        string asyncPath = Path.Combine(destination.Root, "async.txt");
        string skipPath = Path.Combine(destination.Root, "skip.txt");
        const string existing = "existing caller content";
        foreach (string path in new[] { syncPath, asyncPath, skipPath }) File.WriteAllText(path, existing);

        entry.Extract(syncPath);
        await entry.ExtractAsync(asyncPath).WaitAsync(Timeout);
        await entry.ExtractAsync(skipPath, new ExtractionOptions { Overwrite = false }).WaitAsync(Timeout);

        Assert.Equal("deterministic native extraction content", File.ReadAllText(syncPath));
        Assert.Equal(File.ReadAllBytes(syncPath), File.ReadAllBytes(asyncPath));
        Assert.Equal(existing, File.ReadAllText(skipPath));
    }

    [Fact]
    public async Task AsyncNormalAndRichResultShareUnsafePathFailure()
    {
        using var destination = new Destination();
        using var archive = Open("../escaped.txt");
        var result = await archive.ExtractWithResultAsync(destination.Root).WaitAsync(Timeout);
        Assert.False(result.Succeeded);
        Assert.Equal(FailureKind.UnsafePath, result.Failure?.Kind);
        var ex = await Assert.ThrowsAsync<ArchiveExtractionException>(() => archive.ExtractAsync(destination.Root).WaitAsync(Timeout));
        Assert.Equal(result.Failure!.Kind, ex.Failure.Kind);
        Assert.Empty(Directory.GetFiles(destination.Root));
    }

    [Fact]
    public async Task PreCanceledAsyncOperationsDoNotCreateOutput()
    {
        using var destination = new Destination();
        using var archive = Open();
        var entry = Assert.Single(archive.Entries);
        using var output = new ObservedOutput();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var options = new ExtractionOptions { CancellationToken = cts.Token };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => archive.ExtractAsync(destination.Root, options));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => archive.ExtractWithResultAsync(destination.Root, options));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entry.ExtractAsync(output, options));
        Assert.False(output.Entered.IsSet);
        Assert.Empty(Directory.GetFiles(destination.Root));
    }

    private static async Task AssertBlocked(Task task) =>
        Assert.NotSame(task, await Task.WhenAny(task, Task.Delay(100)));

    private static ArchiveFile Open(string name = "entry.txt")
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var output = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
            output.Write(Encoding.UTF8.GetBytes("deterministic native extraction content"));
        }
        return new ArchiveFile(new MemoryStream(bytes.ToArray()), SevenZipFormat.Zip);
    }

    private sealed class BlockingOutput : MemoryStream
    {
        internal ManualResetEventSlim Entered { get; } = new();
        internal ManualResetEventSlim Release { get; } = new();
        public override void Write(byte[] buffer, int offset, int count)
        {
            Entered.Set();
            if (!Release.Wait(Timeout)) throw new TimeoutException("Test did not release the blocked native output.");
            base.Write(buffer, offset, count);
        }
        protected override void Dispose(bool disposing)
        {
            Release.Set();
            base.Dispose(disposing);
        }
    }

    private sealed class ObservedOutput : MemoryStream
    {
        internal ManualResetEventSlim Entered { get; } = new();
        public override void Write(byte[] buffer, int offset, int count)
        {
            Entered.Set();
            base.Write(buffer, offset, count);
        }
    }

    private sealed class ReentrantDisposeStream : MemoryStream
    {
        internal Action? OnDispose { get; set; }
        protected override void Dispose(bool disposing)
        {
            try { if (disposing) OnDispose?.Invoke(); }
            finally { base.Dispose(disposing); }
        }
    }

    private sealed class Destination : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "SZW_Async_" + Guid.NewGuid().ToString("N"));
        internal Destination() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
