using System.Drawing;
using System.Drawing.Imaging;
using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Model;
using JPSoftworks.ScreenManExtension.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Graphics.Imaging;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CapturePreviewPageTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PreviewUsesImageContentAndCaptureDetails(bool isRecording)
    {
        var capture = new CaptureFile(
            Path.Combine(Path.GetTempPath(), isRecording ? "preview #1.mp4" : "preview #1.png"),
            DateTimeOffset.UtcNow,
            123,
            isRecording ? CaptureMediaKind.Video : CaptureMediaKind.Image);
        var details = new Details
        {
            Title = "Release screenshot",
            Body = "Capture date, type, size, and path",
            HeroImage = new IconInfo("gallery-only-hero"),
            Metadata = [new DetailsElement { Key = "Tags", Data = new DetailsTags { Tags = [new Tag("work")] } }],
        };
        var page = new CapturePreviewPage(capture, details);

        var content = page.GetContent();

        Assert.AreSame(content, page.GetContent());
        var image = Assert.IsInstanceOfType<ImageContent>(content[0]);
        Assert.AreEqual(ImageContent.Unlimited, image.MaxWidth);
        Assert.AreEqual(ImageContent.Unlimited, image.MaxHeight);
        var previewDetails = Assert.IsInstanceOfType<Details>(page.Details);
        Assert.AreEqual(details.Title, page.Title);
        Assert.AreEqual(details.Body, previewDetails.Body);
        Assert.AreSame(details.Metadata, previewDetails.Metadata);
        Assert.IsTrue(string.IsNullOrEmpty(previewDetails.HeroImage.Light.Icon));
        Assert.IsInstanceOfType<OpenCaptureCommand>(page.Commands.OfType<CommandContextItem>().First().Command);
        if (isRecording)
        {
            Assert.HasCount(2, content);
            var note = Assert.IsInstanceOfType<PlainTextContent>(content[1]);
            StringAssert.Contains(note.Text, "Still preview");
        }
        else
        {
            Assert.HasCount(1, content);
            var icon = Assert.IsInstanceOfType<IconInfo>(image.Image);
            Assert.AreEqual(capture.FullPath, icon.Light.Icon);
            Assert.AreEqual(capture.FullPath, icon.Dark.Icon);
        }
    }

    [TestMethod]
    public async Task PreviewReceivesFullResolutionImageFromGalleryLoader()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"screenman-preview-#{Guid.NewGuid():N}.png");
        try
        {
            using (var bitmap = new Bitmap(1200, 600))
            {
                bitmap.Save(imagePath, ImageFormat.Png);
            }

            var capture = new CaptureFile(imagePath, DateTimeOffset.UtcNow, new FileInfo(imagePath).Length, CaptureMediaKind.Image);
            var metadataStore = new CaptureMetadataStore(imagePath + ".metadata.json");
            using var item = new CaptureListItem(capture, CaptureMetadata.Empty, metadataStore, openInPreview: true);
            var page = Assert.IsInstanceOfType<CapturePreviewPage>(item.Command);
            var image = Assert.IsInstanceOfType<ImageContent>(page.GetContent().Single());
            var imageChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            image.PropChanged += (_, _) => imageChanged.TrySetResult();
            if (image.Image?.Light.Data is null)
            {
                await imageChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }

            var reference = image.Image?.Light.Data;
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
}
