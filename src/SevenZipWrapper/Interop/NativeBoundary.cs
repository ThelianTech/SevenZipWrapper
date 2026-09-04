namespace SevenZipWrapper.Interop;

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

internal static class NativeStatus
{
    internal const int Abort = unchecked((int)0x80004004);
    internal static void Check(int status, string operation, FailureKind kind = FailureKind.NativeInteropFailure)
    {
        if (status != 0)
            throw new SevenZipNativeException(new(kind, $"Native {operation} failed.", NativeHResult: status));
    }
}

internal sealed class CallbackState
{
    private ExceptionDispatchInfo? _exception;
    internal ArchiveFailure? Failure { get; private set; }
    internal bool HasFailure => _exception is not null || Failure is not null;
    internal int Capture(Exception exception)
    {
        _exception ??= ExceptionDispatchInfo.Capture(exception);
        return NativeStatus.Abort;
    }
    internal void ThrowIfCaptured() => _exception?.Throw();
    internal int RecordResult(OperationResult result, string? entryName = null)
    {
        if (result == OperationResult.OK) return 0;
        Failure ??= new(result switch
        {
            OperationResult.UnsupportedMethod => FailureKind.UnsupportedMethod,
            OperationResult.DataError => FailureKind.DataError,
            OperationResult.CRCError => FailureKind.CrcError,
            OperationResult.WrongPassword => FailureKind.InvalidPassword,
            OperationResult.NotArchive => FailureKind.InvalidArchive,
            _ => FailureKind.Unknown
        }, "Native entry extraction failed.", entryName, NativeOperationResult: (int)result);
        return NativeStatus.Abort;
    }
    internal void Complete(int status, CancellationToken cancellationToken = default)
    {
        ThrowIfCaptured();
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
            throw new ArchiveExtractionException(Failure with { NativeHResult = status == 0 ? null : status });
        if (status != 0)
            throw new ArchiveExtractionException(new(FailureKind.NativeInteropFailure, "Native extraction failed.", NativeHResult: status));
    }
}

internal static class MetadataConverter
{
    internal static T? Convert<T>(object? value, VarEnum variantType)
    {
        if (variantType is VarEnum.VT_EMPTY or VarEnum.VT_NULL) return default;
        try
        {
            object converted;
            Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (target == typeof(string) && variantType == VarEnum.VT_BSTR && value is string text) converted = text;
            else if (target == typeof(bool) && variantType == VarEnum.VT_BOOL && value is bool flag) converted = flag;
            else if (target == typeof(DateTime) && variantType is VarEnum.VT_FILETIME or VarEnum.VT_DATE && value is DateTime time) converted = time;
            else if (variantType is VarEnum.VT_I1 or VarEnum.VT_UI1 or VarEnum.VT_I2 or VarEnum.VT_UI2 or VarEnum.VT_I4 or VarEnum.VT_UI4 or VarEnum.VT_I8 or VarEnum.VT_UI8 or VarEnum.VT_INT or VarEnum.VT_UINT)
            {
                decimal number = value switch { sbyte n => n, byte n => n, short n => n, ushort n => n, int n => n, uint n => n, long n => n, ulong n => n, _ => throw new InvalidCastException() };
                converted = target == typeof(ulong) ? (object)checked((ulong)number) : target == typeof(uint) ? checked((uint)number) : target == typeof(long) ? checked((long)number) : target == typeof(int) ? checked((int)number) : target == typeof(ushort) ? checked((ushort)number) : target == typeof(short) ? checked((short)number) : target == typeof(byte) ? checked((byte)number) : target == typeof(sbyte) ? checked((sbyte)number) : throw new InvalidCastException();
            }
            else throw new InvalidCastException();
            return (T)converted;
        }
        catch (Exception ex) when (ex is InvalidCastException or OverflowException)
        {
            throw new SevenZipNativeException(new(FailureKind.NativeInteropFailure, $"Unsupported or out-of-range metadata type {variantType} for {typeof(T).Name}."), ex);
        }
    }
}
