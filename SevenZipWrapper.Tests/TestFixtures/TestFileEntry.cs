namespace SevenZipWrapper.Tests.TestFixtures;

/// <summary>
/// Describes an expected entry in a test archive for assertion purposes.
/// </summary>
public readonly record struct TestFileEntry(
    string Name,
    bool IsFolder,
    string? MD5 = null,
    string? CRC32 = null);