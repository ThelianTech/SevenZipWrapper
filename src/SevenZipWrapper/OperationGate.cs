namespace SevenZipWrapper;

internal sealed class OperationGate
{
    private sealed class Context { internal volatile bool Active; }
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _sync = new();
    private readonly AsyncLocal<Context?> _context = new();
    private readonly ManualResetEventSlim _disposeComplete = new();
    private bool _stopping;

    internal IDisposable Enter(CancellationToken cancellationToken = default)
    {
        RejectReentrancy();
        lock (_sync) ObjectDisposedException.ThrowIf(_stopping, this);
        _semaphore.Wait(cancellationToken);
        lock (_sync)
        {
            if (_stopping) { _semaphore.Release(); throw new ObjectDisposedException(nameof(ArchiveFile)); }
        }
        var context = new Context { Active = true };
        _context.Value = context;
        return new Lease(this, context);
    }

    internal void Dispose(Action cleanup)
    {
        RejectReentrancy();
        bool owner;
        lock (_sync) { owner = !_stopping; _stopping = true; }
        if (!owner) { _disposeComplete.Wait(); return; }
        _semaphore.Wait();
        var context = new Context { Active = true };
        _context.Value = context;
        try { cleanup(); }
        finally
        {
            context.Active = false;
            _context.Value = null;
            _semaphore.Release();
            _disposeComplete.Set();
        }
    }

    private void RejectReentrancy()
    {
        if (_context.Value is { Active: true })
            throw new InvalidOperationException("Nested operations on the same archive are not supported.");
    }
    private sealed class Lease(OperationGate owner, Context context) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            context.Active = false;
            owner._context.Value = null;
            owner._semaphore.Release();
        }
    }
}
