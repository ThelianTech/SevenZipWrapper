namespace SevenZipWrapper;

/// <summary>Windows archive-name validation; never repairs an unsafe name.</summary>
internal static class RootedPath
{
    internal static string Resolve(string root, string? entryName)
    {
        if (string.IsNullOrEmpty(entryName)) throw Unsafe(entryName);
        string name = entryName.Replace('/', '\\');
        if (Path.IsPathRooted(name) || name.Contains(':')) throw Unsafe(entryName);
        List<string> parts = [];
        foreach (string part in name.Split('\\'))
        {
            if (part.Length == 0 || part == ".") continue;
            if (part == "..")
            {
                if (parts.Count == 0) throw Unsafe(entryName);
                parts.RemoveAt(parts.Count - 1);
                continue;
            }
            if (part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw Unsafe(entryName);
            string stem = part.Split('.')[0].TrimEnd(' ');
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase)
                || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3])))
                throw Unsafe(entryName);
            parts.Add(part);
        }
        if (parts.Count == 0) throw Unsafe(entryName);
        string relative = string.Join('\\', parts);
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string boundary = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw Unsafe(entryName);
        return relative;
    }

    internal static void ValidateTargets(IEnumerable<(string Path, bool IsDirectory)> targets)
    {
        Dictionary<string, bool> explicitTargets = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string path, bool isDirectory) in targets)
        {
            if (!explicitTargets.TryAdd(path, isDirectory) || (!isDirectory && directories.Contains(path)))
                throw Conflict(path);
            string? parent = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(parent))
            {
                if (explicitTargets.TryGetValue(parent, out bool folder) && !folder) throw Conflict(parent);
                directories.Add(parent);
                parent = Path.GetDirectoryName(parent);
            }
            if (isDirectory) directories.Add(path);
        }
    }

    internal static ArchiveExtractionException Unsafe(string? name) =>
        new(new(FailureKind.UnsafePath, "The archive destination is unsafe for rooted extraction.", name));

    internal static ArchiveExtractionException Conflict(string? name) =>
        new(new(FailureKind.DestinationConflict, "Archive destinations conflict with one another or the filesystem.", name));
}
