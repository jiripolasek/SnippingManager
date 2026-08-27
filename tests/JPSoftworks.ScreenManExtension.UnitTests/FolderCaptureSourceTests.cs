using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Helpers;
using JPSoftworks.ScreenManExtension.Model;
using JPSoftworks.ScreenManExtension.Sources;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class FolderCaptureSourceTests
{
    [TestMethod]
    public void GetCapturesFindsSupportedMediaRecursivelyAndSortsNewestFirst()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
            var screenshot = Path.Combine(root, "screenshot.png");
            var recording = Path.Combine(nested, "recording.mp4");
            File.WriteAllBytes(screenshot, [1, 2, 3]);
            File.WriteAllBytes(recording, [4, 5, 6, 7]);
            File.WriteAllText(Path.Combine(root, "notes.txt"), "ignore me");
            File.SetLastWriteTimeUtc(screenshot, DateTime.UtcNow.AddMinutes(-2));
            File.SetLastWriteTimeUtc(recording, DateTime.UtcNow.AddMinutes(-1));

            var settings = new TestCaptureSettings([root], includeSubfolders: true);
            using var source = new FolderCaptureSource(settings);

            var captures = source.GetCaptures(CancellationToken.None);

            Assert.HasCount(2, captures);
            Assert.AreEqual(recording, captures[0].FullPath, ignoreCase: true);
            Assert.AreEqual(CaptureMediaKind.Video, captures[0].Kind);
            Assert.AreEqual(CaptureMediaKind.Image, captures[1].Kind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void GetCapturesSkipsNestedMediaWhenRecursionIsDisabled()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
            File.WriteAllBytes(Path.Combine(nested, "recording.mp4"), [1]);
            var settings = new TestCaptureSettings([root], includeSubfolders: false);
            using var source = new FolderCaptureSource(settings);

            var captures = source.GetCaptures(CancellationToken.None);

            Assert.IsEmpty(captures);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ConfirmedDeleteNotifiesTheSourceAndRemovesTheCapture()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var screenshot = Path.Combine(root, "screenshot.png");
            File.WriteAllBytes(screenshot, [1, 2, 3]);
            var settings = new TestCaptureSettings([root], includeSubfolders: false);
            using var source = new FolderCaptureSource(settings);
            var capture = source.GetCaptures(CancellationToken.None).Single();
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Changed += (_, _) =>
            {
                if (!File.Exists(screenshot))
                {
                    changed.TrySetResult();
                }
            };
            var command = new DeleteCaptureCommand(capture);
            var confirmation = Assert.IsInstanceOfType<ConfirmationArgs>(command.Invoke().Args);
            Assert.IsTrue(File.Exists(screenshot));

            var result = Assert.IsInstanceOfType<InvokableCommand>(confirmation.PrimaryCommand).Invoke();

            Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
            Assert.IsFalse(File.Exists(screenshot));
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsEmpty(source.GetCaptures(CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FileAndFolderRenamesRetainMetadataBeforeTheCatalogNotifies(bool renameFolder)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, "before")).FullName;
            var originalPath = Path.Combine(folder, "capture.png");
            File.WriteAllBytes(originalPath, [1, 2, 3]);
            var storePath = Path.Combine(root, "metadata.json");
            var store = new CaptureMetadataStore(storePath);
            store.Update(originalPath, "Keep this capture", ["work"]);
            store.ToggleFavorite(originalPath);
            using var catalog = new CaptureCatalog(new FolderCaptureSource(new TestCaptureSettings([root], true)), store);
            await WaitForCaptureAsync(catalog, originalPath);
            var originalIdentity = catalog.GetSnapshot().Single().FileIdentity;
            Assert.IsNotNull(originalIdentity);
            var newFolder = Path.Combine(root, "after");
            var renamedPath = renameFolder ? Path.Combine(newFolder, "capture.png") : Path.Combine(folder, "renamed.png");
            string? labelAtNotification = null;
            catalog.Changed += (_, _) => labelAtNotification = store.Get(renamedPath).Label;

            await WaitForCaptureAsync(catalog, renamedPath, () =>
            {
                if (renameFolder)
                {
                    Directory.Move(folder, newFolder);
                }
                else
                {
                    File.Move(originalPath, renamedPath);
                }
            });

            Assert.AreEqual(originalIdentity, catalog.GetSnapshot().Single().FileIdentity);
            Assert.AreEqual("Keep this capture", labelAtNotification);
            Assert.AreEqual("work", store.Get(renamedPath).Tags.Single());
            Assert.IsTrue(store.Get(renamedPath).IsFavorite);
            Assert.AreEqual(CaptureMetadata.Empty, store.Get(originalPath));
            CollectionAssert.AreEqual(new List<byte> { 1, 2, 3 }, File.ReadAllBytes(renamedPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MoveWhileNotWatchingSurvivesReloadAndDoesNotTagACopy()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.png");
            File.WriteAllBytes(originalPath, [1, 2, 3]);
            var storePath = Path.Combine(root, "metadata.json");
            var store = new CaptureMetadataStore(storePath);
            store.Update(originalPath, "Original capture", ["work"]);
            store.ToggleFavorite(originalPath);
            var originalIdentity = CaptureFileIdentity.TryGet(originalPath);
            Assert.IsNotNull(originalIdentity);
            var movedFolder = Directory.CreateDirectory(Path.Combine(root, "moved")).FullName;
            var movedPath = Path.Combine(movedFolder, "renamed.png");

            File.Move(originalPath, movedPath);
            File.Copy(movedPath, originalPath);

            var reloaded = new CaptureMetadataStore(storePath);
            using var source = new FolderCaptureSource(new TestCaptureSettings([root], true));
            var captures = source.GetCaptures(CancellationToken.None);
            reloaded.Reconcile(captures);
            Assert.AreEqual(originalIdentity, captures.Single(capture => capture.FullPath == movedPath).FileIdentity);
            Assert.AreNotEqual(originalIdentity, captures.Single(capture => capture.FullPath == originalPath).FileIdentity);
            Assert.AreEqual("Original capture", reloaded.Get(movedPath).Label);
            Assert.AreEqual("work", reloaded.Get(movedPath).Tags.Single());
            Assert.IsTrue(reloaded.Get(movedPath).IsFavorite);
            Assert.AreEqual(CaptureMetadata.Empty, reloaded.Get(originalPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MissingFileHasNoIdentityAndIsNotCreated()
    {
        var path = Path.Combine(Path.GetTempPath(), $"screenman-missing-{Guid.NewGuid():N}.png");

        Assert.IsNull(CaptureFileIdentity.TryGet(path));
        Assert.IsFalse(File.Exists(path));
    }

    private static async Task WaitForCaptureAsync(CaptureCatalog catalog, string path, Action? trigger = null)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs args)
        {
            if (catalog.GetSnapshot().Any(capture => StringComparer.OrdinalIgnoreCase.Equals(capture.FullPath, path)))
            {
                changed.TrySetResult();
            }
        }

        catalog.Changed += OnChanged;
        try
        {
            trigger?.Invoke();
            OnChanged(null, EventArgs.Empty);
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            catalog.Changed -= OnChanged;
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"screenman-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestCaptureSettings : ICaptureSettings
    {
        internal TestCaptureSettings(IReadOnlyList<string> paths, bool includeSubfolders)
        {
            this.FolderPaths = paths;
            this.IncludeSubfolders = includeSubfolders;
        }

        public event EventHandler? SourcesChanged;

        public IReadOnlyList<string> FolderPaths { get; }

        public bool IncludeSubfolders { get; }

        internal void RaiseSourcesChanged()
        {
            this.SourcesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
