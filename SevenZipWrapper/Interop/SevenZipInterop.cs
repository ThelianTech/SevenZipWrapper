namespace SevenZipWrapper.Interop;

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

// ---------------------------------------------
//  Structs
// ---------------------------------------------

[StructLayout(LayoutKind.Sequential)]
internal struct PropArray
{
    internal uint Length;
    internal IntPtr PointerValues;
}

[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public IntPtr pointerValue;
    [FieldOffset(8)] public byte byteValue;
    [FieldOffset(8)] public long longValue;
    [FieldOffset(8)] public FILETIME filetime;
    [FieldOffset(8)] public PropArray propArray;

    public readonly VarEnum VarType => (VarEnum)vt;

    public void Clear()
    {
        switch (VarType)
        {
            case VarEnum.VT_EMPTY:
                break;

            case VarEnum.VT_NULL:
            case VarEnum.VT_I2:
            case VarEnum.VT_I4:
            case VarEnum.VT_R4:
            case VarEnum.VT_R8:
            case VarEnum.VT_CY:
            case VarEnum.VT_DATE:
            case VarEnum.VT_ERROR:
            case VarEnum.VT_BOOL:
            case VarEnum.VT_I1:
            case VarEnum.VT_UI1:
            case VarEnum.VT_UI2:
            case VarEnum.VT_UI4:
            case VarEnum.VT_I8:
            case VarEnum.VT_UI8:
            case VarEnum.VT_INT:
            case VarEnum.VT_UINT:
            case VarEnum.VT_HRESULT:
            case VarEnum.VT_FILETIME:
                vt = 0;
                break;

            default:
                PropVariantClear(ref this);
                break;
        }
    }

    public readonly object? GetObject()
    {
        return VarType switch
        {
            VarEnum.VT_EMPTY => null,
            VarEnum.VT_FILETIME => DateTime.FromFileTime(longValue),
            _ => MarshalVariant()
        };
    }

    private readonly object MarshalVariant()
    {
        GCHandle handle = GCHandle.Alloc(this, GCHandleType.Pinned);

        try
        {
            return Marshal.GetObjectForNativeVariant(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }
}

// ---------------------------------------------
//  Enums
// ---------------------------------------------

internal enum AskMode : int
{
    Extract = 0,
    Test,
    Skip
}

internal enum OperationResult : int
{
    OK = 0,
    UnsupportedMethod,
    DataError,
    CRCError
}

internal enum ItemPropId : uint
{
    NoProperty = 0,

    HandlerItemIndex = 2,
    Path,
    Name,
    Extension,
    IsFolder,
    Size,
    PackedSize,
    Attributes,
    CreationTime,
    LastAccessTime,
    LastWriteTime,
    Solid,
    Commented,
    Encrypted,
    SplitBefore,
    SplitAfter,
    DictionarySize,
    CRC,
    Type,
    IsAnti,
    Method,
    HostOS,
    FileSystem,
    User,
    Group,
    Block,
    Comment,
    Position,
    Prefix,

    TotalSize = 0x1100,
    FreeSpace,
    ClusterSize,
    VolumeName,

    LocalName = 0x1200,
    Provider,

    UserDefined = 0x10000
}

internal enum ArchivePropId : uint
{
    Name = 0,
    ClassID,
    Extension,
    AddExtension,
    Update,
    KeepName,
    StartSignature,
    FinishSignature,
    Associate
}

// ---------------------------------------------
//  COM Interfaces
// ---------------------------------------------

[ComImport]
[Guid("23170F69-40C1-278A-0000-000000050000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IProgress
{
    void SetTotal(ulong total);
    void SetCompleted([In] ref ulong completeValue);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000600100000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IArchiveOpenCallback
{
    void SetTotal(IntPtr files, IntPtr bytes);
    void SetCompleted(IntPtr files, IntPtr bytes);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000500100000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICryptoGetTextPassword
{
    [PreserveSig]
    int CryptoGetTextPassword(
        [MarshalAs(UnmanagedType.BStr)] out string password);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000500110000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICryptoGetTextPassword2
{
    [PreserveSig]
    int CryptoGetTextPassword2(
        ref int passwordIsDefined,
        [MarshalAs(UnmanagedType.BStr)] out string password);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000600300000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IArchiveOpenVolumeCallback
{
    void GetProperty(ItemPropId propID, IntPtr value);

    [PreserveSig]
    int GetStream(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [MarshalAs(UnmanagedType.Interface)] out IInStream inStream);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000600400000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IInArchiveGetStream
{
    [return: MarshalAs(UnmanagedType.Interface)]
    ISequentialInStream GetStream(uint index);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000300010000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISequentialInStream
{
    uint Read(
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
        uint size);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000300020000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISequentialOutStream
{
    [PreserveSig]
    int Write(
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
        uint size,
        IntPtr processedSize);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000300030000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IInStream
{
    uint Read(
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
        uint size);

    void Seek(long offset, uint seekOrigin, IntPtr newPosition);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000300040000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOutStream
{
    [PreserveSig]
    int Write(
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
        uint size,
        IntPtr processedSize);

    void Seek(long offset, uint seekOrigin, IntPtr newPosition);

    [PreserveSig]
    int SetSize(long newSize);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000600600000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IInArchive
{
    [PreserveSig]
    int Open(
        IInStream stream,
        [In] ref ulong maxCheckStartPosition,
        [MarshalAs(UnmanagedType.Interface)] IArchiveOpenCallback? openArchiveCallback);

    void Close();

    uint GetNumberOfItems();

    void GetProperty(uint index, ItemPropId propID, ref PropVariant value);

    [PreserveSig]
    int Extract(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] uint[]? indices,
        uint numItems,
        int testMode,
        [MarshalAs(UnmanagedType.Interface)] IArchiveExtractCallback extractCallback);

    void GetArchiveProperty(uint propID, ref PropVariant value);

    uint GetNumberOfProperties();

    void GetPropertyInfo(
        uint index,
        [MarshalAs(UnmanagedType.BStr)] out string name,
        out ItemPropId propID,
        out ushort varType);

    uint GetNumberOfArchiveProperties();

    void GetArchivePropertyInfo(
        uint index,
        [MarshalAs(UnmanagedType.BStr)] string name,
        ref uint propID,
        ref ushort varType);
}

[ComImport]
[Guid("23170F69-40C1-278A-0000-000600200000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IArchiveExtractCallback
{
    void SetTotal(ulong total);
    void SetCompleted([In] ref ulong completeValue);

    [PreserveSig]
    int GetStream(
        uint index,
        [MarshalAs(UnmanagedType.Interface)] out ISequentialOutStream? outStream,
        AskMode askExtractMode);

    void PrepareOperation(AskMode askExtractMode);
    void SetOperationResult(OperationResult resultEOperationResult);
}

// ---------------------------------------------
//  Unmanaged Function Delegates
// ---------------------------------------------

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int CreateObjectDelegate(
    [In] ref Guid classID,
    [In] ref Guid interfaceID,
    [MarshalAs(UnmanagedType.Interface)] out object outObject);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetHandlerPropertyDelegate(
    ArchivePropId propID,
    ref PropVariant value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetNumberOfFormatsDelegate(out uint numFormats);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetHandlerProperty2Delegate(
    uint formatIndex,
    ArchivePropId propID,
    ref PropVariant value);

// ---------------------------------------------
//  Stream Wrappers
// ---------------------------------------------

internal class StreamWrapper(Stream baseStream) : IDisposable
{
    protected Stream BaseStream { get; } = baseStream;

    public void Dispose()
    {
        BaseStream.Close();
    }

    public virtual void Seek(long offset, uint seekOrigin, IntPtr newPosition)
    {
        long position = BaseStream.Seek(offset, (SeekOrigin)seekOrigin);

        if (newPosition != IntPtr.Zero)
        {
            Marshal.WriteInt64(newPosition, position);
        }
    }
}

internal class InStreamWrapper(Stream baseStream) : StreamWrapper(baseStream), ISequentialInStream, IInStream
{
    public uint Read(byte[] data, uint size)
    {
        return (uint)BaseStream.Read(data, 0, (int)size);
    }
}

internal class OutStreamWrapper(Stream baseStream) : StreamWrapper(baseStream), ISequentialOutStream, IOutStream
{
    public int SetSize(long newSize)
    {
        BaseStream.SetLength(newSize);
        return 0;
    }

    public int Write(byte[] data, uint size, IntPtr processedSize)
    {
        BaseStream.Write(data, 0, (int)size);

        if (processedSize != IntPtr.Zero)
        {
            Marshal.WriteInt32(processedSize, (int)size);
        }

        return 0;
    }
}