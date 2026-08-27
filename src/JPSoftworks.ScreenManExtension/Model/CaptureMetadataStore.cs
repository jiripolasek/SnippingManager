using System.Text.Json;

namespace JPSoftworks.ScreenManExtension.Model;

internal sealed class CaptureMetadataStore
{
    private readonly Lock _syncRoot = new();
    private readonly string _filePath;
    private Dictionary<string, CaptureMetadataEntry> _items;

    internal CaptureMetadataStore()
        : this(GetDefaultFilePath())
    {
    }

    internal CaptureMetadataStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this._filePath = filePath;
        this._items = this.Load();
    }

    internal event EventHandler? Changed;

    internal CaptureMetadata Get(string fullPath)
    {
        var key = NormalizePath(fullPath);
        lock (this._syncRoot)
        {
            return this._items.TryGetValue(key, out var entry)
                ? new(entry.Label, entry.Tags?.ToArray() ?? [], entry.IsFavorite)
                : CaptureMetadata.Empty;
        }
    }

    internal void Update(string fullPath, string? label, IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var key = NormalizePath(fullPath);
        var normalizedLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        var normalizedTags = tags
            .Select(static tag => tag.Trim())
            .Where(static tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        lock (this._syncRoot)
        {
            var isFavorite = this._items.TryGetValue(key, out var existing) && existing.IsFavorite;
            this.SetEntry(key, normalizedLabel, normalizedTags, isFavorite);
            this.Save();
        }

        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    internal bool ToggleFavorite(string fullPath)
    {
        var key = NormalizePath(fullPath);
        bool isFavorite;
        lock (this._syncRoot)
        {
            this._items.TryGetValue(key, out var existing);
            isFavorite = !(existing?.IsFavorite ?? false);
            this.SetEntry(
                key,
                existing?.Label,
                existing?.Tags ?? [],
                isFavorite);
            this.Save();
        }

        this.Changed?.Invoke(this, EventArgs.Empty);
        return isFavorite;
    }

    private Dictionary<string, CaptureMetadataEntry> Load()
    {
        if (!File.Exists(this._filePath))
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(this._filePath);
            var document = JsonSerializer.Deserialize(json, ScreenManJsonContext.Default.CaptureMetadataDocument);
            return document?.Items is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(document.Items, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            ScreenManLog.Error("Unable to load Snipping Manager metadata.", ex);
            return new(StringComparer.OrdinalIgnoreCase);
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

    private void SetEntry(string key, string? label, string[] tags, bool isFavorite)
    {
        if (label is null && tags.Length == 0 && !isFavorite)
        {
            this._items.Remove(key);
            return;
        }

        this._items[key] = new()
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
