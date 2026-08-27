namespace JPSoftworks.ScreenManExtension.Sources;

internal static class CaptureFileTypes
{
    private static readonly HashSet<string> ImageExtensions = new(
    (string[])
    [
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif", ".avif",
    ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> VideoExtensions = new(
    (string[])
    [
        ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv",
    ],
        StringComparer.OrdinalIgnoreCase);

    internal static bool TryGetKind(string path, out CaptureMediaKind kind)
    {
        var extension = Path.GetExtension(path);
        if (ImageExtensions.Contains(extension))
        {
            kind = CaptureMediaKind.Image;
            return true;
        }

        if (VideoExtensions.Contains(extension))
        {
            kind = CaptureMediaKind.Video;
            return true;
        }

        kind = default;
        return false;
    }
}
