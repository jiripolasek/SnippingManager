namespace JPSoftworks.ScreenManExtension.Pages;

internal sealed partial class CaptureManagerPage : DynamicListPage, IDisposable
{
    private const int PageSize = 40;

    private readonly CaptureCatalog _catalog;
    private readonly CaptureMetadataStore _metadataStore;
    private readonly SettingsManager _settingsManager;
    private readonly CaptureFilters _filters = new();
    private readonly CaptureListItemCache _itemCache = new();
    private readonly Lock _syncRoot = new();

    private List<CaptureFile> _filteredCaptures = [];
    private List<IListItem> _loadedItems = [];
    private IReadOnlyDictionary<string, int> _sectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    private CancellationTokenSource _itemCancellationTokenSource = new();
    private DateTimeOffset _sectionReferenceTime = DateTimeOffset.UtcNow;
    private int _cursor;
    private bool _isRefreshing;
    private bool _isDisposed;

    internal CaptureManagerPage(
        CaptureCatalog catalog,
        CaptureMetadataStore metadataStore,
        SettingsManager settingsManager)
    {
        this._catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this._metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        this._settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        this._itemCache.SetOpenInPreview(this._settingsManager.OpenInPreview);

        this.Id = "com.jpsoftworks.cmdpal.screenman";
        this.Name = "Screenshots and recordings";
        this.Title = "Screenshots and recordings";
        this.Icon = Icons.Main;
        this.PlaceholderText = "Search filenames, labels, tags, dates, or folders";
        this.ShowDetails = this._settingsManager.ShowDetailsAutomatically;
        this.GridProperties = new GalleryGridLayout { ShowTitle = true, ShowSubtitle = true };
        this.Filters = this._filters;
        this.EmptyContent = CreateLoadingContent();
        this.IsLoading = !this._catalog.IsInitialized;

        this._catalog.Changed += this.OnDataChanged;
        this._metadataStore.Changed += this.OnDataChanged;
        this._settingsManager.Settings.SettingsChanged += this.OnSettingsChanged;
        this._filters.PropChanged += (_, _) => this.Refresh(this.SearchText);

        if (this._catalog.IsInitialized)
        {
            this.Refresh(string.Empty);
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (!StringComparer.Ordinal.Equals(oldSearch, newSearch))
        {
            this.Refresh(newSearch);
        }
    }

    public override IListItem[] GetItems()
    {
        lock (this._syncRoot)
        {
            return [.. this._loadedItems];
        }
    }

    public override void LoadMore()
    {
        List<CaptureFile> slice;
        CancellationToken cancellationToken;
        CancellationTokenSource source;
        DateTimeOffset sectionReferenceTime;
        DateTimeOffset? previousTimestamp;
        IReadOnlyDictionary<string, int> sectionCounts;
        lock (this._syncRoot)
        {
            if (this._isDisposed || this._isRefreshing)
            {
                return;
            }

            source = this._itemCancellationTokenSource;
            cancellationToken = source.Token;
            sectionReferenceTime = this._sectionReferenceTime;
            sectionCounts = this._sectionCounts;
            previousTimestamp = this._cursor == 0
                ? null
                : this._filteredCaptures[this._cursor - 1].ModifiedAtUtc;
            slice = this._filteredCaptures.Skip(this._cursor).Take(PageSize).ToList();
            this._cursor += slice.Count;
        }

        var items = this.CreateItems(slice, sectionReferenceTime, sectionCounts, previousTimestamp, cancellationToken);
        if (items is null)
        {
            return;
        }

        int itemCount;
        lock (this._syncRoot)
        {
            if (this._isDisposed || !ReferenceEquals(source, this._itemCancellationTokenSource))
            {
                return;
            }

            this._loadedItems.AddRange(items);
            itemCount = this._loadedItems.Count;
            this.HasMoreItems = this._cursor < this._filteredCaptures.Count;
        }

        this.RaiseItemsChanged(itemCount);
    }

    private List<IListItem>? CreateItems(
        List<CaptureFile> captures,
        DateTimeOffset sectionReferenceTime,
        IReadOnlyDictionary<string, int> sectionCounts,
        DateTimeOffset? previousTimestamp,
        CancellationToken cancellationToken)
    {
        var items = new List<IListItem>(captures.Count + 1);
        foreach (var capture in captures)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (CaptureDateSection.StartsNewSection(
                capture.ModifiedAtUtc,
                previousTimestamp,
                sectionReferenceTime))
            {
                var section = CaptureDateSection.Get(capture.ModifiedAtUtc, sectionReferenceTime);
                items.Add(new Separator(CaptureDateSection.FormatHeader(section, sectionCounts[section])));
            }

            var item = this._itemCache.GetOrCreate(
                capture,
                this._metadataStore.Get(capture.FullPath),
                this._metadataStore,
                onDeleted: this._catalog.Remove,
                thumbnailCancellationToken: CancellationToken.None); // Cached thumbnails outlive this refresh.
            if (item is null)
            {
                return null;
            }

            items.Add(item);
            previousTimestamp = capture.ModifiedAtUtc;
        }

        return items;
    }

    public void Dispose()
    {
        CancellationTokenSource cancellationTokenSource;
        lock (this._syncRoot)
        {
            if (this._isDisposed)
            {
                return;
            }

            this._isDisposed = true;
            cancellationTokenSource = this._itemCancellationTokenSource;
        }

        this._catalog.Changed -= this.OnDataChanged;
        this._metadataStore.Changed -= this.OnDataChanged;
        this._settingsManager.Settings.SettingsChanged -= this.OnSettingsChanged;
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        this._itemCache.Dispose();
    }

    private void OnSettingsChanged(object sender, Settings args)
    {
        this.ShowDetails = this._settingsManager.ShowDetailsAutomatically;
        this._itemCache.SetOpenInPreview(this._settingsManager.OpenInPreview);
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        this.Refresh(this.SearchText, preserveLoadedCaptures: true);
    }

    private void Refresh(string? query, bool preserveLoadedCaptures = false)
    {
        if (!this._catalog.IsInitialized)
        {
            this.IsLoading = true;
            this.EmptyContent = CreateLoadingContent();
            return;
        }

        var allCaptures = this._catalog.GetSnapshot();
        this._itemCache.Prune(allCaptures);
        var sectionReferenceTime = DateTimeOffset.UtcNow;
        var filterId = this._filters.CurrentFilterId;
        var captures = allCaptures
            .Where(capture =>
            {
                var metadata = this._metadataStore.Get(capture.FullPath);
                return CaptureSearch.MatchesFilter(capture, metadata, filterId) &&
                       CaptureSearch.Matches(capture, metadata, query);
            })
            .ToList();
        var sectionCounts = CaptureDateSection.CountSections(captures, sectionReferenceTime);

        var nextSource = new CancellationTokenSource();
        var cancellationToken = nextSource.Token;
        CancellationTokenSource previousSource;
        int captureCount;
        lock (this._syncRoot)
        {
            if (this._isDisposed)
            {
                nextSource.Dispose();
                return;
            }

            captureCount = PageSize;
            if (preserveLoadedCaptures)
            {
                var loadedPaths = this._filteredCaptures.Take(this._cursor)
                    .Select(static capture => capture.FullPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                // New captures can push the previous last loaded capture past the old page boundary.
                var lastRetainedIndex = captures.FindLastIndex(capture => loadedPaths.Contains(capture.FullPath));
                captureCount = Math.Max(this._cursor, Math.Max(PageSize, lastRetainedIndex + 1));
            }

            captureCount = Math.Min(captureCount, captures.Count);
            previousSource = this._itemCancellationTokenSource;
            this._itemCancellationTokenSource = nextSource;
            this._isRefreshing = true;
        }

        previousSource.Cancel();
        previousSource.Dispose();
        var items = this.CreateItems(
            captures.GetRange(0, captureCount),
            sectionReferenceTime,
            sectionCounts,
            previousTimestamp: null,
            cancellationToken);
        if (items is null)
        {
            return;
        }

        lock (this._syncRoot)
        {
            if (this._isDisposed || !ReferenceEquals(nextSource, this._itemCancellationTokenSource))
            {
                return;
            }

            // Publish the complete replacement at once, without exposing an empty or first-page-only list.
            this._filteredCaptures = captures;
            this._loadedItems = items;
            this._sectionCounts = sectionCounts;
            this._sectionReferenceTime = sectionReferenceTime;
            this._cursor = captureCount;
            this._isRefreshing = false;
            this.HasMoreItems = captureCount < captures.Count;
        }

        this.IsLoading = false;
        this.EmptyContent = captures.Count == 0
            ? CreateEmptyContent(string.IsNullOrWhiteSpace(query))
            : null;

        this.RaiseItemsChanged(items.Count);
    }

    private static CommandItem CreateLoadingContent()
    {
        return new()
        {
            Title = "Loading captures…",
            Subtitle = "Checking your screenshot and screen recording folders",
            Icon = Icons.Main,
        };
    }

    private static CommandItem CreateEmptyContent(bool noQuery)
    {
        return new()
        {
            Title = noQuery ? "No captures found" : "No matching captures",
            Subtitle = noQuery
                ? "Add a folder in Snipping Manager settings or create a new snip."
                : "Try a different search or filter.",
            Icon = Icons.Main,
        };
    }
}
