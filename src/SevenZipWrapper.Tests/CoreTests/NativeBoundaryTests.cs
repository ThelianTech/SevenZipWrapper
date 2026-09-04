namespace SevenZipWrapper.Tests.CoreTests;

using System.Runtime.InteropServices;
using SevenZipWrapper.Callbacks;
using SevenZipWrapper.Interop;

public class NativeBoundaryTests
{
    [Fact]
    public void NativeFailurePreservesStatus()
    {
        var ex = Assert.Throws<ArchiveExtractionException>(() => new CallbackState().Complete(unchecked((int)0x80004005)));
        Assert.Equal(FailureKind.NativeInteropFailure, ex.Failure.Kind);
        Assert.Equal(unchecked((int)0x80004005), ex.Failure.NativeHResult);
    }

    [Theory]
    [InlineData(OperationResult.CRCError, FailureKind.CrcError)]
    [InlineData(OperationResult.DataError, FailureKind.DataError)]
    [InlineData(OperationResult.UnsupportedMethod, FailureKind.UnsupportedMethod)]
    [InlineData(OperationResult.WrongPassword, FailureKind.InvalidPassword)]
    [InlineData((OperationResult)1234, FailureKind.Unknown)]
    internal void EntryFailureSurvivesSuccessfulOverallStatus(OperationResult result, FailureKind kind)
    {
        using var output = new MemoryStream();
        var callback = new ArchiveStreamsCallback(new Stream?[] { output }, entryNames: new[] { "entry" });
        Assert.Equal(0, callback.GetStream(0, out _, AskMode.Extract));
        Assert.Equal(NativeStatus.Abort, callback.SetOperationResult(result));
        var ex = Assert.Throws<ArchiveExtractionException>(() => callback.Complete(0));
        Assert.Equal(kind, ex.Failure.Kind);
        Assert.Equal((int)result, ex.Failure.NativeOperationResult);
        Assert.Equal("entry", ex.Failure.EntryName);
    }

    [Fact]
    public void IntentionalSkipDoesNotBecomeCorruption()
    {
        var callback = new ArchiveStreamsCallback(new Stream?[] { null });
        callback.GetStream(0, out _, AskMode.Extract);
        Assert.Equal(0, callback.SetOperationResult(OperationResult.CRCError));
        callback.Complete(0);
    }

    [Fact]
    public void PasswordRequestDistinguishesAbsentAndExplicitEmpty()
    {
        var missing = new ArchiveStreamsCallback(Array.Empty<Stream?>());
        Assert.Equal(NativeStatus.Abort, missing.CryptoGetTextPassword(out _));
        Assert.Equal(FailureKind.MissingPassword, Assert.Throws<ArchiveExtractionException>(() => missing.Complete(NativeStatus.Abort)).Failure.Kind);
        var empty = new ArchiveStreamsCallback(Array.Empty<Stream?>(), password: "");
        Assert.Equal(0, empty.CryptoGetTextPassword(out string password));
        Assert.Equal("", password);
    }

    [Fact]
    public void NativeVariantDecodesScalarsAndRejectsUnsupportedTypes()
    {
        var unsigned = new PropVariant { vt = (ushort)VarEnum.VT_UI8, longValue = -1 };
        Assert.Equal(ulong.MaxValue, Assert.IsType<ulong>(unsigned.GetObject()));
        var boolean = new PropVariant { vt = (ushort)VarEnum.VT_BOOL, longValue = -1 };
        Assert.True(Assert.IsType<bool>(boolean.GetObject()));
        var invalidTime = new PropVariant { vt = (ushort)VarEnum.VT_FILETIME, longValue = -1 };
        Assert.Throws<SevenZipNativeException>(() => invalidTime.GetObject());
        var unsupported = new PropVariant { vt = (ushort)VarEnum.VT_UNKNOWN };
        Assert.Throws<SevenZipNativeException>(() => unsupported.GetObject());
    }

    [Fact]
    public void ProgressExceptionIsRethrownByIdentityAfterNativeUnwind()
    {
        using var output = new MemoryStream();
        var sentinel = new InvalidOperationException("callback failure");
        var callback = new ArchiveStreamsCallback(new Stream?[] { output }, onFileExtracted: _ => throw sentinel);
        callback.GetStream(0, out _, AskMode.Extract);
        Assert.Equal(NativeStatus.Abort, callback.SetOperationResult(OperationResult.OK));
        Assert.Same(sentinel, Assert.Throws<InvalidOperationException>(() => callback.Complete(NativeStatus.Abort)));
    }

    [Fact]
    public void StreamExceptionsAreCapturedAndPreserved()
    {
        var sentinel = new IOException("stream failure");
        using var stream = new ThrowingStream(sentinel);
        var input = new InStreamWrapper(stream, leaveOpen: true);
        Assert.Equal(NativeStatus.Abort, input.Read(new byte[1], 1, IntPtr.Zero));
        Assert.Same(sentinel, Assert.Throws<IOException>(() => input.State.ThrowIfCaptured()));
        var output = new OutStreamWrapper(stream, leaveOpen: true);
        Assert.Equal(NativeStatus.Abort, output.Write(new byte[1], 1, IntPtr.Zero));
        Assert.Same(sentinel, Assert.Throws<IOException>(() => output.State.Complete(NativeStatus.Abort)));
    }

    [Fact]
    public void MetadataUsesExplicitCheckedTypes()
    {
        Assert.Equal(uint.MaxValue, MetadataConverter.Convert<uint>(uint.MaxValue, VarEnum.VT_UI4));
        Assert.Equal(7UL, MetadataConverter.Convert<ulong>(7U, VarEnum.VT_UI4));
        Assert.Equal("3", MetadataConverter.Convert<string>((byte)3, VarEnum.VT_UI1, allowNumericString: true));
        Assert.Throws<SevenZipNativeException>(() => MetadataConverter.Convert<string>((byte)3, VarEnum.VT_UI1));
        Assert.True(MetadataConverter.Convert<bool>(true, VarEnum.VT_BOOL));
        Assert.Equal("test", MetadataConverter.Convert<string>("test", VarEnum.VT_BSTR));
        Assert.Equal(0U, MetadataConverter.Convert<uint>(null, VarEnum.VT_EMPTY));
        Assert.Throws<SevenZipNativeException>(() => MetadataConverter.Convert<uint>(-1L, VarEnum.VT_I8));
        Assert.Throws<SevenZipNativeException>(() => MetadataConverter.Convert<uint>(ulong.MaxValue, VarEnum.VT_UI8));
        Assert.Throws<SevenZipNativeException>(() => MetadataConverter.Convert<uint>("123", VarEnum.VT_BSTR));
    }

    [Fact]
    public void CreationChecksNativeFailureAndWrongInterface()
    {
        var ex = Assert.Throws<SevenZipNativeException>(() => SevenZipHandle.CreateInArchive(FailingCreation, Guid.Empty));
        Assert.Equal(unchecked((int)0x80004005), ex.Failure.NativeHResult);
        Assert.Throws<SevenZipNativeException>(() => SevenZipHandle.CreateInArchive(WrongCreation, Guid.Empty));
    }

    [Fact]
    public void MissingRequiredExportHasControlledDiagnostic()
    {
        IntPtr library = NativeLibrary.Load("kernel32.dll");
        try
        {
            var ex = Assert.Throws<SevenZipNativeException>(() => SevenZipHandle.RequireExport(library, "CreateObject"));
            Assert.Equal(FailureKind.NativeLibraryFailure, ex.Failure.Kind);
            Assert.Contains("CreateObject", ex.Message);
        }
        finally { NativeLibrary.Free(library); }
    }

    private static int FailingCreation(ref Guid classId, ref Guid interfaceId, out object result) { result = null!; return unchecked((int)0x80004005); }
    private static int WrongCreation(ref Guid classId, ref Guid interfaceId, out object result) { result = new object(); return 0; }
    private sealed class ThrowingStream(Exception failure) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw failure;
        public override void Write(byte[] buffer, int offset, int count) => throw failure;
    }
}

