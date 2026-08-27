namespace JPSoftworks.ScreenManExtension.Model;

internal sealed record CaptureMetadata(string? Label, IReadOnlyList<string> Tags, bool IsFavorite = false)
{
    internal static CaptureMetadata Empty { get; } = new(null, Array.Empty<string>(), false);
}
