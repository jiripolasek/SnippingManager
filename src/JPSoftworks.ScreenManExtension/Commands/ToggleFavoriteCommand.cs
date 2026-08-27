namespace JPSoftworks.ScreenManExtension.Commands;

internal sealed partial class ToggleFavoriteCommand : InvokableCommand
{
    private readonly CaptureFile _capture;
    private readonly CaptureMetadataStore _metadataStore;

    internal ToggleFavoriteCommand(
        CaptureFile capture,
        CaptureMetadataStore metadataStore,
        bool isFavorite)
    {
        this._capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this._metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        this.Name = isFavorite ? "Remove from favorites" : "Add to favorites";
        this.Icon = isFavorite ? Icons.FavoriteFilled : Icons.Favorite;
    }

    public override CommandResult Invoke()
    {
        var isFavorite = this._metadataStore.ToggleFavorite(this._capture.FullPath, this._capture.FileIdentity);
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = isFavorite
                ? $"Added {this._capture.FileName} to favorites"
                : $"Removed {this._capture.FileName} from favorites",
            Result = CommandResult.KeepOpen(),
        });
    }
}
