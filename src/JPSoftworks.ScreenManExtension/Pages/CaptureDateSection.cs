using System.Globalization;

namespace JPSoftworks.ScreenManExtension.Pages;

internal static class CaptureDateSection
{
    internal static string Get(
        DateTimeOffset timestampUtc,
        DateTimeOffset? now = null,
        DayOfWeek? firstDayOfWeek = null)
    {
        var localTimestamp = timestampUtc.ToLocalTime();
        var localNow = (now ?? DateTimeOffset.UtcNow).ToLocalTime();
        if (localTimestamp <= localNow && localTimestamp >= localNow.AddHours(-1))
        {
            return "Last hour";
        }

        var localDate = localTimestamp.Date;
        var currentDate = localNow.Date;
        if (localDate == currentDate)
        {
            return "Earlier today";
        }

        if (localDate == currentDate.AddDays(-1))
        {
            return "Yesterday";
        }

        var weekStartsOn = firstDayOfWeek ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var daysSinceStartOfWeek = ((int)currentDate.DayOfWeek - (int)weekStartsOn + 7) % 7;
        var startOfThisWeek = currentDate.AddDays(-daysSinceStartOfWeek);
        if (localDate >= startOfThisWeek)
        {
            return "This week";
        }

        if (localDate >= startOfThisWeek.AddDays(-7))
        {
            return "Last week";
        }

        return "Older";
    }

    internal static bool StartsNewSection(
        DateTimeOffset timestampUtc,
        DateTimeOffset? previousTimestampUtc,
        DateTimeOffset now,
        DayOfWeek? firstDayOfWeek = null)
    {
        return previousTimestampUtc is null ||
               !StringComparer.Ordinal.Equals(
                   Get(timestampUtc, now, firstDayOfWeek),
                   Get(previousTimestampUtc.Value, now, firstDayOfWeek));
    }

    internal static IReadOnlyDictionary<string, int> CountSections(
        IEnumerable<CaptureFile> captures,
        DateTimeOffset now,
        DayOfWeek? firstDayOfWeek = null)
    {
        ArgumentNullException.ThrowIfNull(captures);
        return captures
            .GroupBy(capture => Get(capture.ModifiedAtUtc, now, firstDayOfWeek), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
    }

    internal static string FormatHeader(string section, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return $"{section} · {count}";
    }
}
