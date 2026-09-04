namespace SevenZipWrapper;

using SevenZipWrapper.Callbacks;
using SevenZipWrapper.Interop;

public sealed partial class ArchiveFile
{
    public void Extract(string outputFolder, bool overwrite = false, string? password = null) =>
        Extract(outputFolder, new ExtractionOptions { Overwrite = overwrite, Password = password });

    public void Extract(string outputFolder, bool overwrite, Action<int>? onFileExtracted,
        CancellationToken cancellationToken, string? password = null) => Extract(outputFolder,
            new ExtractionOptions { Overwrite = overwrite, Password = password, Progress = onFileExtracted, CancellationToken = cancellationToken });

    public void Extract(string outputFolder, ExtractionOptions options) => ThrowFailure(ExtractWithResult(outputFolder, options));

    public ExtractionResult ExtractWithResult(string outputFolder, ExtractionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        options ??= new();
        options.Validate();
        options.CancellationToken.ThrowIfCancellationRequested();
        using var operation = EnterOperation(options.CancellationToken);
        IReadOnlyList<ArchiveEntry> entries;
        try { entries = LoadEntries(options.MaxEntryCount); }
        catch (SevenZipException ex) when (ex.Failure.Kind == FailureKind.ResourceLimitExceeded)
        { return new ExtractionResult([], ex.Failure); }
        var paths = new Dictionary<ArchiveEntry, string>();
        var skips = new Dictionary<ArchiveEntry, string>();
        foreach (var entry in entries)
        {
            try { paths.Add(entry, RootedPath.Resolve(outputFolder, entry.FileName)); }
            catch (ArchiveExtractionException ex) when (ex.Failure.Kind == FailureKind.UnsafePath)
            {
                if (options.UnsafePathPolicy == UnsafePathPolicy.Skip) skips.Add(entry, "Unsafe archive path.");
                else return PreflightFailure(entries, entry, ex.Failure);
            }
        }
        try { RootedPath.ValidateTargets(paths.Select(p => (p.Value, p.Key.IsFolder))); }
        catch (ArchiveExtractionException ex) { return PreflightFailure(entries, null, ex.Failure); }

        RootedOutputSession session;
        try { session = new RootedOutputSession(outputFolder); }
        catch (SevenZipException ex) { return PreflightFailure(entries, null, ex.Failure); }
        using var outputSession = session;
        return ExtractCore(entries, options, entry =>
        {
            if (skips.TryGetValue(entry, out var reason)) return new(null, false, reason);
            try
            {
                if (entry.IsFolder) { session.CreateDirectory(paths[entry]); return new(null, false); }
                var stream = session.OpenFile(paths[entry], options.Overwrite);
                return new(stream, true, stream is null ? "Destination already exists." : null);
            }
            catch (ArchiveExtractionException ex) when (ex.Failure.Kind == FailureKind.UnsafePath && options.UnsafePathPolicy == UnsafePathPolicy.Skip)
            { return new(null, false, "Unsafe destination path."); }
        });
    }

    public void Extract(Func<ArchiveEntry, string?> getOutputPath, string? password = null) =>
        Extract(getOutputPath, null, default, password);

    public void Extract(Func<ArchiveEntry, string?> getOutputPath, Action<int>? onFileExtracted,
        CancellationToken cancellationToken, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(getOutputPath);
        var options = new ExtractionOptions { Overwrite = true, Password = password, Progress = onFileExtracted, CancellationToken = cancellationToken };
        using var operation = EnterOperation(cancellationToken);
        var entries = LoadEntries();
        // Invoke caller selectors outside native work, preserving their original exceptions.
        var paths = entries.ToDictionary(e => e, getOutputPath);
        foreach (var entry in entries)
            if (paths[entry] is { } path) paths[entry] = Path.GetFullPath(path);
        RootedPath.ValidateTargets(paths.Where(p => p.Value is not null).Select(p => (p.Value!, p.Key.IsFolder)));
        var sessions = new Dictionary<string, RootedOutputSession>(StringComparer.OrdinalIgnoreCase);
        try
        {
            ThrowFailure(ExtractCore(entries, options, entry =>
            {
                if (paths[entry] is not { } path) return new(null, false, "Skipped by output selector.");
                string root = Path.GetPathRoot(path)!;
                if (!sessions.TryGetValue(root, out var session))
                    sessions.Add(root, session = new RootedOutputSession(root));
                string relative = path[root.Length..];
                if (entry.IsFolder) { session.CreateDirectory(relative); return new(null, false); }
                return new(session.OpenFile(relative, true), true);
            }));
        }
        finally { foreach (var session in sessions.Values) session.Dispose(); }
    }

    private sealed record Output(Stream? Stream, bool Owned, string? SkipReason = null, IDisposable? Lease = null);

    internal void ExtractEntry(ArchiveEntry entry, Stream stream, ExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        if (!stream.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(stream));
        using var operation = EnterOperation(options.CancellationToken);
        ThrowFailure(ExtractCore([entry], options, _ => new(stream, false)));
    }

    internal void ExtractEntry(ArchiveEntry entry, string path, ExtractionOptions options, bool preserveTimestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);
        using var operation = EnterOperation(options.CancellationToken);
        var result = ExtractCore([entry], options, _ => OpenExplicitOutput(entry, path, options.Overwrite));
        ThrowFailure(result);
        if (preserveTimestamp && !entry.IsFolder && entry.LastWriteTime != default &&
            result.Entries.Any(e => e.Status == ExtractionStatus.Succeeded)) File.SetLastWriteTime(path, entry.LastWriteTime);
    }

    public Task ExtractAsync(string outputFolder, ExtractionOptions? options = null) =>
        Task.Run(() => Extract(outputFolder, options ?? new()), options?.CancellationToken ?? default);

    public Task<ExtractionResult> ExtractWithResultAsync(string outputFolder, ExtractionOptions? options = null) =>
        Task.Run(() => ExtractWithResult(outputFolder, options), options?.CancellationToken ?? default);

    private static Output OpenExplicitOutput(ArchiveEntry entry, string path, bool overwrite)
    {
        RootedOutputSession? session = null;
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (entry.IsFolder) return new(null, false, Lease: new RootedOutputSession(fullPath));
            session = new RootedOutputSession(Path.GetDirectoryName(fullPath)!);
            FileStream? stream = session.OpenFile(Path.GetFileName(fullPath), overwrite);
            return new(stream, true, stream is null ? "Destination already exists." : null, session);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            session?.Dispose();
            throw new ArchiveExtractionException(new(FailureKind.DestinationConflict, "Cannot create extraction destination.", entry.FileName), ex);
        }
        catch { session?.Dispose(); throw; }
    }

    private ExtractionResult ExtractCore(IReadOnlyList<ArchiveEntry> entries, ExtractionOptions options, Func<ArchiveEntry, Output> openOutput)
    {
        options.Validate();
        options.CancellationToken.ThrowIfCancellationRequested();
        var budget = new ExtractionBudget(options);
        try { budget.Preflight(entries); }
        catch (ArchiveExtractionException ex) { return PreflightFailure(entries, null, ex.Failure); }
        var results = new List<EntryExtractionResult>();
        int completed = 0;
        foreach (var entry in entries)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            Output? output = null;
            ArchiveStreamCallback? callback = null;
            bool succeeded = false;
            try
            {
                output = openOutput(entry);
                if (output.SkipReason is { } reason)
                {
                    results.Add(new(entry, ExtractionStatus.Skipped, SkipReason: reason));
                    continue;
                }
                if (!entry.IsFolder)
                {
                    _archiveStream.State.Reset();
                    Stream limited = budget.Wrap(output.Stream!, entry, output.Owned);
                    callback = new ArchiveStreamCallback(entry.Index, limited, options.Password ?? _openPassword, cancellationToken: options.CancellationToken);
                    int status = _archive.Extract([entry.Index], 1, 0, callback);
                    _archiveStream.State.ThrowIfCaptured();
                    callback.Complete(status, options.CancellationToken);
                    if (callback.CompletedFiles != 1)
                        throw new ArchiveExtractionException(new(FailureKind.NativeInteropFailure, "Native extraction did not complete the requested entry.", entry.FileName));
                    if (output.Owned) limited.Flush();
                }
                succeeded = true;
                results.Add(new(entry, ExtractionStatus.Succeeded));
            }
            catch (SevenZipException ex) when (ReferenceEquals(ex, budget.Failure) ||
                (!_archiveStream.State.HasCapturedException && callback?.State.HasCapturedException != true))
            {
                results.Add(new(entry, ExtractionStatus.Failed, ex.Failure with { EntryName = entry.FileName }));
                if (options.FailureMode == ExtractionFailureMode.FailFast)
                {
                    foreach (var pending in entries.Skip(results.Count))
                        results.Add(new(pending, ExtractionStatus.Skipped, SkipReason: "Not attempted after failure."));
                    return new(results, ex.Failure with { EntryName = entry.FileName });
                }
            }
            finally
            {
                try
                {
                    if (output is { Owned: true, Stream: { } stream })
                    {
                        if (!succeeded && stream is FileStream file) RootedOutputSession.TryDeleteIncomplete(file);
                        stream.Dispose();
                    }
                }
                finally { output?.Lease?.Dispose(); }
            }
            if (succeeded && !entry.IsFolder) options.Progress?.Invoke(++completed);
        }
        return new(results);
    }

    private static ExtractionResult PreflightFailure(IReadOnlyList<ArchiveEntry> entries, ArchiveEntry? failed, ArchiveFailure failure) =>
        new(entries.Select(e => e == failed ? new EntryExtractionResult(e, ExtractionStatus.Failed, failure)
            : new EntryExtractionResult(e, ExtractionStatus.Skipped, SkipReason: "Not attempted: preflight failed.")), failure);

    private static void ThrowFailure(ExtractionResult result)
    {
        var failure = result.Failure ?? result.Entries.FirstOrDefault(e => e.Status == ExtractionStatus.Failed)?.Failure;
        if (failure is not null) throw new ArchiveExtractionException(failure);
    }
}
