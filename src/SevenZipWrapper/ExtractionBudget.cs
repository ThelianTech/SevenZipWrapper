namespace SevenZipWrapper;

internal sealed class ExtractionBudget(ExtractionOptions options)
{
    private ulong _total;
    internal ArchiveExtractionException? Failure { get; private set; }

    internal void Preflight(IReadOnlyList<ArchiveEntry> entries)
    {
        if (options.MaxEntryCount is { } count && entries.Count > count) Fail(null);
        ulong total = 0;
        foreach (var entry in entries)
        {
            try { total = checked(total + entry.Size); }
            catch (OverflowException) { Fail(entry); }
            Check(entry, entry.Size, total);
            if (options.MaxPathDepth is { } depth &&
                (entry.FileName ?? "").Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Length > depth)
                Fail(entry);
        }
    }

    internal Stream Wrap(Stream output, ArchiveEntry entry, bool owned = false) => new BudgetStream(output, this, entry, options.CancellationToken, owned);

    private void Check(ArchiveEntry entry, ulong size, ulong total)
    {
        if (options.MaxEntrySize is { } max && size > max ||
            options.MaxTotalSize is { } totalMax && total > totalMax ||
            options.MaxCompressionRatio is { } ratio && size > 0 &&
            (entry.PackedSize == 0 || (double)size / entry.PackedSize > ratio)) Fail(entry);
    }

    private void Fail(ArchiveEntry? entry)
    {
        Failure = new(new(FailureKind.ResourceLimitExceeded, "Extraction resource limit exceeded.", entry?.FileName));
        throw Failure;
    }

    private sealed class BudgetStream(Stream target, ExtractionBudget budget, ArchiveEntry entry, CancellationToken token, bool owned) : Stream
    {
        private ulong _written;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => target.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush()
        {
            try { target.Flush(); }
            catch (Exception ex) when (owned && ex is IOException or UnauthorizedAccessException) { OutputFailed(ex); }
        }
        public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            token.ThrowIfCancellationRequested();
            ulong size = 0, total = 0;
            try
            {
                size = checked(_written + (ulong)count);
                total = checked(budget._total + (ulong)count);
            }
            catch (OverflowException) { budget.Fail(entry); }
            budget.Check(entry, size, total);
            try { target.Write(buffer, offset, count); }
            catch (Exception ex) when (owned && ex is IOException or UnauthorizedAccessException) { OutputFailed(ex); }
            _written = size;
            budget._total = total;
        }

        private void OutputFailed(Exception exception)
        {
            budget.Failure = new(new(FailureKind.OutputFailure, "Unable to write extraction output.", entry.FileName), exception);
            throw budget.Failure;
        }
    }
}
