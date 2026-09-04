namespace SevenZipWrapper.Callbacks;
using SevenZipWrapper.Interop;
internal class ArchiveStreamsCallback : IArchiveExtractCallback, ICryptoGetTextPassword
{
    private readonly IList<Stream?> _streams;
    private readonly string? _password;
    private readonly Action<int>? _onFileExtracted;
    private readonly CancellationToken _cancellationToken;
    private readonly IReadOnlyList<string?>? _entryNames;
    private readonly Func<uint, Stream?>? _getStream;
    private int _filesExtracted;
    private bool _currentEntryHasStream;
    private string? _currentEntryName;
    internal CallbackState State { get; }
    internal int CompletedFiles => _filesExtracted;
    internal ArchiveStreamsCallback(IList<Stream?> streams, string? password = null,
        Action<int>? onFileExtracted = null, CancellationToken cancellationToken = default,
        CallbackState? state = null, IReadOnlyList<string?>? entryNames = null)
    {
        _streams = streams; _password = password; _onFileExtracted = onFileExtracted;
        _cancellationToken = cancellationToken; _entryNames = entryNames; State = state ?? new();
    }
    protected ArchiveStreamsCallback(Func<uint, Stream?> getStream, string? password = null, CallbackState? state = null, CancellationToken cancellationToken = default)
        : this(Array.Empty<Stream?>(), password, cancellationToken: cancellationToken, state: state) => _getStream = getStream;
    public int SetTotal(ulong total) => State.HasFailure ? NativeStatus.Abort : 0;
    public int SetCompleted(ref ulong completeValue) => State.HasFailure || _cancellationToken.IsCancellationRequested ? NativeStatus.Abort : 0;
    public int PrepareOperation(AskMode askExtractMode) => State.HasFailure ? NativeStatus.Abort : 0;
    public int CryptoGetTextPassword(out string password)
    {
        password = _password ?? "";
        if (_password is null) return State.Fail(new(FailureKind.MissingPassword, "This entry requires a password.", _currentEntryName));
        return State.HasFailure ? NativeStatus.Abort : 0;
    }
    public int GetStream(uint index, out ISequentialOutStream? outStream, AskMode askExtractMode)
    {
        outStream = null; _currentEntryHasStream = false; _currentEntryName = null;
        try
        {
            if (State.HasFailure || _cancellationToken.IsCancellationRequested) return NativeStatus.Abort;
            if (askExtractMode != AskMode.Extract) return 0;
            if (_entryNames is not null) _currentEntryName = _entryNames[checked((int)index)];
            Stream? stream = _getStream is null ? _streams[checked((int)index)] : _getStream(index);
            if (stream is null) return 0;
            _currentEntryHasStream = true;
            outStream = new OutStreamWrapper(stream, State, leaveOpen: true);
            return 0;
        }
        catch (Exception ex) { return State.Capture(ex); }
    }
    public int SetOperationResult(OperationResult resultEOperationResult)
    {
        try
        {
            if (State.HasFailure) return NativeStatus.Abort;
            if (!_currentEntryHasStream) return 0;
            int status = State.RecordResult(resultEOperationResult, _currentEntryName);
            if (status != 0) return status;
            _filesExtracted++;
            _onFileExtracted?.Invoke(_filesExtracted);
            return 0;
        }
        catch (Exception ex) { return State.Capture(ex); }
    }
    internal void Complete(int status, CancellationToken cancellationToken = default) => State.Complete(status, cancellationToken);
}
