using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace JPSoftworks.ScreenManExtension.Helpers;

internal static class CaptureThumbnailLoader
{
    private const uint RecordingThumbnailSize = 512;

    internal static async Task<IRandomAccessStreamReference?> CreateAsync(
        CaptureFile capture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        cancellationToken.ThrowIfCancellationRequested();

        var file = await StorageFile.GetFileFromPathAsync(capture.FullPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (capture.Kind == CaptureMediaKind.Image)
        {
            // Let Command Palette decode the original at the exact pixel size it needs.
            // Its gallery asks for a 256px source and scales that request for display DPI.
            return RandomAccessStreamReference.CreateFromFile(file);
        }

        var thumbnail = await file.GetThumbnailAsync(
            ThumbnailMode.SingleItem,
            RecordingThumbnailSize,
            ThumbnailOptions.ResizeThumbnail);
        cancellationToken.ThrowIfCancellationRequested();
        return thumbnail is null ? null : RandomAccessStreamReference.CreateFromStream(thumbnail);
    }
}
