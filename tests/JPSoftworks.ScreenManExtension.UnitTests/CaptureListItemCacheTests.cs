using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Model;
using JPSoftworks.ScreenManExtension.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CaptureListItemCacheTests
{
    [TestMethod]
    public void CacheUpdatesMetadataWithoutReplacingTheItemOrPreview()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            using var cache = new CaptureListItemCache();
            var capturePath = Path.Combine(root, "capture.png");
            var capture = new CaptureFile(capturePath, DateTimeOffset.UtcNow, 123, CaptureMediaKind.Image);
            var store = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));

            var original = cache.GetOrCreate(
                capture,
                CaptureMetadata.Empty,
                store,
                thumbnailCancellationToken: cancellation.Token);
            var reused = cache.GetOrCreate(
                capture with { FullPath = capturePath.ToUpperInvariant() },
                new CaptureMetadata(null, []),
                store,
                thumbnailCancellationToken: cancellation.Token);
            Assert.IsNotNull(original);
            var preview = original.MoreCommands.OfType<CommandContextItem>()
                .Select(context => context.Command).OfType<CapturePreviewPage>().Single();
            var icon = original.Icon;
            var dataPackage = original.DataPackage;
            store.Update(capturePath, "Favorite capture", ["work"]);
            store.ToggleFavorite(capturePath);
            var updated = cache.GetOrCreate(
                capture,
                store.Get(capturePath),
                store,
                thumbnailCancellationToken: cancellation.Token);

            Assert.AreSame(original, reused);
            Assert.AreSame(original, updated);
            Assert.AreSame(icon, original.Icon);
            Assert.AreSame(dataPackage, original.DataPackage);
            Assert.AreEqual("Favorite capture", original.Title);
            Assert.AreEqual(original.Title, original.TextToSuggest);
            Assert.AreEqual(original.Title, preview.Title);
            Assert.AreEqual(original.Details!.Body, preview.Details!.Body);
            Assert.AreSame(original.Details.Metadata, preview.Details.Metadata);
            var commands = original.MoreCommands.OfType<CommandContextItem>().Select(context => context.Command).ToArray();
            Assert.AreSame(preview, commands.OfType<CapturePreviewPage>().Single());
            Assert.AreEqual("Remove from favorites", commands.OfType<ToggleFavoriteCommand>().Single().Name);
            var form = Assert.IsInstanceOfType<FormContent>(commands.OfType<EditCaptureMetadataPage>().Single().GetContent().Single());
            StringAssert.Contains(form.TemplateJson, "Favorite capture");
            StringAssert.Contains(form.TemplateJson, "work");

            var replaced = cache.GetOrCreate(
                capture with { SizeInBytes = 456 },
                store.Get(capturePath),
                store,
                thumbnailCancellationToken: cancellation.Token);
            Assert.AreNotSame(original, replaced);
            Assert.AreEqual(1, cache.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void CachePrunesRemovedCaptures()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            using var cache = new CaptureListItemCache();
            var capture = new CaptureFile(
                Path.Combine(root, "capture.png"),
                DateTimeOffset.UtcNow,
                123,
                CaptureMediaKind.Image);
            var store = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            cache.GetOrCreate(capture, CaptureMetadata.Empty, store, thumbnailCancellationToken: cancellation.Token);

            cache.Prune([]);

            Assert.AreEqual(0, cache.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void OpeningBehaviorChangesWithoutReplacingCachedItems()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            using var cache = new CaptureListItemCache();
            var capture = new CaptureFile(Path.Combine(root, "capture.png"), DateTimeOffset.UtcNow, 123, CaptureMediaKind.Image);
            var store = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            var item = cache.GetOrCreate(capture, CaptureMetadata.Empty, store, thumbnailCancellationToken: cancellation.Token);
            Assert.IsNotNull(item);
            var external = Assert.IsInstanceOfType<OpenCaptureCommand>(item.Command);
            var preview = item.MoreCommands.OfType<CommandContextItem>()
                .Select(context => context.Command).OfType<CapturePreviewPage>().Single();
            var icon = item.Icon;
            var commandChanges = 0;
            item.PropChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CaptureListItem.Command))
                {
                    commandChanges++;
                }
            };

            cache.SetOpenInPreview(true);

            Assert.AreSame(preview, item.Command);
            Assert.AreSame(item, cache.GetOrCreate(capture, CaptureMetadata.Empty, store, thumbnailCancellationToken: cancellation.Token));
            Assert.AreSame(icon, item.Icon);
            Assert.AreEqual(1, commandChanges);
            Assert.AreSame(external, item.MoreCommands.OfType<CommandContextItem>().Last().Command);

            cache.SetOpenInPreview(false);

            Assert.AreSame(external, item.Command);
            Assert.AreSame(preview, item.MoreCommands.OfType<CommandContextItem>().Last().Command);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"screenman-cache-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
