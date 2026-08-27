using System.Text.Json;

namespace JPSoftworks.ScreenManExtension.Model;

internal sealed class CaptureMetadataStore
{
    private readonly Lock _syncRoot = new();
    private readonly string _filePath;
    private readonly Dictionary<string, CaptureMetadataEntry> _items;
    private readonly Dictionary<string, CaptureMetadataEntry> _identifiedItems;
    private Dictionary<string, string> _identitiesByPath = new(StringComparer.OrdinalIgnoreCase);

    internal CaptureMetadataStore()
        : this(GetDefaultFilePath())
    {
    }

    internal CaptureMetadataStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this._filePath = filePath;
        var document = this.Load();
        this._items = new(document.Items ?? [], StringComparer.OrdinalIgnoreCase);
        this._identifiedItems = new(document.IdentifiedItems ?? [], StringComparer.Ordinal);
    }

    internal event EventHandler? Changed;

    internal CaptureMetadata Get(string fullPath, string? fileIdentity = null)
    {
        var key = NormalizePath(fullPath);
        lock (this._syncRoot)
        {
            var (items, metadataKey) = this.GetStorageLocation(key, fileIdentity);
            return items.TryGetValue(metadataKey, out var entry)
                ? new(entry.Label, entry.Tags?.ToArray() ?? [], entry.IsFavorite)
                : CaptureMetadata.Empty;
        }
    }

    internal void Reconcile(IReadOnlyList<CaptureFile> captures)
    {
        var identitiesByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures)
        {
            if (capture.FileIdentity is not null)
            {
                identitiesByPath[NormalizePath(capture.FullPath)] = capture.FileIdentity;
            }
        }

        lock (this._syncRoot)
        {
            this._identitiesByPath = identitiesByPath;
            var migrated = false;
            foreach (var (path, identity) in identitiesByPath)
            {
                migrated |= this.MigrateLegacyEntry(path, identity);
            }

            if (migrated)
            {
                this.Save();
            }
        }

        // The catalog publishes its new snapshot after reconciliation; avoid refreshing the old page snapshot here.
    }

    internal void Update(string fullPath, string? label, IEnumerable<string> tags, string? fileIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var key = NormalizePath(fullPath);
        var identity = fileIdentity ?? CaptureFileIdentity.TryGet(key);
        var normalizedLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        var normalizedTags = tags
            .Select(static tag => tag.Trim())
            .Where(static tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        lock (this._syncRoot)
        {
            if (fileIdentity is null)
            {
                this.RememberIdentity(key, identity);
            }

            var (items, metadataKey) = this.GetStorageLocation(key, identity);
            var isFavorite = items.TryGetValue(metadataKey, out var existing) && existing.IsFavorite;
            SetEntry(items, metadataKey, normalizedLabel, normalizedTags, isFavorite);
            this.Save();
        }

        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    internal bool ToggleFavorite(string fullPath, string? fileIdentity = null)
    {
        var key = NormalizePath(fullPath);
        var identity = fileIdentity ?? CaptureFileIdentity.TryGet(key);
        bool isFavorite;
        lock (this._syncRoot)
        {
            if (fileIdentity is null)
            {
                this.RememberIdentity(key, identity);
            }

            var (items, metadataKey) = this.GetStorageLocation(key, identity);
            items.TryGetValue(metadataKey, out var existing);
            isFavorite = !(existing?.IsFavorite ?? false);
            SetEntry(
                items,
                metadataKey,
                existing?.Label,
                existing?.Tags ?? [],
                isFavorite);
            this.Save();
        }

        this.Changed?.Invoke(this, EventArgs.Empty);
        return isFavorite;
    }

    private (Dictionary<string, CaptureMetadataEntry> Items, string Key) GetStorageLocation(string path, string? identity = null)
    {
        if (identity is null)
        {
            this._identitiesByPath.TryGetValue(path, out identity);
        }

        return identity is null ? (this._items, path) : (this._identifiedItems, identity);
    }

    private void RememberIdentity(string path, string? identity)
    {
        if (identity is not null)
        {
            this._identitiesByPath[path] = identity;
            this.MigrateLegacyEntry(path, identity);
        }
    }

    private bool MigrateLegacyEntry(string path, string identity)
    {
        if (!this._items.Remove(path, out var legacy))
        {
            return false;
        }

        if (this._identifiedItems.TryGetValue(identity, out var existing))
        {
            // Multiple legacy paths can refer to the same file (for example, hard links).
            existing.Label ??= legacy.Label;
            existing.Tags = (existing.Tags ?? []).Concat(legacy.Tags ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            existing.IsFavorite |= legacy.IsFavorite;
        }
        else
        {
            this._identifiedItems[identity] = legacy;
        }

        return true;
    }

    private CaptureMetadataDocument Load()
    {
        if (!File.Exists(this._filePath))
        {
            return new();
        }

        try
        {
            var json = File.ReadAllText(this._filePath);
            return JsonSerializer.Deserialize(json, ScreenManJsonContext.Default.CaptureMetadataDocument) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            ScreenManLog.Error("Unable to load Snipping Manager metadata.", ex);
            return new();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(this._filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new CaptureMetadataDocument
            {
                Items = new(this._items, StringComparer.OrdinalIgnoreCase),
                IdentifiedItems = new(this._identifiedItems, StringComparer.Ordinal),
            };
            var json = JsonSerializer.Serialize(document, ScreenManJsonContext.Default.CaptureMetadataDocument);
            var temporaryPath = this._filePath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, this._filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ScreenManLog.Error("Unable to save Snipping Manager metadata.", ex);
        }
    }

    private static void SetEntry(
        Dictionary<string, CaptureMetadataEntry> items,
        string key,
        string? label,
        string[] tags,
        bool isFavorite)
    {
        if (label is null && tags.Length == 0 && !isFavorite)
        {
            items.Remove(key);
            return;
        }

        items[key] = new()
        {
            Label = label,
            Tags = tags,
            IsFavorite = isFavorite,
        };
    }

    private static string NormalizePath(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        return Path.GetFullPath(fullPath);
    }

    private static string GetDefaultFilePath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "screenman.metadata.json");
    }
}
