using System.Drawing;
using System.Drawing.Imaging;
using JPSoftworks.ScreenManExtension.Helpers;
using JPSoftworks.ScreenManExtension.Model;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CaptureDataPackageFactoryTests
{
    [TestMethod]
    public async Task CreateForImageProvidesCopyableFileAndBitmap()
    {
        var imagePath = CreateTemporaryPath(".png");
        try
        {
            using (var bitmap = new Bitmap(120, 60))
            {
                bitmap.Save(imagePath, ImageFormat.Png);
            }

            var package = CaptureDataPackageFactory.Create(CreateCapture(imagePath, CaptureMediaKind.Image));
            var view = package.GetView();

            Assert.AreEqual(DataPackageOperation.Copy, view.RequestedOperation);
            Assert.AreEqual(Path.GetFileName(imagePath), view.Properties.Title);
            CollectionAssert.Contains(view.AvailableFormats.ToArray(), StandardDataFormats.StorageItems);
            CollectionAssert.Contains(view.AvailableFormats.ToArray(), StandardDataFormats.Bitmap);

            var storageItems = await view.GetStorageItemsAsync();
            Assert.HasCount(1, storageItems);
            Assert.AreEqual(imagePath, storageItems[0].Path, ignoreCase: true);

            var bitmapReference = await view.GetBitmapAsync();
            using var bitmapStream = await bitmapReference.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(bitmapStream);
            Assert.AreEqual(120U, decoder.PixelWidth);
            Assert.AreEqual(60U, decoder.PixelHeight);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [TestMethod]
    public async Task CreateForVideoProvidesCopyableFileWithoutBitmap()
    {
        var videoPath = CreateTemporaryPath(".mp4");
        try
        {
            await File.WriteAllBytesAsync(videoPath, [1, 2, 3]);

            var package = CaptureDataPackageFactory.Create(CreateCapture(videoPath, CaptureMediaKind.Video));
            var view = package.GetView();

            Assert.AreEqual(DataPackageOperation.Copy, view.RequestedOperation);
            CollectionAssert.Contains(view.AvailableFormats.ToArray(), StandardDataFormats.StorageItems);
            CollectionAssert.DoesNotContain(view.AvailableFormats.ToArray(), StandardDataFormats.Bitmap);

            var storageItems = await view.GetStorageItemsAsync();
            Assert.HasCount(1, storageItems);
            Assert.AreEqual(videoPath, storageItems[0].Path, ignoreCase: true);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    private static CaptureFile CreateCapture(string path, CaptureMediaKind kind) =>
        new(path, DateTimeOffset.UtcNow, new FileInfo(path).Length, kind);

    private static string CreateTemporaryPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"screenman-drag-{Guid.NewGuid():N}{extension}");
}
