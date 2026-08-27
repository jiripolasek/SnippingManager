using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace JPSoftworks.ScreenManExtension.Commands;

internal sealed partial class CopyCaptureCommand : InvokableCommand
{
    private readonly CaptureFile _capture;

    internal CopyCaptureCommand(CaptureFile capture)
    {
        this._capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.Name = "Copy capture";
        this.Icon = Icons.Copy;
    }

    public override CommandResult Invoke()
    {
        return CopyToClipboard(this._capture);
    }

    internal static CommandResult CopyToClipboard(CaptureFile capture)
    {
        try
        {
            ClipboardThread.Invoke(() =>
            {
                var file = StorageFile.GetFileFromPathAsync(capture.FullPath).AsTask().GetAwaiter().GetResult();
                var dataPackage = new DataPackage();
                dataPackage.SetStorageItems((IStorageItem[])[file], readOnly: true);
                if (capture.Kind == CaptureMediaKind.Image)
                {
                    dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
                }

                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            });
            return CommandResult.ShowToast($"Copied {capture.FileName}");
        }
        catch (Exception ex)
        {
            ScreenManLog.Error($"Unable to copy capture '{capture.FullPath}'.", ex);
            return CommandResult.ShowToast("Snipping Manager couldn't copy that capture.");
        }
    }
}
