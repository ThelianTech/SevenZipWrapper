namespace SevenZipWrapper.Interop;
using System.Runtime.InteropServices;
internal sealed class SevenZipHandle : IDisposable
{
    private IntPtr _libraryHandle;
    private readonly CreateObjectDelegate _createObject;
    private bool _disposed;
    public SevenZipHandle(string libraryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
        try
        {
            _libraryHandle = NativeLibrary.Load(libraryPath);
            _createObject = Marshal.GetDelegateForFunctionPointer<CreateObjectDelegate>(RequireExport(_libraryHandle, "CreateObject"));
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or SevenZipNativeException)
        {
            if (_libraryHandle != IntPtr.Zero) NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
            if (ex is SevenZipNativeException) throw;
            throw new SevenZipNativeException(new(FailureKind.NativeLibraryFailure, "Unable to load the native archive library."), ex);
        }
    }
    internal static IntPtr RequireExport(IntPtr libraryHandle, string exportName)
    {
        if (!NativeLibrary.TryGetExport(libraryHandle, exportName, out IntPtr address))
            throw new SevenZipNativeException(new(FailureKind.NativeLibraryFailure, $"Native archive library is missing required export {exportName}."));
        return address;
    }
    public IInArchive CreateInArchive(Guid classId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return CreateInArchive(_createObject, classId);
    }
    internal static IInArchive CreateInArchive(CreateObjectDelegate createObject, Guid classId)
    {
        Guid interfaceId = typeof(IInArchive).GUID;
        int status = createObject(ref classId, ref interfaceId, out object result);
        if (status != 0 || result is not IInArchive)
        {
            if (OperatingSystem.IsWindows() && result is not null && Marshal.IsComObject(result)) Marshal.ReleaseComObject(result);
            NativeStatus.Check(status, "object creation");
            throw new SevenZipNativeException(new(FailureKind.NativeInteropFailure, "Native object creation returned an incompatible object.", NativeHResult: status));
        }
        return (IInArchive)result;
    }
    public void Dispose()
    {
        if (_disposed) return;
        if (_libraryHandle != IntPtr.Zero) NativeLibrary.Free(_libraryHandle);
        _libraryHandle = IntPtr.Zero;
        _disposed = true;
    }
}
