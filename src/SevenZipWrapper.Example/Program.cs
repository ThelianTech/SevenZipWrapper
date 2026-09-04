using SevenZipWrapper;

const string SampleZip = @"Resources/sample.zip";
const string Sample7z = @"Resources/sample.7z";
const string OutputDir = "Output";

// Clean up from previous runs
if (Directory.Exists(OutputDir))
{
    Directory.Delete(OutputDir, recursive: true);
}

// ──────────────────────────────────────────────
// 1. Extract all files
// ──────────────────────────────────────────────
Console.WriteLine("═══ 1. Extract all files ═══");

using (ArchiveFile archive = new(Sample7z))
{
    archive.Extract(OutputDir);
    Console.WriteLine($"  Extracted {archive.Entries.Count} entries to '{OutputDir}'");
}

// ──────────────────────────────────────────────
// 2. Extract with overwrite
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 2. Extract with overwrite ═══");

using (ArchiveFile archive = new(Sample7z))
{
    archive.Extract(OutputDir, overwrite: true);
    Console.WriteLine("  Extracted again with overwrite enabled — existing files replaced.");
}

// ──────────────────────────────────────────────
// 3. Extract with password (demonstration only — sample archive is not encrypted)
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 3. Extract with password ═══");
Console.WriteLine("  (Skipped — sample archive is not encrypted.)");
Console.WriteLine("  Usage: archive.Extract(outputDir, password: \"secret\");");

// ──────────────────────────────────────────────
// 4. List entries + properties
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 4. List entries + properties ═══");

using (ArchiveFile archive = new(Sample7z))
{
    Console.WriteLine($"  Format: {archive.Format}");
    Console.WriteLine($"  {"Name",-35} {"Size",10}  {"Packed",10}  {"Folder",6}  Method");
    Console.WriteLine($"  {new string('─', 85)}");

    foreach (ArchiveEntry entry in archive.Entries)
    {
        Console.WriteLine(
            $"  {entry.FileName,-35} {entry.Size,10:N0}  {entry.PackedSize,10:N0}  {entry.IsFolder,6}  {entry.Method}");
    }
}

// ──────────────────────────────────────────────
// 5. Extract single entry to file
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 5. Extract single entry to file ═══");

using (ArchiveFile archive = new(SampleZip))
{
    ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);
    string outputPath = Path.Combine(OutputDir, "single", entry.FileName!);

    entry.Extract(outputPath);
    Console.WriteLine($"  Extracted '{entry.FileName}' → '{outputPath}' ({new FileInfo(outputPath).Length:N0} bytes)");
}

// ──────────────────────────────────────────────
// 6. Extract single entry to stream
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 6. Extract single entry to stream ═══");

using (ArchiveFile archive = new(SampleZip))
{
    ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);

    using MemoryStream ms = new();
    entry.Extract(ms);

    Console.WriteLine($"  Extracted '{entry.FileName}' to MemoryStream — {ms.Length:N0} bytes");
}

// ──────────────────────────────────────────────
// 7. Selective extraction via callback
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 7. Selective extraction via callback ═══");

using (ArchiveFile archive = new(Sample7z))
{
    string selectiveDir = Path.Combine(OutputDir, "selective");

    archive.Extract(entry =>
    {
        // Only extract .jpg files, skip everything else
        if (!entry.IsFolder && entry.FileName?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true)
        {
            string path = Path.Combine(selectiveDir, entry.FileName);
            Console.WriteLine($"  ✓ Extracting: {entry.FileName}");
            return path;
        }

        Console.WriteLine($"  ✗ Skipping:   {entry.FileName}");
        return null;
    });
}

// ──────────────────────────────────────────────
// 8. Open from Stream (not file path)
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 8. Open from Stream ═══");

using (FileStream fs = File.OpenRead(SampleZip))
using (ArchiveFile archive = new(fs, SevenZipFormat.Zip))
{
    Console.WriteLine($"  Opened from FileStream — Format: {archive.Format}, Entries: {archive.Entries.Count}");
}

// ──────────────────────────────────────────────
// 9. Format auto-detection from signature
// ──────────────────────────────────────────────
Console.WriteLine("\n═══ 9. Format auto-detection from signature ═══");

using (FileStream fs = File.OpenRead(Sample7z))
using (ArchiveFile archive = new(fs))
{
    Console.WriteLine($"  Auto-detected format: {archive.Format} (from stream signature, no file extension used)");
}

// ──────────────────────────────────────────────
Console.WriteLine("\n✔ All examples completed.");
