namespace JPSoftworks.ScreenManExtension.Commands;

internal sealed partial class OpenLatestCaptureCommand : InvokableCommand
{
    private readonly CaptureCatalog _catalog;
    private readonly CaptureMediaKind? _kind;
    private readonly Func<CaptureFile, CommandResult> _openCapture;

    internal OpenLatestCaptureCommand(
        CaptureCatalog catalog,
        CaptureMediaKind? kind = null,
        Func<CaptureFile, CommandResult>? openCapture = null)
    {
        this._catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this._kind = kind;
        this._openCapture = openCapture ?? (static capture => new OpenCaptureCommand(capture).Invoke());
        var captureType = kind switch
        {
            CaptureMediaKind.Image => "screenshot",
            CaptureMediaKind.Video => "recording",
            null => "capture",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        this.Id = $"com.jpsoftworks.cmdpal.screenman.open-latest-{captureType}";
        this.Name = $"Open latest {captureType}";
        this.Icon = kind switch
        {
            CaptureMediaKind.Image => Icons.Picture,
            CaptureMediaKind.Video => Icons.Video,
            _ => Icons.Main,
        };
    }

    public override CommandResult Invoke()
    {
        if (!this._catalog.IsInitialized)
        {
            return CommandResult.ShowToast("Snipping Manager is still loading your captures.");
        }

        return this._catalog.TryGetLatest(out var capture, this._kind) && capture is not null
            ? this._openCapture(capture)
            : CommandResult.ShowToast(this._kind switch
            {
                CaptureMediaKind.Image => "No screenshots were found.",
                CaptureMediaKind.Video => "No screen recordings were found.",
                _ => "No screenshots or screen recordings were found.",
            });
    }
}
