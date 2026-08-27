using System.ComponentModel;
using System.Diagnostics;

namespace JPSoftworks.ScreenManExtension.Commands;

internal sealed partial class OpenCaptureCommand : InvokableCommand
{
    private readonly string _path;

    internal OpenCaptureCommand(CaptureFile capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        this._path = capture.FullPath;
        this.Name = capture.Kind == CaptureMediaKind.Image ? "Open screenshot" : "Open screen recording";
        this.Icon = capture.Kind == CaptureMediaKind.Image ? Icons.Picture : Icons.Video;
    }

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo(this._path) { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Win32Exception ex)
        {
            ScreenManLog.Error($"Unable to open capture '{this._path}'.", ex);
            return CommandResult.ShowToast("Snipping Manager couldn't open that capture.");
        }
    }
}
