namespace SevenZipWrapper.Tests.CoreTests;

/// <summary>
/// Tests progress callback support on <see cref="ArchiveFile.Extract"/> overloads.
/// </summary>
public class ExtractProgressTests : TestBase
{
    [Fact]
    public void Extract_WithCallback_FiresForEachFile()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "zip.zip");
            using ArchiveFile archive = new(path);

            List<int> progressValues = [];

            archive.Extract(
                outputDir,
                overwrite: true,
                onFileExtracted: count => progressValues.Add(count),
                cancellationToken: CancellationToken.None);

            int expectedFileCount = archive.Entries.Count(e => !e.IsFolder);
            Assert.Equal(expectedFileCount, progressValues.Count);
            Assert.Equal(expectedFileCount, progressValues[^1]);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Extract_WithCallback_CountIncrementsSequentially()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "SevenZip.7z");
            using ArchiveFile archive = new(path);

            List<int> progressValues = [];

            archive.Extract(
                outputDir,
                overwrite: true,
                onFileExtracted: count => progressValues.Add(count),
                cancellationToken: CancellationToken.None);

            // Each invocation should be exactly 1 greater than the previous
            for (int i = 0; i < progressValues.Count; i++)
            {
                Assert.Equal(i + 1, progressValues[i]);
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Extract_WithCallback_SevenZipFormat_FiresForEachFile()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "SevenZip.7z");
            using ArchiveFile archive = new(path);

            List<int> progressValues = [];

            archive.Extract(
                outputDir,
                overwrite: true,
                onFileExtracted: count => progressValues.Add(count),
                cancellationToken: CancellationToken.None);

            int expectedFileCount = archive.Entries.Count(e => !e.IsFolder);
            Assert.Equal(expectedFileCount, progressValues.Count);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Extract_WithCallback_RarFormat_FiresForEachFile()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "rar.rar");
            using ArchiveFile archive = new(path);

            List<int> progressValues = [];

            archive.Extract(
                outputDir,
                overwrite: true,
                onFileExtracted: count => progressValues.Add(count),
                cancellationToken: CancellationToken.None);

            int expectedFileCount = archive.Entries.Count(e => !e.IsFolder);
            Assert.Equal(expectedFileCount, progressValues.Count);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Extract_NullCallback_DoesNotThrow()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "zip.zip");
            using ArchiveFile archive = new(path);

            Exception? ex = Record.Exception(() =>
                archive.Extract(
                    outputDir,
                    overwrite: true,
                    onFileExtracted: null,
                    cancellationToken: CancellationToken.None));

            Assert.Null(ex);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Extract_FuncOverload_WithCallback_FiresForEachFile()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "zip.zip");
            using ArchiveFile archive = new(path);

            List<int> progressValues = [];

            archive.Extract(
                entry => entry.IsFolder
                    ? null
                    : Path.Combine(outputDir, entry.FileName ?? string.Empty),
                onFileExtracted: count => progressValues.Add(count),
                cancellationToken: CancellationToken.None);

            int expectedFileCount = archive.Entries.Count(e => !e.IsFolder);
            Assert.Equal(expectedFileCount, progressValues.Count);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }
}