using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Model;
using JPSoftworks.ScreenManExtension.Pages;
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

    [TestMethod]
    public void LegacyMetadataMigratesAndSurvivesRenameAndReload()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var storePath = Path.Combine(root, "metadata.json");
            var original = new CaptureFile(Path.Combine(root, "original.png"), DateTimeOffset.UtcNow, 3, CaptureMediaKind.Image)
            {
                FileIdentity = "volume:file:created",
            };
            var store = new CaptureMetadataStore(storePath);
            store.Update(original.FullPath, "Release dialog", ["work"]);
            store.ToggleFavorite(original.FullPath);
            var unobservedPath = Path.Combine(root, "unobserved.png");
            store.Update(unobservedPath, "Keep legacy entry", []);

            store.Reconcile([original]);

            var renamed = original with { FullPath = Path.Combine(root, "renamed.png") };
            var reloaded = new CaptureMetadataStore(storePath);
            reloaded.Reconcile([renamed]);
            var metadata = reloaded.Get(renamed.FullPath);
            Assert.AreEqual("Release dialog", metadata.Label);
            Assert.AreEqual("work", metadata.Tags.Single());
            Assert.IsTrue(metadata.IsFavorite);
            Assert.AreEqual(CaptureMetadata.Empty, reloaded.Get(original.FullPath));
            Assert.AreEqual("Keep legacy entry", reloaded.Get(unobservedPath).Label);

            reloaded.Update(renamed.FullPath, null, []);
            reloaded.ToggleFavorite(renamed.FullPath);
            var cleared = new CaptureMetadataStore(storePath);
            cleared.Reconcile([renamed]);
            Assert.AreEqual(CaptureMetadata.Empty, cleared.Get(renamed.FullPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ReplacementAtTheSamePathDoesNotInheritTheOriginalFilesMetadata()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var storePath = Path.Combine(root, "metadata.json");
            var original = new CaptureFile(Path.Combine(root, "capture.png"), DateTimeOffset.UtcNow, 3, CaptureMediaKind.Image)
            {
                FileIdentity = "original-file",
            };
            var store = new CaptureMetadataStore(storePath);
            store.Reconcile([original]);
            store.Update(original.FullPath, "Original", ["original"]);
            store.ToggleFavorite(original.FullPath);
            var editor = new EditCaptureMetadataPage(original, store);
            var favoriteCommand = new ToggleFavoriteCommand(original, store, isFavorite: true);
            var replacement = original with { FileIdentity = "replacement-file" };
            var moved = original with { FullPath = Path.Combine(root, "moved.png") };

            store.Reconcile([replacement, moved]);

            Assert.AreEqual(CaptureMetadata.Empty, store.Get(replacement.FullPath));
            Assert.AreEqual("Original", store.Get(moved.FullPath).Label);
            Assert.IsTrue(store.Get(moved.FullPath).IsFavorite);
            store.Update(replacement.FullPath, "Replacement", ["replacement"]);

            var reloaded = new CaptureMetadataStore(storePath);
            reloaded.Reconcile([replacement, moved]);
            Assert.AreEqual("Replacement", reloaded.Get(replacement.FullPath).Label);
            Assert.AreEqual("Original", reloaded.Get(moved.FullPath).Label);
            Assert.IsTrue(reloaded.Get(moved.FullPath).IsFavorite);

            // Commands opened before the move retain the original identity, even if its old path is reused.
            var form = Assert.IsInstanceOfType<FormContent>(editor.GetContent().Single());
            form.SubmitForm("""{"label":"Edited after moving","tags":"kept"}""", string.Empty);
            favoriteCommand.Invoke();
            Assert.AreEqual("Edited after moving", store.Get(moved.FullPath).Label);
            Assert.IsFalse(store.Get(moved.FullPath).IsFavorite);
            Assert.AreEqual("Replacement", store.Get(replacement.FullPath).Label);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void UnidentifiedCapturesRetainPathMetadataWithoutGuessingRenames()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var original = new CaptureFile(Path.Combine(root, "capture.png"), DateTimeOffset.UtcNow, 3, CaptureMediaKind.Image);
            var storePath = Path.Combine(root, "metadata.json");
            var store = new CaptureMetadataStore(storePath);
            store.Update(original.FullPath, "Path metadata", ["work"]);
            store.Reconcile([original]);
            Assert.AreEqual("Path metadata", store.Get(original.FullPath).Label);

            var renamed = original with { FullPath = Path.Combine(root, "same-size-and-date.png") };
            var reloaded = new CaptureMetadataStore(storePath);
            reloaded.Reconcile([renamed]);

            Assert.AreEqual(CaptureMetadata.Empty, reloaded.Get(renamed.FullPath));
            Assert.AreEqual("Path metadata", reloaded.Get(original.FullPath).Label);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LegacyAliasesMergeTagsAndFavoritesWithoutReplacingAnExistingIdentityLabel()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var original = new CaptureFile(Path.Combine(root, "original.png"), DateTimeOffset.UtcNow, 3, CaptureMediaKind.Image)
            {
                FileIdentity = "shared-file",
            };
            var alias = original with { FullPath = Path.Combine(root, "alias.png") };
            var store = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            store.Reconcile([original]);
            store.Update(original.FullPath, "Existing label", ["work"]);
            store.Update(alias.FullPath, "Legacy alias", ["WORK", "release"]);
            store.ToggleFavorite(alias.FullPath);

            store.Reconcile([original, alias]);

            var metadata = store.Get(alias.FullPath);
            Assert.AreEqual("Existing label", metadata.Label);
            CollectionAssert.AreEquivalent(new List<string> { "work", "release" }, metadata.Tags.ToArray());
            Assert.IsTrue(metadata.IsFavorite);
            Assert.AreEqual(metadata.Label, store.Get(original.FullPath).Label);
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
