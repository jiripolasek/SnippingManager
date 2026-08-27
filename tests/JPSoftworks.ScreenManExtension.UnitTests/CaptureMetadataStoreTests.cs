using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Model;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CaptureMetadataStoreTests
{
    [TestMethod]
    public void UpdatePersistsNormalizedLabelAndTags()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var storePath = Path.Combine(root, "metadata.json");
            var capturePath = Path.Combine(root, "capture.png");
            var store = new CaptureMetadataStore(storePath);

            store.Update(capturePath, "  Release dialog  ", [" bug ", "release", "BUG", ""]);
            var reloaded = new CaptureMetadataStore(storePath);
            var metadata = reloaded.Get(capturePath);

            Assert.AreEqual("Release dialog", metadata.Label);
            Assert.HasCount(2, metadata.Tags);
            Assert.AreEqual("bug", metadata.Tags[0]);
            Assert.AreEqual("release", metadata.Tags[1]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void UpdateWithEmptyValuesRemovesMetadata()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var storePath = Path.Combine(root, "metadata.json");
            var capturePath = Path.Combine(root, "capture.png");
            var store = new CaptureMetadataStore(storePath);
            store.Update(capturePath, "Label", ["tag"]);

            store.Update(capturePath, " ", Array.Empty<string>());
            var metadata = new CaptureMetadataStore(storePath).Get(capturePath);

            Assert.IsNull(metadata.Label);
            Assert.IsEmpty(metadata.Tags);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void FavoritePersistsAcrossMetadataEditsAndCanExistWithoutLabelOrTags()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var storePath = Path.Combine(root, "metadata.json");
            var capturePath = Path.Combine(root, "capture.png");
            var store = new CaptureMetadataStore(storePath);

            Assert.IsTrue(store.ToggleFavorite(capturePath));
            store.Update(capturePath, "  Keep this  ", ["work"]);
            store.Update(capturePath, " ", []);

            var reloaded = new CaptureMetadataStore(storePath);
            var metadata = reloaded.Get(capturePath);
            Assert.IsTrue(metadata.IsFavorite);
            Assert.IsNull(metadata.Label);
            Assert.IsEmpty(metadata.Tags);

            Assert.IsFalse(reloaded.ToggleFavorite(capturePath));
            Assert.AreEqual(CaptureMetadata.Empty, new CaptureMetadataStore(storePath).Get(capturePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ToggleFavoriteCommandUpdatesMetadataAndKeepsPaletteOpen()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var capturePath = Path.Combine(root, "capture.png");
            var store = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            var capture = new CaptureFile(capturePath, DateTimeOffset.UtcNow, 123, CaptureMediaKind.Image);
            var command = new ToggleFavoriteCommand(capture, store, isFavorite: false);

            var result = command.Invoke();

            Assert.IsTrue(store.Get(capturePath).IsFavorite);
            Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
            var toast = (ToastArgs)result.Args!;
            Assert.IsNotNull(toast.Result);
            Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result.Kind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"screenman-metadata-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
