namespace SevenZipWrapper;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Opens each component relative to an already held directory. No archive-controlled
/// descendant is opened through a full pathname. Directory leases prevent relocation
/// until the caller has closed every output stream and disposed this session.
/// Existing hard-linked overwrite targets are rejected. This does not isolate files
/// from an external process deliberately adding a new hard link outside the root:
/// Windows allows that metadata operation despite file sharing restrictions.
/// </summary>
internal sealed class RootedOutputSession : IDisposable
{
    private readonly string _root;
    private readonly List<SafeFileHandle> _leases = [];
    private readonly Dictionary<string, SafeFileHandle> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(uint Volume, uint High, uint Low)> _fileTargets = [];
    private bool _disposed;

    internal RootedOutputSession(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        if (_root.StartsWith(@"\\?\", StringComparison.Ordinal) || _root.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw RootedPath.Unsafe(root);
        string volume = Path.GetPathRoot(_root)!;
        try
        {
            SafeFileHandle current = CreateFileW(volume, 0x100081, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
            if (current.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                current.Dispose();
                throw OutputError(error, root);
            }
            _leases.Add(current);
            Verify(current, true, root);
            foreach (string component in _root[volume.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = OpenDirectory(current, component);
                _leases.Add(current);
            }
            _directories.Add(string.Empty, current);
        }
        catch { Dispose(); throw; }
    }

    internal void CreateDirectory(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        GetDirectory(RootedPath.Resolve(_root, relativePath));
    }

    internal FileStream? OpenFile(string relativePath, bool overwrite)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string relative = RootedPath.Resolve(_root, relativePath);
        SafeFileHandle parent = GetDirectory(Path.GetDirectoryName(relative) ?? string.Empty);
        string name = Path.GetFileName(relative);
        SafeFileHandle? handle = OpenRelative(parent, name, false, overwrite ? 3u : 2u, out int status);
        if (handle is null)
        {
            if (!overwrite && status == unchecked((int)0xC0000035))
            {
                // Inspect the colliding object without following it. A reparse point or
                // directory is a conflict even when an ordinary existing file is skipped.
                using SafeFileHandle existing = OpenRelative(parent, name, null, 1, out status)
                    ?? throw StatusError(status, relative);
                Verify(existing, false, relative);
                return null;
            }
            throw StatusError(status, relative);
        }
        try
        {
            FileInformation information = Verify(handle, false, relative);
            if (information.NumberOfLinks > 1) throw RootedPath.Unsafe(relative);
            if (!_fileTargets.Add((information.VolumeSerialNumber, information.FileIndexHigh, information.FileIndexLow)))
                throw RootedPath.Conflict(relative);
            FileStream stream = new(handle, FileAccess.Write);
            try
            {
                if (overwrite) stream.SetLength(0);
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }
        catch { handle.Dispose(); throw; }
    }

    private SafeFileHandle GetDirectory(string relative)
    {
        SafeFileHandle current = _directories[string.Empty];
        Verify(current, true, string.Empty);
        string prefix = string.Empty;
        foreach (string component in relative.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            prefix = prefix.Length == 0 ? component : prefix + "\\" + component;
            if (_directories.TryGetValue(prefix, out SafeFileHandle? known))
            {
                Verify(known, true, prefix);
                current = known;
                continue;
            }
            current = OpenDirectory(current, component);
            _leases.Add(current);
            _directories.Add(prefix, current);
        }
        return current;
    }

    private static SafeFileHandle OpenDirectory(SafeFileHandle parent, string name)
    {
        SafeFileHandle directory = OpenRelative(parent, name, true, 3, out int status)
            ?? throw StatusError(status, name);
        try { Verify(directory, true, name); return directory; }
        catch { directory.Dispose(); throw; }
    }

    private static FileInformation Verify(SafeFileHandle handle, bool directory, string name)
    {
        if (!GetFileInformationByHandle(handle, out FileInformation info))
            throw OutputError(Marshal.GetLastWin32Error(), name);
        if ((info.Attributes & 0x400) != 0) throw RootedPath.Unsafe(name);
        if (((info.Attributes & 0x10) != 0) != directory) throw RootedPath.Conflict(name);
        return info;
    }

    private static SafeFileHandle? OpenRelative(SafeFileHandle parent, string name, bool? directory, uint disposition, out int status)
    {
        IntPtr text = Marshal.StringToHGlobalUni(name);
        IntPtr unicodePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        bool referenced = false;
        try
        {
            UnicodeString unicode = new() { Length = checked((ushort)(name.Length * 2)), MaximumLength = checked((ushort)(name.Length * 2)), Buffer = text };
            Marshal.StructureToPtr(unicode, unicodePointer, false);
            parent.DangerousAddRef(ref referenced);
            ObjectAttributes attributes = new()
            {
                Length = Marshal.SizeOf<ObjectAttributes>(), RootDirectory = parent.DangerousGetHandle(),
                ObjectName = unicodePointer, Attributes = 0x1040 // OBJ_CASE_INSENSITIVE | OBJ_DONT_REPARSE
            };
            // Directory leases need FILE_LIST_DIRECTORY (read-data access): metadata-only
            // handles do not participate in the sharing check that prevents rename.
            uint access = directory == false ? 0x40110080u : directory == true ? 0x100081u : 0x100080u;
            uint options = 0x00200020u | (directory == true ? 1u : directory == false ? 0x40u : 0u);
            status = NtCreateFile(out SafeFileHandle handle, access, ref attributes, out _, IntPtr.Zero,
                0x80, directory == false ? 1u : 3u, disposition, options, IntPtr.Zero, 0);
            if (status < 0) { handle.Dispose(); return null; }
            return handle;
        }
        finally
        {
            if (referenced) parent.DangerousRelease();
            Marshal.FreeHGlobal(unicodePointer);
            Marshal.FreeHGlobal(text);
        }
    }

    private static ArchiveExtractionException StatusError(int status, string name)
    {
        if (status == unchecked((int)0xC000050B) || status == unchecked((int)0xC0000279)) return RootedPath.Unsafe(name);
        if (status == unchecked((int)0xC0000103) || status == unchecked((int)0xC00000BA)
            || status == unchecked((int)0xC0000035)) return RootedPath.Conflict(name);
        return OutputError(unchecked((int)RtlNtStatusToDosError(status)), name);
    }

    private static ArchiveExtractionException OutputError(int error, string name) =>
        new(new(FailureKind.OutputFailure, "Unable to open the extraction destination safely.", name), new Win32Exception(error));

    /// <summary>Best-effort removal of only the incomplete object represented by this handle.</summary>
    internal static void TryDeleteIncomplete(FileStream stream)
    {
        try
        {
            byte delete = 1;
            SetFileInformationByHandle(stream.SafeFileHandle, 4, ref delete, 1);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = _leases.Count - 1; i >= 0; i--) _leases[i].Dispose();
        _leases.Clear();
        _directories.Clear();
        _fileTargets.Clear();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString { internal ushort Length, MaximumLength; internal IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        internal int Length; internal IntPtr RootDirectory, ObjectName; internal uint Attributes;
        internal IntPtr SecurityDescriptor, SecurityQualityOfService;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock { internal IntPtr Status; internal UIntPtr Information; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        internal uint Attributes; internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        internal uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint desiredAccess, ref ObjectAttributes attributes,
        out IoStatusBlock ioStatusBlock, IntPtr allocationSize, uint fileAttributes, uint shareAccess,
        uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength);
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, ref byte information, uint size);
}
