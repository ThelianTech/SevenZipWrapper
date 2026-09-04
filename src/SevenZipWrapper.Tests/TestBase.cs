using SevenZipWrapper.Tests.Helpers;
using SevenZipWrapper.Tests.TestFixtures;

namespace SevenZipWrapper.Tests;

/// <summary>
/// Abstract base class providing shared test data and stream extraction verification.
/// </summary>
public abstract class TestBase
{
    /// <summary>
    /// Path to the <c>Resources</c> directory containing test archive files.
    /// </summary>
    protected static readonly string ResourcesDirectory =
        Path.Combine(AppContext.BaseDirectory, "Resources");

    /// <summary>
    /// Expected entries for archives that include a folder entry.
    /// </summary>
    protected static readonly IReadOnlyList<TestFileEntry> TestEntriesWithFolder =
    [
        new("image1.jpg", IsFolder: false, MD5: "b3144b66569ab0052b4019a2b4c07a31"),
        new("image2.jpg", IsFolder: false, MD5: "8fdd4013edcf04b335ac3a9ce0c13887"),
        new("testFolder", IsFolder: true),
        new("testFolder\\image3.jpg", IsFolder: false, MD5: "24ffd227340432596fe61ef6300098ad"),
    ];

    /// <summary>
    /// Expected entries for archives that do not include a folder entry.
    /// </summary>
    protected static readonly IReadOnlyList<TestFileEntry> TestEntriesWithoutFolder =
    [
        new("image1.jpg", IsFolder: false, MD5: "b3144b66569ab0052b4019a2b4c07a31"),
        new("image2.jpg", IsFolder: false, MD5: "8fdd4013edcf04b335ac3a9ce0c13887"),
        new("testFolder\\image3.jpg", IsFolder: false, MD5: "24ffd227340432596fe61ef6300098ad"),
    ];

    /// <summary>
    /// Opens an archive from a byte array and verifies that all expected entries are present
    /// with matching MD5/CRC-32 hashes when extracted to a stream.
    /// </summary>
    protected static void AssertExtractToStream(
        byte[] archiveBytes,
        IReadOnlyList<TestFileEntry> expected,
        SevenZipFormat? format = null,
        string? password = null)
    {
        using MemoryStream archiveStream = new(archiveBytes);
        using ArchiveFile archiveFile = new(archiveStream, format);

        foreach (TestFileEntry testEntry in expected)
        {
            ArchiveEntry? entry = archiveFile.Entries
                .FirstOrDefault(e => e.FileName == testEntry.Name && e.IsFolder == testEntry.IsFolder);

            Assert.NotNull(entry);

            if (testEntry.IsFolder)
            {
                continue;
            }

            using MemoryStream entryStream = new();
            entry.Extract(entryStream, password);

            byte[] data = entryStream.ToArray();

            if (testEntry.MD5 is not null)
            {
                Assert.Equal(testEntry.MD5, data.MD5String());
            }

            if (testEntry.CRC32 is not null)
            {
                Assert.Equal(testEntry.CRC32, data.CRC32String());
            }
        }
    }

    /// <summary>
    /// Opens an archive from a file path and verifies that all expected entries are present
    /// with matching MD5 hashes when extracted to a stream.
    /// </summary>
    protected static void AssertExtractToStreamFromFile(
        string archiveFilePath,
        IReadOnlyList<TestFileEntry> expected,
        string? password = null)
    {
        using ArchiveFile archiveFile = new(archiveFilePath);

        foreach (TestFileEntry testEntry in expected)
        {
            ArchiveEntry? entry = archiveFile.Entries
                .FirstOrDefault(e => e.FileName == testEntry.Name && e.IsFolder == testEntry.IsFolder);

            Assert.NotNull(entry);

            if (testEntry.IsFolder)
            {
                continue;
            }

            using MemoryStream entryStream = new();
            entry.Extract(entryStream, password);

            byte[] data = entryStream.ToArray();

            if (testEntry.MD5 is not null)
            {
                Assert.Equal(testEntry.MD5, data.MD5String());
            }
        }
    }

    /// <summary>
    /// Reads a test archive file from the <c>Resources</c> directory as a byte array.
    /// </summary>
    protected static byte[] LoadResource(string fileName)
    {
        string path = Path.Combine(ResourcesDirectory, fileName);
        Assert.True(File.Exists(path), $"Test resource not found: {path}");
        return File.ReadAllBytes(path);
    }
}