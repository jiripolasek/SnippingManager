using Windows.Storage;

namespace JPSoftworks.ScreenManExtension.Commands;

internal sealed partial class DeleteCaptureCommand : InvokableCommand
{
    private readonly CaptureFile _capture;
    private readonly Func<string, Task> _deleteFileAsync;
    private readonly Action<string>? _onDeleted;
    private readonly bool _isConfirmed;

    internal DeleteCaptureCommand(
        CaptureFile capture,
        Func<string, Task>? deleteFileAsync = null,
        Action<string>? onDeleted = null)
        : this(capture, deleteFileAsync ?? DeleteFileAsync, onDeleted, isConfirmed: false)
    {
    }

    private DeleteCaptureCommand(
        CaptureFile capture,
        Func<string, Task> deleteFileAsync,
        Action<string>? onDeleted,
        bool isConfirmed)
    {
        this._capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this._deleteFileAsync = deleteFileAsync;
        this._onDeleted = onDeleted;
        this._isConfirmed = isConfirmed;
        this.Name = "Delete";
        this.Icon = Icons.Delete;
    }

    public override CommandResult Invoke()
    {
        if (!this._isConfirmed)
        {
            return CommandResult.Confirm(new ConfirmationArgs
            {
                Title = $"Delete {this._capture.FileName}?",
                Description = $"{this._capture.FullPath}\n\n" +
                    "Windows will move this file to the Recycle Bin when available. " +
                    "If its location or your Windows settings do not allow recycling, it will be permanently deleted.",
                PrimaryCommand = new DeleteCaptureCommand(this._capture, this._deleteFileAsync, this._onDeleted, isConfirmed: true),
                IsPrimaryCommandCritical = true,
            });
        }

        try
        {
            this._deleteFileAsync(this._capture.FullPath).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return CommandResult.KeepOpen();
        }
        catch (FileNotFoundException)
        {
            this.NotifyDeleted();
            return ShowToast("That capture no longer exists.");
        }
        catch (Exception ex)
        {
            ScreenManLog.Error($"Unable to delete capture '{this._capture.FullPath}'.", ex);
            return ShowToast("Snipping Manager couldn't delete that capture.");
        }

        this.NotifyDeleted();
        return ShowToast($"Deleted {this._capture.FileName}");
    }

    private void NotifyDeleted()
    {
        try
        {
            this._onDeleted?.Invoke(this._capture.FullPath);
        }
        catch (Exception ex)
        {
            ScreenManLog.Error($"Unable to update the capture list after deleting '{this._capture.FullPath}'.", ex);
        }
    }

    private static async Task DeleteFileAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
        // Use File Explorer's normal recycle/delete behavior for this location.
        await file.DeleteAsync(StorageDeleteOption.Default).AsTask().ConfigureAwait(false);
    }

    private static CommandResult ShowToast(string message)
    {
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = message,
            Result = CommandResult.KeepOpen(),
        });
    }
}
