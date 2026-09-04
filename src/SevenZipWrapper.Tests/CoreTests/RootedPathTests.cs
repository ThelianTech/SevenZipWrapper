namespace SevenZipWrapper.Tests.CoreTests;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public sealed class RootedPathTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "SZW_Rooted_" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_sandbox, "output");
    public RootedPathTests() => Directory.CreateDirectory(_sandbox);

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/../../escape")]
    [InlineData("C:\\escape")]
    [InlineData("C:escape")]
    [InlineData("\\\\server\\share\\escape")]
    [InlineData("\\escape")]
    [InlineData("a\\..\\../escape")]
    [InlineData("../outputBackup/escape")]
    [InlineData("safe:stream")]
    [InlineData("dir. /file")]
    [InlineData("NUL.txt")]
    [InlineData("COM¹")]
    [InlineData("a/..")]
    public void UnsafeNamesAreRejected(string name)
    {
        ArchiveExtractionException ex = Assert.Throws<ArchiveExtractionException>(() => RootedPath.Resolve(Root, name));
        Assert.Equal(FailureKind.UnsafePath, ex.Failure.Kind);
    }

    [Theory]
    [InlineData("file.txt", "file.txt")]
    [InlineData("nested/file.txt", "nested\\file.txt")]
    [InlineData("a/.././file.txt", "file.txt")]
    [InlineData("a\\b/c", "a\\b\\c")]
    public void SafeNamesAreNormalizedWithEitherRootEnding(string name, string expected)
    {
        Assert.Equal(expected, RootedPath.Resolve(Root, name));
        Assert.Equal(expected, RootedPath.Resolve(Root + "\\", name));
    }

    [Fact]
    public void DuplicateAndAncestorConflictsAreRejected()
    {
        Assert.Throws<ArchiveExtractionException>(() => RootedPath.ValidateTargets([("file", false), ("FILE", false)]));
        Assert.Throws<ArchiveExtractionException>(() => RootedPath.ValidateTargets([("dir", false), ("dir\\file", false)]));
        Assert.Throws<ArchiveExtractionException>(() => RootedPath.ValidateTargets([("dir\\file", false), ("dir", false)]));
        RootedPath.ValidateTargets([("dir\\file", false), ("dir", true)]);
    }

    [Fact]
    public void OrdinaryFilesOverwriteSkipAndIncompleteCleanupUseHandles()
    {
        using (RootedOutputSession session = new(Root))
        {
            session.CreateDirectory("empty");
            using (FileStream file = session.OpenFile("nested/file", false)!) file.Write(Encoding.UTF8.GetBytes("first contents"));
            Assert.Null(session.OpenFile("nested/file", false));
            Assert.Equal("first contents", File.ReadAllText(Path.Combine(Root, "nested", "file")));
        }
        using (RootedOutputSession session = new(Root))
        {
            using (FileStream file = session.OpenFile("nested/file", true)!) file.WriteByte(65);
            Assert.Equal("A", File.ReadAllText(Path.Combine(Root, "nested", "file")));
            using (FileStream incomplete = session.OpenFile("incomplete", false)!)
            {
                incomplete.WriteByte(1);
                RootedOutputSession.TryDeleteIncomplete(incomplete);
            }
            Assert.False(File.Exists(Path.Combine(Root, "incomplete")));
        }
        Directory.Move(Root, Root + "Moved"); // Directory leases must be released.
    }

    [Fact]
    public void LeasesPreventRootAndNestedDirectoryRelocation()
    {
        using RootedOutputSession session = new(Root);
        session.CreateDirectory("nested");
        Assert.Throws<IOException>(() => Directory.Move(Root, Root + "Moved"));
        Assert.Throws<IOException>(() => Directory.Move(Path.Combine(Root, "nested"), Path.Combine(_sandbox, "moved")));
        using FileStream file = session.OpenFile("nested/file", false)!;
        file.WriteByte(42);
        Assert.False(Directory.Exists(Path.Combine(_sandbox, "moved")));
    }

    [Fact]
    public void ExistingFilesystemShapesConflict()
    {
        using RootedOutputSession session = new(Root);
        session.CreateDirectory("directory");
        Assert.Equal(FailureKind.DestinationConflict, Assert.Throws<ArchiveExtractionException>(() => session.OpenFile("directory", true)).Failure.Kind);
        using (session.OpenFile("file", false)) { }
        Assert.Equal(FailureKind.DestinationConflict, Assert.Throws<ArchiveExtractionException>(() => session.CreateDirectory("file/child")).Failure.Kind);
    }

    [Fact]
    public void JunctionCannotRedirectOutputOrRoot()
    {
        string outside = Path.Combine(_sandbox, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(Root);
        string junction = Path.Combine(Root, "link");
        CreateJunction(junction, outside);
        try
        {
            using RootedOutputSession session = new(Root);
            Assert.Equal(FailureKind.UnsafePath, Assert.Throws<ArchiveExtractionException>(() => session.OpenFile("link/escape", true)).Failure.Kind);
            Assert.Equal(FailureKind.UnsafePath, Assert.Throws<ArchiveExtractionException>(() => new RootedOutputSession(junction)).Failure.Kind);
            Assert.False(File.Exists(Path.Combine(outside, "escape")));
        }
        finally { Directory.Delete(junction); }
    }

    [Fact]
    public void InPlaceJunctionMutationOfLeasedDirectoryCannotRedirectOutput()
    {
        string outside = Path.Combine(_sandbox, "outside");
        Directory.CreateDirectory(outside);
        string junction = Path.Combine(Root, "held");
        using (RootedOutputSession session = new(Root))
        {
            session.CreateDirectory("held");
            SetJunction(junction, outside);
            Assert.Equal(FailureKind.UnsafePath, Assert.Throws<ArchiveExtractionException>(() => session.OpenFile("held/escape", true)).Failure.Kind);
            Assert.False(File.Exists(Path.Combine(outside, "escape")));
        }
        Directory.Delete(junction);
    }

    [Fact]
    public void ExistingHardLinkIsNotTruncated()
    {
        string outside = Path.Combine(_sandbox, "sentinel");
        File.WriteAllText(outside, "unchanged");
        Directory.CreateDirectory(Root);
        if (!CreateHardLinkW(Path.Combine(Root, "linked"), outside, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using RootedOutputSession session = new(Root);
        Assert.Equal(FailureKind.UnsafePath, Assert.Throws<ArchiveExtractionException>(() => session.OpenFile("linked", true)).Failure.Kind);
        Assert.Equal("unchanged", File.ReadAllText(outside));
    }

    [Fact]
    public void SameFileIdentityCannotBeOverwrittenTwiceInOneSession()
    {
        using RootedOutputSession session = new(Root);
        using (FileStream file = session.OpenFile("long file name.txt", true)!) file.WriteByte(42);
        Assert.Equal(FailureKind.DestinationConflict, Assert.Throws<ArchiveExtractionException>(
            () => session.OpenFile("LONG FILE NAME.TXT", true)).Failure.Kind);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(Path.Combine(Root, "long file name.txt")));
    }

    private static void CreateJunction(string path, string target)
    {
        Directory.CreateDirectory(path);
        SetJunction(path, target);
    }

    private static void SetJunction(string path, string target)
    {
        using SafeFileHandle handle = CreateFileW(path, 0x40000000, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + target);
        byte[] print = Encoding.Unicode.GetBytes(target);
        byte[] buffer = new byte[16 + substitute.Length + 2 + print.Length + 2];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
        BitConverter.GetBytes((ushort)(buffer.Length - 8)).CopyTo(buffer, 4);
        BitConverter.GetBytes((ushort)substitute.Length).CopyTo(buffer, 10);
        BitConverter.GetBytes((ushort)(substitute.Length + 2)).CopyTo(buffer, 12);
        BitConverter.GetBytes((ushort)print.Length).CopyTo(buffer, 14);
        substitute.CopyTo(buffer, 16);
        print.CopyTo(buffer, 16 + substitute.Length + 2);
        if (!DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose() { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true); }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, IntPtr output, int outputSize, out int bytes, IntPtr overlapped);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string path, string target, IntPtr security);
}
