namespace SevenZipWrapper.Callbacks;

using SevenZipWrapper.Interop;

internal sealed class ArchivePasswordCallback(string? suppliedPassword) : IArchiveOpenCallback, ICryptoGetTextPassword
{
    internal CallbackState State { get; } = new();
    public void SetTotal(IntPtr files, IntPtr bytes) { }
    public void SetCompleted(IntPtr files, IntPtr bytes) { }
    public int CryptoGetTextPassword(out string password)
    {
        password = suppliedPassword ?? "";
        return suppliedPassword is null
            ? State.Fail(new(FailureKind.MissingPassword, "The archive requires an opening password.")) : 0;
    }
}
