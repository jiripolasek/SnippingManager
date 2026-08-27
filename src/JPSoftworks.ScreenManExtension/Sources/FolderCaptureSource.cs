namespace JPSoftworks.ScreenManExtension.Sources;

internal sealed partial class FolderCaptureSource : ICaptureSource
{
    private const int MaximumCaptureCount = 2000;

    private readonly ICaptureSettings _settings;
    private readonly Lock _lifecycleLock = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private bool _isDisposed;

    internal FolderCaptureSource(ICaptureSettings settings)
    {
        this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this._settings.SourcesChanged += this.OnSettingsChanged;
        this.RebuildWatchers();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<CaptureFile> GetCaptures(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this._isDisposed, this);

        var folders = this._settings.FolderPaths;
        var includeSubfolders = this._settings.IncludeSubfolders;
        var captures = new List<CaptureFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var filePath in EnumerateFilesSafely(folder, includeSubfolders, cancellationToken))
            {
                if (!seen.Add(filePath) || !CaptureFileTypes.TryGetKind(filePath, out var kind))
                {
                    continue;
                }

                try
                {
                    var file = new FileInfo(filePath);
                    if (!file.Exists)
                    {
                        continue;
                    }

                    captures.Add(new(
                        file.FullName,
                        new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                        file.Length,
                        kind));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    ScreenManLog.Warning($"Unable to inspect capture '{filePath}'.");
                }
            }
        }

        return captures
            .OrderByDescending(static capture => capture.ModifiedAtUtc)
            .Take(MaximumCaptureCount)
            .ToArray();
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
            this._settings.SourcesChanged -= this.OnSettingsChanged;
            this.DisposeWatchers();
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(
        string root,
        bool includeSubfolders,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out var folder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                ScreenManLog.Warning($"Unable to enumerate capture folder '{folder}'.");
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }

            if (!includeSubfolders)
            {
                continue;
            }

            try
            {
                foreach (var child in Directory.GetDirectories(folder))
                {
                    pending.Push(child);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                ScreenManLog.Warning($"Unable to enumerate subfolders of '{folder}'.");
            }
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        this.RebuildWatchers();
        this.RaiseChanged();
    }

    private void RebuildWatchers()
    {
        lock (this._lifecycleLock)
        {
            if (this._isDisposed)
            {
                return;
            }

            this.DisposeWatchers();
            foreach (var folder in this._settings.FolderPaths.Where(Directory.Exists))
            {
                try
                {
                    var watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = this._settings.IncludeSubfolders,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    };
                    watcher.Created += this.OnFileSystemChanged;
                    watcher.Changed += this.OnFileSystemChanged;
                    watcher.Deleted += this.OnFileSystemChanged;
                    watcher.Renamed += this.OnFileSystemChanged;
                    watcher.Error += this.OnWatcherError;
                    watcher.EnableRaisingEvents = true;
                    this._watchers.Add(watcher);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    ScreenManLog.Warning($"Unable to watch capture folder '{folder}'.");
                }
            }
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (CaptureFileTypes.TryGetKind(e.FullPath, out _) || e.ChangeType == WatcherChangeTypes.Deleted)
        {
            this.RaiseChanged();
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        this.RaiseChanged();
    }

    private void RaiseChanged()
    {
        if (!this._isDisposed)
        {
            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in this._watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= this.OnFileSystemChanged;
            watcher.Changed -= this.OnFileSystemChanged;
            watcher.Deleted -= this.OnFileSystemChanged;
            watcher.Renamed -= this.OnFileSystemChanged;
            watcher.Error -= this.OnWatcherError;
            watcher.Dispose();
        }

        this._watchers.Clear();
    }
}
