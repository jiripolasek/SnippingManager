using System.Globalization;

namespace JPSoftworks.ScreenManExtension.Pages;

internal static class CaptureSearch
{
    internal static bool Matches(CaptureFile capture, CaptureMetadata metadata, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var kind = capture.Kind == CaptureMediaKind.Image
            ? "image screenshot"
            : "video recording screen recording";
        var haystack = string.Join(
            '\n',
            capture.FileName,
            capture.FullPath,
            metadata.Label ?? string.Empty,
            string.Join(' ', metadata.Tags),
            metadata.IsFavorite ? "favorite favorites starred" : string.Empty,
            kind,
            CaptureDateSection.Get(capture.ModifiedAtUtc),
            capture.ModifiedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));

        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool MatchesFilter(
        CaptureFile capture,
        CaptureMetadata metadata,
        string filterId,
        DateTime? today = null)
    {
        return filterId switch
        {
            CaptureFilters.ScreenshotsId => capture.Kind == CaptureMediaKind.Image,
            CaptureFilters.RecordingsId => capture.Kind == CaptureMediaKind.Video,
            CaptureFilters.TodayId => capture.ModifiedAtUtc.ToLocalTime().Date == (today ?? DateTime.Today).Date,
            CaptureFilters.FavoritesId => metadata.IsFavorite,
            CaptureFilters.TaggedId => !string.IsNullOrWhiteSpace(metadata.Label) || metadata.Tags.Count > 0,
            CaptureFilters.UnorganizedId => string.IsNullOrWhiteSpace(metadata.Label) && metadata.Tags.Count == 0,
            _ => true,
        };
    }
}
