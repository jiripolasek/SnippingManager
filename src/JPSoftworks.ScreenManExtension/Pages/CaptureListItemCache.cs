namespace JPSoftworks.ScreenManExtension.Pages;

internal sealed partial class CaptureListItemCache : IDisposable
{
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, CacheEntry> _items = new(StringComparer.OrdinalIgnoreCase);
    private bool _openInPreview;
    private bool _isDisposed;

    internal int Count
    {
        get
        {
            lock (this._syncRoot)
            {
                return this._items.Count;
            }
        }
    }

    internal void SetOpenInPreview(bool openInPreview)
    {
        CaptureListItem[] items;
        lock (this._syncRoot)
        {
            if (this._isDisposed || this._openInPreview == openInPreview)
            {
                return;
            }

            this._openInPreview = openInPreview;
            items = this._items.Values.Select(static entry => entry.Item).ToArray();
        }

        foreach (var item in items)
        {
            item.SetOpenInPreview(openInPreview);
        }
    }

    internal CaptureListItem? GetOrCreate(
        CaptureFile capture,
        CaptureMetadata metadata,
        CaptureMetadataStore metadataStore,
        Action<string>? onDeleted = null,
        CancellationToken thumbnailCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(metadataStore);

        CaptureListItem? previousItem = null;
        CaptureListItem nextItem;
        lock (this._syncRoot)
        {
            if (this._isDisposed)
            {
                return null;
            }

            if (this._items.TryGetValue(capture.FullPath, out var existing) && existing.MatchesCapture(capture))
            {
                if (!existing.MatchesMetadata(metadata))
                {
                    // Command Palette retains selection by item identity, including across metadata edits.
                    existing.Item.UpdateMetadata(capture, metadata, metadataStore);
                    this._items[capture.FullPath] = existing with { Metadata = metadata };
                }

                return existing.Item;
            }

            nextItem = new(capture, metadata, metadataStore, onDeleted, this._openInPreview, thumbnailCancellationToken);
            if (existing is not null)
            {
                previousItem = existing.Item;
            }

            this._items[capture.FullPath] = new(capture, metadata, nextItem);
        }

        previousItem?.Dispose();
        return nextItem;
    }

    internal void Prune(IEnumerable<CaptureFile> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        var retainedPaths = captures
            .Select(static capture => capture.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<CaptureListItem> removedItems = [];
        lock (this._syncRoot)
        {
            if (this._isDisposed)
            {
                return;
            }

            foreach (var path in this._items.Keys.Where(path => !retainedPaths.Contains(path)).ToArray())
            {
                removedItems.Add(this._items[path].Item);
                this._items.Remove(path);
            }
        }

        foreach (var item in removedItems)
        {
            item.Dispose();
        }
    }

    public void Dispose()
    {
        List<CaptureListItem> items;
        lock (this._syncRoot)
        {
            if (this._isDisposed)
            {
                return;
            }

            this._isDisposed = true;
            items = this._items.Values.Select(static entry => entry.Item).ToList();
            this._items.Clear();
        }

        foreach (var item in items)
        {
            item.Dispose();
        }
    }

    private sealed record CacheEntry(
        CaptureFile Capture,
        CaptureMetadata Metadata,
        CaptureListItem Item)
    {
        internal bool MatchesCapture(CaptureFile capture)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(this.Capture.FullPath, capture.FullPath)
                && this.Capture.ModifiedAtUtc == capture.ModifiedAtUtc
                && this.Capture.SizeInBytes == capture.SizeInBytes
                && StringComparer.Ordinal.Equals(this.Capture.FileIdentity, capture.FileIdentity)
                && this.Capture.Kind == capture.Kind;
        }

        internal bool MatchesMetadata(CaptureMetadata metadata)
        {
            return StringComparer.Ordinal.Equals(this.Metadata.Label, metadata.Label)
                && this.Metadata.IsFavorite == metadata.IsFavorite
                && this.Metadata.Tags.SequenceEqual(metadata.Tags, StringComparer.Ordinal);
        }
    }
}
