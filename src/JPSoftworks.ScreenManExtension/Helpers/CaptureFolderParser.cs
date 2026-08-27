namespace JPSoftworks.ScreenManExtension.Helpers;

internal static class CaptureFolderParser
{
    internal static IReadOnlyList<string> GetDefaultFolders()
    {
        return Normalize(
        (string[])
        [
            Path.Combine(GetKnownFolder(Environment.SpecialFolder.MyPictures), "Screenshots"),
            Path.Combine(GetKnownFolder(Environment.SpecialFolder.MyVideos), "Screen Recordings"),
        ]);
    }

    internal static string GetDefaultSettingValue()
    {
        return string.Join(Environment.NewLine, GetDefaultFolders());
    }

    internal static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return Normalize(value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> Normalize(IEnumerable<string> candidates)
    {
        var folders = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            var trimmed = candidate.Trim().Trim('"');
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(trimmed);
                if (expanded.StartsWith("~\\", StringComparison.Ordinal) ||
                    expanded.StartsWith("~/", StringComparison.Ordinal))
                {
                    expanded = Path.Combine(GetKnownFolder(Environment.SpecialFolder.UserProfile), expanded[2..]);
                }

                if (!Path.IsPathRooted(expanded))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(expanded);
                var root = Path.GetPathRoot(fullPath);
                if (!StringComparer.OrdinalIgnoreCase.Equals(fullPath, root))
                {
                    fullPath = Path.TrimEndingDirectorySeparator(fullPath);
                }

                if (seen.Add(fullPath))
                {
                    folders.Add(fullPath);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                ScreenManLog.Warning($"Ignored invalid capture folder '{trimmed}'.");
            }
        }

        return folders;
    }

    private static string GetKnownFolder(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
