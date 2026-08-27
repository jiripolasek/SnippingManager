using JPSoftworks.ScreenManExtension.Model;
using JPSoftworks.ScreenManExtension.Pages;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CaptureSearchTests
{
    private static readonly DateTime Today = new(2026, 8, 19);

    [TestMethod]
    public void MatchesSearchesLabelTagsPathAndMediaKind()
    {
        var capture = CreateCapture(CaptureMediaKind.Video, Today);
        var metadata = new CaptureMetadata("Settings regression", ["cmdpal", "bug"]);

        Assert.IsTrue(CaptureSearch.Matches(capture, metadata, "settings cmdpal"));
        Assert.IsTrue(CaptureSearch.Matches(capture, metadata, "screen recording"));
        Assert.IsTrue(CaptureSearch.Matches(capture, metadata, "captures"));
        Assert.IsFalse(CaptureSearch.Matches(capture, metadata, "meeting"));
    }

    [TestMethod]
    public void FiltersDistinguishKindTodayAndOrganizedItems()
    {
        var capture = CreateCapture(CaptureMediaKind.Image, Today);
        var metadata = new CaptureMetadata(null, ["work"]);
        var favorite = new CaptureMetadata(null, [], true);

        Assert.IsTrue(CaptureSearch.MatchesFilter(capture, metadata, CaptureFilters.ScreenshotsId, Today));
        Assert.IsFalse(CaptureSearch.MatchesFilter(capture, metadata, CaptureFilters.RecordingsId, Today));
        Assert.IsTrue(CaptureSearch.MatchesFilter(capture, metadata, CaptureFilters.TodayId, Today));
        Assert.IsTrue(CaptureSearch.MatchesFilter(capture, metadata, CaptureFilters.TaggedId, Today));
        Assert.IsTrue(CaptureSearch.MatchesFilter(capture, favorite, CaptureFilters.FavoritesId, Today));
        Assert.IsTrue(CaptureSearch.MatchesFilter(capture, favorite, CaptureFilters.UnorganizedId, Today));
        Assert.IsFalse(CaptureSearch.MatchesFilter(capture, metadata, CaptureFilters.UnorganizedId, Today));
        Assert.IsTrue(CaptureSearch.Matches(capture, favorite, "favorite"));
    }

    [TestMethod]
    public void DateSectionsUseRelativeHourDayAndWeekBuckets()
    {
        var now = ToUtc(Today.AddHours(15));

        Assert.AreEqual("Last hour", GetSection(Today.AddHours(14).AddMinutes(30), now));
        Assert.AreEqual("Earlier today", GetSection(Today.AddHours(12), now));
        Assert.AreEqual("Yesterday", GetSection(Today.AddDays(-1).AddHours(12), now));
        Assert.AreEqual("This week", GetSection(new DateTime(2026, 8, 17, 12, 0, 0), now));
        Assert.AreEqual("Last week", GetSection(new DateTime(2026, 8, 12, 12, 0, 0), now));
        Assert.AreEqual("Older", GetSection(new DateTime(2026, 8, 9, 12, 0, 0), now));
    }

    [TestMethod]
    public void DateSectionsDoNotRepeatAcrossPageBoundaries()
    {
        var now = ToUtc(Today.AddHours(15));
        var previousPageLastCapture = ToUtc(new DateTime(2026, 8, 12, 13, 0, 0));
        var nextPageFirstCapture = ToUtc(new DateTime(2026, 8, 12, 12, 0, 0));

        Assert.IsFalse(CaptureDateSection.StartsNewSection(
            nextPageFirstCapture,
            previousPageLastCapture,
            now,
            DayOfWeek.Monday));
        Assert.IsTrue(CaptureDateSection.StartsNewSection(
            ToUtc(new DateTime(2026, 8, 9, 12, 0, 0)),
            nextPageFirstCapture,
            now,
            DayOfWeek.Monday));
    }

    [TestMethod]
    public void DateSectionsCountAllFilteredCapturesAndFormatHeaders()
    {
        var now = ToUtc(Today.AddHours(15));
        var captures = new[]
        {
            CreateCapture(CaptureMediaKind.Image, Today.AddHours(14).AddMinutes(30)),
            CreateCapture(CaptureMediaKind.Image, Today.AddHours(14).AddMinutes(15)),
            CreateCapture(CaptureMediaKind.Image, Today.AddDays(-1).AddHours(12)),
        };

        var counts = CaptureDateSection.CountSections(captures, now, DayOfWeek.Monday);

        Assert.AreEqual(2, counts["Last hour"]);
        Assert.AreEqual(1, counts["Yesterday"]);
        Assert.AreEqual("Last hour · 2", CaptureDateSection.FormatHeader("Last hour", counts["Last hour"]));
    }

    private static CaptureFile CreateCapture(CaptureMediaKind kind, DateTime localDate)
    {
        return new(
            Path.Combine(Path.GetTempPath(), "captures", kind == CaptureMediaKind.Image ? "capture.png" : "capture.mp4"),
            ToUtc(localDate),
            1024,
            kind);
    }

    private static DateTimeOffset ToUtc(DateTime localDate)
    {
        return new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate)).ToUniversalTime();
    }

    private static string GetSection(DateTime localDate, DateTimeOffset now)
    {
        return CaptureDateSection.Get(ToUtc(localDate), now, DayOfWeek.Monday);
    }
}
