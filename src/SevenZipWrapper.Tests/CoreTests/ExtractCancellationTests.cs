namespace SevenZipWrapper.Tests.CoreTests;

/// <summary>
/// Tests cancellation token support on <see cref="ArchiveFile.Extract"/> overloads.
/// </summary>
public class ExtractCancellationTests : TestBase
{
    [Fact]
    public void Extract_CancelledBeforeStart_ThrowsOperationCanceledException()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "zip.zip");
            using ArchiveFile archive = new(path);

            using CancellationTokenSource cts = new();
            cts.Cancel(); // Cancel immediately

            Assert.Throws<OperationCanceledException>(() =>
                archive.Extract(
                    outputDir,
                    overwrite: true,
                    onFileExtracted: null,
                    cancellationToken: cts.Token));
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
    public void Extract_CancelledMidExtraction_ThrowsOperationCanceledException()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "SevenZip.7z");
            using ArchiveFile archive = new(path);

            using CancellationTokenSource cts = new();

            // Cancel after the first file is extracted
            Assert.Throws<OperationCanceledException>(() =>
                archive.Extract(
                    outputDir,
                    overwrite: true,
                    onFileExtracted: count =>
                    {
                        if (count >= 1)
                        {
                            cts.Cancel();
                        }
                    },
                    cancellationToken: cts.Token));
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
    public void Extract_DefaultCancellationToken_DoesNotThrow()
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
                    cancellationToken: default));

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
    public void Extract_ExistingOverload_StringBoolString_StillWorks()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "zip.zip");
            using ArchiveFile archive = new(path);

            archive.Extract(outputDir, overwrite: true);

            Assert.True(File.Exists(Path.Combine(outputDir, "image1.jpg")));
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
    public void Extract_ExistingOverload_FuncString_StillWorks()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"SZW_Test_{Guid.NewGuid():N}");

        try
        {
            string path = Path.Combine(ResourcesDirectory, "zip.zip");
            using ArchiveFile archive = new(path);

            archive.Extract(entry =>
                entry.FileName == "image1.jpg"
                    ? Path.Combine(outputDir, entry.FileName)
                    : null);

            Assert.True(File.Exists(Path.Combine(outputDir, "image1.jpg")));
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