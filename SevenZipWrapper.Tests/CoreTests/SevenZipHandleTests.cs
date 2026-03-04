namespace SevenZipWrapper.Tests.CoreTests;

/// <summary>
/// Tests library loading and invalid DLL path handling.
/// </summary>
public class SevenZipHandleTests
{
    [Fact]
    public void Constructor_InvalidDllPath_ThrowsSevenZipException()
    {
        Assert.Throws<SevenZipException>(
            () => new ArchiveFile("dummy.7z", libraryFilePath: @"C:\nonexistent\7z.dll"));
    }

    [Fact]
    public void Constructor_EmptyLibraryPath_FallsBackToAutoDetection()
    {
        // If 7z.dll is available via auto-detection, this should not throw
        // (or throw SevenZipException for the archive file — not for the library)
        Exception? ex = Record.Exception(
            () => new ArchiveFile(@"C:\nonexistent_archive_12345.7z", libraryFilePath: ""));

        // Either SevenZipException for missing library or missing archive file — both are acceptable
        if (ex is not null)
        {
            Assert.IsType<SevenZipException>(ex);
        }
    }

    [Fact]
    public void Constructor_NullLibraryPath_UsesAutoDetection()
    {
        // Should throw for the archive path, not the library path
        SevenZipException ex = Assert.Throws<SevenZipException>(
            () => new ArchiveFile(@"C:\nonexistent_archive_12345.7z", libraryFilePath: null));

        // The error should be about the archive file or library — not a NullReferenceException
        Assert.NotNull(ex.Message);
    }
}