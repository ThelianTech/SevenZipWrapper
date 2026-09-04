namespace SevenZipWrapper.Callbacks;
using SevenZipWrapper.Interop;
internal sealed class ArchiveStreamCallback(uint fileNumber, Stream stream, string? password = null, CallbackState? state = null, CancellationToken cancellationToken = default)
    : ArchiveStreamsCallback(index => index == fileNumber ? stream : null, password, state, cancellationToken);
