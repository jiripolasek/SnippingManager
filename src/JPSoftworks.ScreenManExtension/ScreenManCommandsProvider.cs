namespace JPSoftworks.ScreenManExtension;

public sealed partial class ScreenManCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();
    private readonly CaptureMetadataStore _metadataStore = new();
    private readonly CaptureCatalog _catalog;
    private readonly CaptureManagerPage _page;
    private readonly ICommandItem[] _commands;
    private bool _isDisposed;

    public ScreenManCommandsProvider()
        : this(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
    {
    }

    internal ScreenManCommandsProvider(ILoggerFactory loggerFactory)
    {
        ScreenManLog.Initialize(loggerFactory);
        this.Id = "JPSoftworks.CmdPal.ScreenMan";
        this.DisplayName = "Snipping Manager";
        this.Icon = Icons.Main;
        this.Settings = this._settingsManager.Settings;

        this._catalog = new(new FolderCaptureSource(this._settingsManager), this._metadataStore);
        this._page = new(this._catalog, this._metadataStore, this._settingsManager);
        this._commands =
        [
            new CommandItem(this._page)
            {
                Title = "Screenshots and recordings",
                Subtitle = "Browse, search, label, tag, and copy your captures",
                Icon = Icons.Main,
                MoreCommands =
                [
                    new CommandContextItem(this.Settings.SettingsPage!),
                ],
            },
            new CommandItem(new CopyLatestCaptureCommand(this._catalog))
            {
                Title = "Copy latest capture",
                Subtitle = "Put the newest screenshot or recording on the clipboard",
                Icon = Icons.Copy,
            },
            new CommandItem(new OpenLatestCaptureCommand(this._catalog))
            {
                Subtitle = "Open the newest screenshot or recording in your default app",
                Icon = Icons.Main,
            },
            new CommandItem(new CopyLatestCaptureCommand(this._catalog, CaptureMediaKind.Image))
            {
                Subtitle = "Copy the newest screenshot as an image and file",
                Icon = Icons.Copy,
            },
            new CommandItem(new CopyLatestCaptureCommand(this._catalog, CaptureMediaKind.Video))
            {
                Subtitle = "Copy the newest screen recording as a file",
                Icon = Icons.Copy,
            },
            new CommandItem(new OpenLatestCaptureCommand(this._catalog, CaptureMediaKind.Image))
            {
                Subtitle = "Open the newest screenshot in your default app",
                Icon = Icons.Picture,
            },
            new CommandItem(new OpenLatestCaptureCommand(this._catalog, CaptureMediaKind.Video))
            {
                Subtitle = "Open the newest screen recording in your default app",
                Icon = Icons.Video,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return this._commands;
    }

    public override void Dispose()
    {
        if (this._isDisposed)
        {
            return;
        }

        this._isDisposed = true;
        this._page.Dispose();
        this._catalog.Dispose();
        base.Dispose();
    }
}
