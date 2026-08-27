using System.Drawing;
using System.Drawing.Imaging;
using JPSoftworks.ScreenManExtension.Helpers;
using JPSoftworks.ScreenManExtension.Model;
using Windows.Graphics.Imaging;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CaptureThumbnailLoaderTests
{
    [TestMethod]
    public async Task CreateAsyncForImagePreservesSourceResolution()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"screenman-thumbnail-{Guid.NewGuid():N}.png");
        try
        {
            using (var bitmap = new Bitmap(1200, 600))
            {
                bitmap.Save(imagePath, ImageFormat.Png);
            }

            var capture = new CaptureFile(
                imagePath,
                DateTimeOffset.UtcNow,
                new FileInfo(imagePath).Length,
                CaptureMediaKind.Image);

            var reference = await CaptureThumbnailLoader.CreateAsync(capture, CancellationToken.None);

            Assert.IsNotNull(reference);
            using var stream = await reference.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            Assert.AreEqual(1200U, decoder.PixelWidth);
            Assert.AreEqual(600U, decoder.PixelHeight);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [TestMethod]
    public async Task CreateAsyncHonorsPreCanceledRequest()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var capture = new CaptureFile(
            "unused.png",
            DateTimeOffset.UtcNow,
            0,
            CaptureMediaKind.Image);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => CaptureThumbnailLoader.CreateAsync(capture, cancellation.Token));
    }
}
