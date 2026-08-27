namespace JPSoftworks.ScreenManExtension.Sources;

internal sealed partial class CaptureCatalog : IDisposable
{
    private static readonly TimeSpan RefreshDebounceInterval = TimeSpan.FromMilliseconds(400);

    private readonly ICaptureSource _source;
    private readonly CaptureMetadataStore? _metadataStore;
    private readonly Lock _syncRoot = new();
    private readonly Lock _lifecycleLock = new();
    private readonly System.Timers.Timer _reloadTimer = new(RefreshDebounceInterval) { AutoReset = false };
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private List<CaptureFile> _captures = [];
    private long _removalVersion;
    private bool _isInitialized;
    private bool _isRefreshInProgress;
    private bool _refreshPending;
    private bool _isDisposed;

    internal CaptureCatalog(ICaptureSource source, CaptureMetadataStore? metadataStore = null)
    {
        this._source = source ?? throw new ArgumentNullException(nameof(source));
        this._metadataStore = metadataStore;
        this._source.Changed += this.OnSourceChanged;
        this._reloadTimer.Elapsed += this.OnReloadTimerElapsed;
        _ = Task.Run(this.InitialLoadAsync, this._cancellationTokenSource.Token);
    }

    internal event EventHandler? Changed;

    internal bool IsInitialized
    {
        get
        {
            lock (this._syncRoot)
            {
                return this._isInitialized;
            }
        }
    }

    internal IReadOnlyList<CaptureFile> GetSnapshot()
    {
        lock (this._syncRoot)
        {
            return this._captures.ToArray();
        }
    }

    internal bool TryGetLatest(out CaptureFile? capture)
    {
        lock (this._syncRoot)
        {
            capture = this._captures.Count == 0 ? null : this._captures[0];
            return capture is not null;
        }
    }

    internal void Remove(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        bool changed;
        lock (this._lifecycleLock)
        {
            if (this._isDisposed)
            {
                return;
            }

            lock (this._syncRoot)
            {
                this._removalVersion++;
                changed = this._captures.RemoveAll(capture =>
                    StringComparer.OrdinalIgnoreCase.Equals(capture.FullPath, path)) > 0;
            }
        }

        if (changed)
        {
            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        lock (this._lifecycleLock)
        {
            if (this._isDisposed)
            {
                return;
            }

            this._isDisposed = true;
            this._cancellationTokenSource.Cancel();
            this._reloadTimer.Stop();
        }

        this._source.Changed -= this.OnSourceChanged;
        this._reloadTimer.Elapsed -= this.OnReloadTimerElapsed;
        this._reloadTimer.Dispose();
        this._source.Dispose();
        this._cancellationTokenSource.Dispose();
    }

    private async Task InitialLoadAsync()
    {
        try
        {
            await Task.Run(this.Refresh, this._cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (this._cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ScreenManLog.Error("Unable to load captures.", ex);
            lock (this._syncRoot)
            {
                this._isInitialized = true;
            }

            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        lock (this._lifecycleLock)
        {
            if (this._isDisposed)
            {
                return;
            }

            if (this._isRefreshInProgress)
            {
                this._refreshPending = true;
                return;
            }

            this._reloadTimer.Stop();
            this._reloadTimer.Start();
        }
    }

    private void OnReloadTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        this.Refresh();
    }

    private void Refresh()
    {
        long removalVersion;
        lock (this._lifecycleLock)
        {
            if (this._isDisposed || this._cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (this._isRefreshInProgress)
            {
                this._refreshPending = true;
                return;
            }

            this._isRefreshInProgress = true;
            lock (this._syncRoot)
            {
                removalVersion = this._removalVersion;
            }
        }

        try
        {
            var next = this._source.GetCaptures(this._cancellationTokenSource.Token).ToList();
            this._cancellationTokenSource.Token.ThrowIfCancellationRequested();
            this._metadataStore?.Reconcile(next);
            bool changed;
            lock (this._lifecycleLock)
            {
                if (this._isDisposed)
                {
                    return;
                }

                lock (this._syncRoot)
                {
                    if (removalVersion != this._removalVersion)
                    {
                        // An older scan may still contain a capture we just deleted.
                        this._refreshPending = true;
                        return;
                    }

                    changed = !this._captures.SequenceEqual(next);
                    if (changed)
                    {
                        this._captures = next;
                    }

                    changed |= !this._isInitialized;
                    this._isInitialized = true;
                }
            }

            if (changed && !this._cancellationTokenSource.IsCancellationRequested)
            {
                this.Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (this._cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ScreenManLog.Error("Unable to refresh captures.", ex);
        }
        finally
        {
            lock (this._lifecycleLock)
            {
                this._isRefreshInProgress = false;
                if (this._refreshPending && !this._isDisposed)
                {
                    this._refreshPending = false;
                    this._reloadTimer.Stop();
                    this._reloadTimer.Start();
                }
            }
        }
    }
}
