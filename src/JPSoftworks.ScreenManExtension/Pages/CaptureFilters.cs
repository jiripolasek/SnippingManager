namespace JPSoftworks.ScreenManExtension.Pages;

internal sealed partial class CaptureFilters : Filters
{
    internal const string AllId = "all";
    internal const string ScreenshotsId = "screenshots";
    internal const string RecordingsId = "recordings";
    internal const string TodayId = "today";
    internal const string FavoritesId = "favorites";
    internal const string TaggedId = "tagged";
    internal const string UnorganizedId = "unorganized";

    internal CaptureFilters()
    {
        this.CurrentFilterId = AllId;
    }

    public override IFilterItem[] GetFilters()
    {
        return
        [
            new Filter { Id = AllId, Name = "All captures", Icon = Icons.All },
            new Separator(),
            new Filter { Id = ScreenshotsId, Name = "Screenshots", Icon = Icons.Picture },
            new Filter { Id = RecordingsId, Name = "Screen recordings", Icon = Icons.Video },
            new Separator(),
            new Filter { Id = TodayId, Name = "Today", Icon = Icons.Calendar },
            new Filter { Id = FavoritesId, Name = "Favorites", Icon = Icons.FavoriteFilled },
            new Filter { Id = TaggedId, Name = "Tagged or labeled", Icon = Icons.Tag },
            new Filter { Id = UnorganizedId, Name = "Unorganized", Icon = Icons.Edit },
        ];
    }
}
