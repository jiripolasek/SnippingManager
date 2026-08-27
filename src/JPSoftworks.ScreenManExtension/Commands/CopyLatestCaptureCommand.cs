namespace JPSoftworks.ScreenManExtension.Commands;

internal sealed partial class CopyLatestCaptureCommand : InvokableCommand
{
    private readonly CaptureCatalog _catalog;

    internal CopyLatestCaptureCommand(CaptureCatalog catalog)
    {
        this._catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.Name = "Copy latest capture";
        this.Icon = Icons.Copy;
    }

    public override CommandResult Invoke()
    {
        if (!this._catalog.IsInitialized)
        {
            return CommandResult.ShowToast("Snipping Manager is still loading your captures.");
        }

        return this._catalog.TryGetLatest(out var capture) && capture is not null
            ? CopyCaptureCommand.CopyToClipboard(capture)
            : CommandResult.ShowToast("No screenshots or screen recordings were found.");
    }
}
