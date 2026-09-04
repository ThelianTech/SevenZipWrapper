namespace SevenZipWrapper.Tests.CoreTests;

public class ArchiveLifecycleTests : TestBase
{
    [Fact]
    public void LeaveOpenPreservesCallerStreamAndDefaultOwnsIt()
    {
        var kept = new MemoryStream(LoadResource("zip.zip"));
        using (var archive = ArchiveFile.Open(kept, new ArchiveOpenOptions { LeaveOpen = true })) Assert.NotEmpty(archive.Entries);
        Assert.True(kept.CanRead);
        kept.Dispose();
        var owned = new MemoryStream(LoadResource("zip.zip"));
        new ArchiveFile(owned).Dispose();
        Assert.False(owned.CanRead);
    }

    [Fact]
    public void RejectedStreamCapabilitiesFailBeforeNativeLoading()
    {
        using var stream = new NonSeekableStream();
        Assert.Throws<ArgumentException>(() => ArchiveFile.Open(stream, new ArchiveOpenOptions { LeaveOpen = true, LibraryFilePath = "missing.dll" }));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void ProbeRestoresPositionWhenReadThrows()
    {
        var failure = new IOException("probe failed");
        using var stream = new FailingProbeStream(failure) { Position = 2 };
        Assert.Same(failure, Assert.Throws<IOException>(() => ArchiveFile.Open(stream, new ArchiveOpenOptions { LeaveOpen = true })));
        Assert.Equal(2, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void FailedConstructionPreservesLeaveOpenAndReleasesOwnedStream()
    {
        using var kept = new MemoryStream(LoadResource("zip.zip"));
        Assert.Throws<SevenZipNativeException>(() => ArchiveFile.Open(kept, new ArchiveOpenOptions { LeaveOpen = true, LibraryFilePath = "missing.dll" }));
        Assert.True(kept.CanRead);
        var owned = new MemoryStream(LoadResource("zip.zip"));
        Assert.Throws<SevenZipNativeException>(() => ArchiveFile.Open(owned, new ArchiveOpenOptions { LibraryFilePath = "missing.dll" }));
        Assert.False(owned.CanRead);
    }

    [Fact]
    public void DisposedArchiveAndEntriesRejectOperations()
    {
        var archive = new ArchiveFile(new MemoryStream(LoadResource("zip.zip")));
        var entry = archive.Entries.First(e => !e.IsFolder);
        archive.Dispose();
        Assert.Throws<ObjectDisposedException>(() => archive.Entries);
        using var output = new MemoryStream();
        Assert.Throws<ObjectDisposedException>(() => entry.Extract(output));
        archive.Dispose();
    }

    [Fact]
    public void CachedEntriesCannotBeMutatedThroughCollectionCast()
    {
        using var archive = new ArchiveFile(new MemoryStream(LoadResource("zip.zip")));
        var entries = archive.Entries;
        var mutableView = Assert.IsAssignableFrom<IList<ArchiveEntry>>(entries);
        Assert.Throws<NotSupportedException>(() => mutableView.RemoveAt(0));
    }

    [Fact]
    public async Task IndependentOperationsSerializeOnOneArchive()
    {
        using var archive = new ArchiveFile(new MemoryStream(LoadResource("zip.zip")));
        Task<IReadOnlyList<ArchiveEntry>>[] operations = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => archive.Entries)).ToArray();
        var results = await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(results, entries => Assert.Same(results[0], entries));
    }

    [Fact]
    public void CallbackReentrancyIsRejectedWithoutDeadlock()
    {
        using var archive = new ArchiveFile(new MemoryStream(LoadResource("zip.zip")));
        Assert.Throws<InvalidOperationException>(() => archive.Extract(entry => { _ = archive.Entries; return null; }));
    }

    [Fact]
    public async Task GateCancellationStopsWaitAndIndependentCallsSerialize()
    {
        var gate = new OperationGate();
        using var owner = gate.Enter();
        Task waiter;
        using (ExecutionContext.SuppressFlow())
        {
            waiter = Task.Run(() =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
                Assert.Throws<OperationCanceledException>(() => gate.Enter(cts.Token));
            });
        }
        await waiter.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GateRejectsInheritedReentrancy()
    {
        var gate = new OperationGate();
        using var owner = gate.Enter();
        await Task.Run(() => Assert.Throws<InvalidOperationException>(() => gate.Enter())).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(() => gate.Dispose(() => { }));
    }

    [Fact]
    public async Task DisposeWaitsForActiveOperationThenRejectsNewWork()
    {
        var gate = new OperationGate();
        var owner = gate.Enter();
        using var started = new ManualResetEventSlim();
        int disposed = 0;
        Task disposal;
        using (ExecutionContext.SuppressFlow())
            disposal = Task.Run(() => { started.Set(); gate.Dispose(() => Interlocked.Increment(ref disposed)); });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, Volatile.Read(ref disposed));
        owner.Dispose();
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, disposed);
        Assert.Throws<ObjectDisposedException>(() => gate.Enter());
        gate.Dispose(() => Interlocked.Increment(ref disposed));
        Assert.Equal(1, disposed);
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
    private sealed class FailingProbeStream(Exception failure) : MemoryStream(new byte[20])
    {
        public override int Read(Span<byte> buffer) { Position++; throw failure; }
    }
}

