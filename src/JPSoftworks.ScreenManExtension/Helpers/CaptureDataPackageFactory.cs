using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace JPSoftworks.ScreenManExtension.Helpers;

internal static class CaptureDataPackageFactory
{
    internal static DataPackage Create(CaptureFile capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        var path = capture.FullPath;
        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy,
            Properties =
            {
                Title = capture.FileName,
                Description = capture.Kind == CaptureMediaKind.Image
                    ? "Screenshot file"
                    : "Screen recording file",
            },
        };

        dataPackage.SetDataProvider(
            StandardDataFormats.StorageItems,
            request => ProvideStorageItem(request, path));

        if (capture.Kind == CaptureMediaKind.Image)
        {
            dataPackage.SetDataProvider(
                StandardDataFormats.Bitmap,
                request => ProvideBitmap(request, path));
        }

        return dataPackage;
    }

    private static async void ProvideStorageItem(DataProviderRequest request, string path)
    {
        var deferral = request.GetDeferral();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            request.SetData((IStorageItem[])[file]);
        }
        catch (Exception ex)
        {
            ScreenManLog.Error($"Unable to provide dragged capture '{path}'.", ex);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static async void ProvideBitmap(DataProviderRequest request, string path)
    {
        var deferral = request.GetDeferral();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            request.SetData(RandomAccessStreamReference.CreateFromFile(file));
        }
        catch (Exception ex)
        {
            ScreenManLog.Error($"Unable to provide dragged screenshot bitmap '{path}'.", ex);
        }
        finally
        {
            deferral.Complete();
        }
    }
}
