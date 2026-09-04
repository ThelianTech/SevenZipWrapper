namespace SevenZipWrapper.Interop;

using System.Runtime.InteropServices;

/// <summary>
/// Manages the lifetime of the native 7z.dll library and creates <see cref="IInArchive"/> instances.
/// Replaces the old <c>Kernel32Dll</c>, <c>SafeLibraryHandle</c>, and <c>SevenZipHandle</c> classes
/// with the modern <see cref="NativeLibrary"/> API.
/// </summary>
internal sealed class SevenZipHandle : IDisposable
{
    private IntPtr _libraryHandle;
    private bool _disposed;

    /// <summary>
    /// Loads <paramref name="libraryPath"/> and validates it exports <c>GetHandlerProperty</c>.
    /// </summary>
    /// <param name="libraryPath">Full path to <c>7z.dll</c>.</param>
    /// <exception cref="SevenZipException">The library could not be loaded or is not a valid 7z.dll.</exception>
    public SevenZipHandle(string libraryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);

        _libraryHandle = NativeLibrary.Load(libraryPath);

        if (!NativeLibrary.TryGetExport(_libraryHandle, "GetHandlerProperty", out _))
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
            throw new SevenZipException($"'{libraryPath}' is not a valid 7z.dll — missing GetHandlerProperty export.");
        }
    }

    /// <summary>
    /// Creates an <see cref="IInArchive"/> COM instance for the specified format class ID.
    /// </summary>
    /// <param name="classId">The 7z format GUID (from <see cref="Formats.FormatGuidMapping"/>).</param>
    /// <returns>An <see cref="IInArchive"/> instance, or <see langword="null"/> if creation failed.</returns>
    /// <exception cref="ObjectDisposedException">This handle has been disposed.</exception>
    public IInArchive? CreateInArchive(Guid classId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IntPtr procAddress = NativeLibrary.GetExport(_libraryHandle, "CreateObject");
        CreateObjectDelegate createObject = Marshal.GetDelegateForFunctionPointer<CreateObjectDelegate>(procAddress);

        Guid interfaceId = typeof(IInArchive).GUID;
        createObject(ref classId, ref interfaceId, out object result);

        return result as IInArchive;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_libraryHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
        }

        _disposed = true;
    }
}