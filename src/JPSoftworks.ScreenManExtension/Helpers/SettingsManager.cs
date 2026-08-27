using System.Diagnostics.CodeAnalysis;

namespace JPSoftworks.ScreenManExtension.Helpers;

internal sealed class SettingsManager : JsonSettingsManager, ICaptureSettings
{
    private const string DefaultNamespace = "jpsoftworks.screenman";

    private string? _lastFoldersValue;
    private bool _lastIncludeSubfolders;

    [SuppressMessage("Maintainability", "CA1507:Use nameof to express symbol names", Justification = "Persisted settings keys must remain stable.")]
    private readonly TextSetting _folders = new(
        Namespaced("Sources.Folders"),
        "Capture folders",
        "Enter one folder per line. Environment variables such as %USERPROFILE% are supported.",
        CaptureFolderParser.GetDefaultSettingValue())
    {
        Multiline = true,
        Placeholder = "%USERPROFILE%\\Pictures\\Screenshots",
    };

    [SuppressMessage("Maintainability", "CA1507:Use nameof to express symbol names", Justification = "Persisted settings keys must remain stable.")]
    private readonly ToggleSetting _includeSubfolders = new(
        Namespaced("Sources.IncludeSubfolders"),
        "Include subfolders",
        "Discover captures in nested project or date folders.",
        true);

    [SuppressMessage("Maintainability", "CA1507:Use nameof to express symbol names", Justification = "Persisted settings keys must remain stable.")]
    private readonly ToggleSetting _showDetailsAutomatically = new(
        Namespaced("Layout.ShowDetailsAutomatically"),
        "Show details automatically",
        "Open the details pane automatically when browsing captures.",
        true);

    [SuppressMessage("Maintainability", "CA1507:Use nameof to express symbol names", Justification = "Persisted settings keys must remain stable.")]
    private readonly ToggleSetting _openInPreview = new(
        Namespaced("Behavior.OpenInPreview"),
        "Open captures in Command Palette",
        "Use a preview page instead of the default app. Recordings show a still preview; open the default app for playback.",
        false);

    internal SettingsManager(string? filePath = null)
    {
        this.FilePath = filePath ?? GetSettingsPath();
        this.Settings.Add(new SettingsGroupHeader(
            Namespaced("Layout.Sources"),
            "Folders",
            showSeparator: false));
        this.Settings.Add(this._folders);
        this.Settings.Add(this._includeSubfolders);
        this.Settings.Add(new SettingsGroupHeader(
            Namespaced("Layout.Behavior"),
            "Behavior"));
        this.Settings.Add(this._openInPreview);
        this.Settings.Add(new SettingsGroupHeader(
            Namespaced("Layout.Appearance"),
            "Appearance"));
        this.Settings.Add(this._showDetailsAutomatically);

        this.LoadSettings();
        this._lastFoldersValue = this._folders.Value;
        this._lastIncludeSubfolders = this._includeSubfolders.Value;
        this.Settings.SettingsChanged += this.OnSettingsChanged;
    }

    public event EventHandler? SourcesChanged;

    public IReadOnlyList<string> FolderPaths => CaptureFolderParser.Parse(this._folders.Value);

    public bool IncludeSubfolders => this._includeSubfolders.Value;

    internal bool ShowDetailsAutomatically => this._showDetailsAutomatically.Value;

    internal bool OpenInPreview => this._openInPreview.Value;

    private void OnSettingsChanged(object sender, Settings args)
    {
        var sourcesChanged =
            !StringComparer.Ordinal.Equals(this._lastFoldersValue, this._folders.Value) ||
            this._lastIncludeSubfolders != this._includeSubfolders.Value;

        this.SaveSettings();

        if (sourcesChanged)
        {
            this._lastFoldersValue = this._folders.Value;
            this._lastIncludeSubfolders = this._includeSubfolders.Value;
            this.SourcesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string Namespaced(string propertyName)
    {
        return $"{DefaultNamespace}.{propertyName}";
    }

    private static string GetSettingsPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "screenman.settings.json");
    }
}
