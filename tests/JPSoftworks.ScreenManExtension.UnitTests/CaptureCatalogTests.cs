using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Helpers;
using JPSoftworks.ScreenManExtension.Model;
using JPSoftworks.ScreenManExtension.Pages;
using JPSoftworks.ScreenManExtension.Sources;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CaptureCatalogTests
{
    [TestMethod]
    public async Task RemoveUpdatesSnapshotAndLatestWithoutRescanning()
    {
        var newest = CreateCapture();
        var older = CreateCapture() with { ModifiedAtUtc = newest.ModifiedAtUtc.AddMinutes(-1) };
        var source = new TestCaptureSource((_, _) => [newest, older]);
        using var catalog = new CaptureCatalog(source);
        await WaitUntilAsync(catalog, () => catalog.IsInitialized);
        var changes = 0;
        catalog.Changed += (_, _) => changes++;

        catalog.Remove(newest.FullPath.ToUpperInvariant());

        Assert.AreEqual(older, catalog.GetSnapshot().Single());
        Assert.IsTrue(catalog.TryGetLatest(out var latest));
        Assert.AreEqual(older, latest);
        Assert.AreEqual(1, changes);
        Assert.AreEqual(1, source.ReadCount);

        catalog.Remove(newest.FullPath);

        Assert.AreEqual(1, changes);

        catalog.Remove(older.FullPath);

        Assert.IsEmpty(catalog.GetSnapshot());
        Assert.IsFalse(catalog.TryGetLatest(out _));
        Assert.AreEqual(2, changes);
    }

    [TestMethod]
    public async Task OlderRefreshCannotBringBackARemovedCapture()
    {
        var removedCapture = CreateCapture();
        var nextCapture = CreateCapture();
        var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseScan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new TestCaptureSource((readCount, cancellationToken) =>
        {
            if (readCount == 2)
            {
                scanStarted.TrySetResult();
                releaseScan.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
                return [removedCapture];
            }

            return readCount == 1 ? [removedCapture] : [nextCapture];
        });
        using var catalog = new CaptureCatalog(source);
        try
        {
            await WaitUntilAsync(catalog, () => catalog.IsInitialized);
            source.NotifyChanged();
            await scanStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            catalog.Remove(removedCapture.FullPath);

            Assert.IsEmpty(catalog.GetSnapshot());
            var reappeared = 0;
            catalog.Changed += (_, _) =>
            {
                if (catalog.GetSnapshot().Contains(removedCapture))
                {
                    Interlocked.Exchange(ref reappeared, 1);
                }
            };

            await WaitUntilAsync(
                catalog,
                () => catalog.GetSnapshot().Contains(nextCapture),
                () => releaseScan.TrySetResult());

            Assert.AreEqual(0, reappeared);
            Assert.AreEqual(nextCapture, catalog.GetSnapshot().Single());
            Assert.AreEqual(3, source.ReadCount);
        }
        finally
        {
            releaseScan.TrySetResult();
        }
    }

    [TestMethod]
    public async Task FreshScanCanRediscoverARestoredCapture()
    {
        var capture = CreateCapture();
        var source = new TestCaptureSource((_, _) => [capture]);
        using var catalog = new CaptureCatalog(source);
        await WaitUntilAsync(catalog, () => catalog.IsInitialized);
        catalog.Remove(capture.FullPath);
        Assert.IsEmpty(catalog.GetSnapshot());

        await WaitUntilAsync(
            catalog,
            () => catalog.GetSnapshot().Contains(capture),
            source.NotifyChanged);

        Assert.AreEqual(capture, catalog.GetSnapshot().Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConfirmedDeleteUpdatesGalleryWithoutSourceNotification(bool deleteLastCapture)
    {
        var root = Path.Combine(Path.GetTempPath(), $"screenman-catalog-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var capture = CreateCapture() with { FullPath = Path.Combine(root, "capture.png") };
            var remaining = CreateCapture() with
            {
                FullPath = Path.Combine(root, "remaining.png"),
                ModifiedAtUtc = capture.ModifiedAtUtc.AddMinutes(-1),
            };
            File.WriteAllBytes(capture.FullPath, [1, 2, 3]);
            File.WriteAllBytes(remaining.FullPath, [4, 5, 6]);
            var source = new TestCaptureSource((_, _) => deleteLastCapture ? [capture] : [capture, remaining]);
            using var catalog = new CaptureCatalog(source);
            await WaitUntilAsync(catalog, () => catalog.IsInitialized);
            var metadataStore = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            var settings = new SettingsManager(Path.Combine(root, "settings.json"));
            using var page = new CaptureManagerPage(catalog, metadataStore, settings);
            var items = page.GetItems().OfType<CaptureListItem>().ToArray();
            var item = items.Single(item => item.Title == capture.FileName);
            var remainingItem = items.SingleOrDefault(item => item.Title == remaining.FileName);
            var command = item.MoreCommands
                .OfType<CommandContextItem>()
                .Select(context => context.Command)
                .OfType<DeleteCaptureCommand>()
                .Single();
            var confirmation = Assert.IsInstanceOfType<ConfirmationArgs>(command.Invoke().Args);
            var notifications = 0;
            page.ItemsChanged += (_, _) => notifications++;
            Assert.IsTrue(File.Exists(capture.FullPath));

            Assert.IsInstanceOfType<InvokableCommand>(confirmation.PrimaryCommand).Invoke();

            Assert.IsFalse(File.Exists(capture.FullPath));
            Assert.IsGreaterThan(0, notifications);
            Assert.AreEqual(1, source.ReadCount);
            Assert.IsFalse(page.HasMoreItems);
            if (deleteLastCapture)
            {
                Assert.IsEmpty(catalog.GetSnapshot());
                Assert.IsEmpty(page.GetItems());
                Assert.IsNotNull(page.EmptyContent);
                Assert.IsFalse(catalog.TryGetLatest(out _));
            }
            else
            {
                Assert.AreEqual(remaining, catalog.GetSnapshot().Single());
                Assert.AreSame(remainingItem, page.GetItems().OfType<CaptureListItem>().Single());
                Assert.IsTrue(catalog.TryGetLatest(out var latest));
                Assert.AreEqual(remaining, latest);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task OpeningSettingUpdatesGalleryWithoutRescanningOrReplacingItems()
    {
        var root = Path.Combine(Path.GetTempPath(), $"screenman-preview-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var screenshot = CreateCapture();
            var recording = CreateCapture() with { Kind = CaptureMediaKind.Video };
            var source = new TestCaptureSource((_, _) => [screenshot, recording]);
            using var catalog = new CaptureCatalog(source);
            await WaitUntilAsync(catalog, () => catalog.IsInitialized);
            var metadataStore = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            var settingsPath = Path.Combine(root, "settings.json");
            var settings = new SettingsManager(settingsPath);
            using var page = new CaptureManagerPage(catalog, metadataStore, settings);
            var items = page.GetItems().OfType<CaptureListItem>().ToArray();
            foreach (var item in items)
            {
                Assert.IsInstanceOfType<OpenCaptureCommand>(item.Command);
            }

            var form = (SettingsForm)settings.Settings.ToContent().Single();
            foreach (var choice in new[] { "true", "false", "default", "true" })
            {
                form.SubmitForm($$"""{"jpsoftworks.screenman.Behavior.OpenInPreview":"{{choice}}"}""", string.Empty);

                CollectionAssert.AreEqual(items, page.GetItems().OfType<CaptureListItem>().ToArray());
                foreach (var item in items)
                {
                    if (choice == "true")
                    {
                        Assert.IsInstanceOfType<CapturePreviewPage>(item.Command);
                    }
                    else
                    {
                        Assert.IsInstanceOfType<OpenCaptureCommand>(item.Command);
                    }
                }
            }

            var reloadedSettings = new SettingsManager(settingsPath);
            using var reopenedPage = new CaptureManagerPage(catalog, metadataStore, reloadedSettings);
            foreach (var item in reopenedPage.GetItems().OfType<CaptureListItem>())
            {
                Assert.IsInstanceOfType<CapturePreviewPage>(item.Command);
            }

            Assert.AreEqual(1, source.ReadCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task MetadataEditsPreserveLoadedCapturesAndPublishOneCompleteRefresh()
    {
        var root = Path.Combine(Path.GetTempPath(), $"screenman-browsing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var captures = CreateCaptures(root, 95);
            using var catalog = new CaptureCatalog(new TestCaptureSource((_, _) => captures));
            await WaitUntilAsync(catalog, () => catalog.IsInitialized);
            var store = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            using var page = new CaptureManagerPage(catalog, store, new SettingsManager(Path.Combine(root, "settings.json")));
            page.LoadMore();
            var originalItems = page.GetItems().OfType<CaptureListItem>().ToArray();
            Assert.HasCount(80, originalItems);
            var observedCounts = new List<int>();
            page.ItemsChanged += (_, _) => observedCounts.Add(page.GetItems().OfType<CaptureListItem>().Count());
            var countDuringMetadataUpdate = -1;
            originalItems[^1].PropChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CaptureListItem.Title))
                {
                    countDuringMetadataUpdate = page.GetItems().OfType<CaptureListItem>().Count();
                }
            };

            store.Update(captures[79].FullPath, "Updated capture", ["work"]);
            store.ToggleFavorite(captures[79].FullPath);

            CollectionAssert.AreEqual(originalItems, page.GetItems().OfType<CaptureListItem>().ToArray());
            Assert.HasCount(2, observedCounts);
            Assert.IsTrue(observedCounts.All(count => count == 80));
            Assert.AreEqual(80, countDuringMetadataUpdate);
            Assert.AreEqual("Updated capture", originalItems[^1].Title);
            Assert.IsTrue(page.HasMoreItems);

            page.LoadMore();

            Assert.HasCount(95, page.GetItems().OfType<CaptureListItem>().ToArray());
            Assert.IsFalse(page.HasMoreItems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task NewCapturesRetainThePreviousLoadedBoundaryAndContinuePagingWithoutDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), $"screenman-browsing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var originalCaptures = CreateCaptures(root, 95);
            var snapshot = originalCaptures;
            var source = new TestCaptureSource((_, _) => snapshot);
            using var catalog = new CaptureCatalog(source);
            await WaitUntilAsync(catalog, () => catalog.IsInitialized);
            using var page = new CaptureManagerPage(
                catalog,
                new CaptureMetadataStore(Path.Combine(root, "metadata.json")),
                new SettingsManager(Path.Combine(root, "settings.json")));
            page.LoadMore();
            var originalItems = page.GetItems().OfType<CaptureListItem>().ToArray();
            var newCaptures = CreateCaptures(Path.Combine(root, "new"), 41)
                .Select(capture => capture with { ModifiedAtUtc = capture.ModifiedAtUtc.AddDays(1) }).ToArray();
            snapshot = [.. newCaptures, .. originalCaptures];

            await WaitUntilAsync(
                catalog,
                () => page.GetItems().OfType<CaptureListItem>().Count() == 121,
                source.NotifyChanged);

            var refreshedItems = page.GetItems().OfType<CaptureListItem>().ToArray();
            CollectionAssert.AreEqual(originalItems, refreshedItems.Skip(41).ToArray());
            Assert.IsTrue(page.HasMoreItems);

            page.LoadMore();

            var allItems = page.GetItems().OfType<CaptureListItem>().ToArray();
            Assert.HasCount(snapshot.Length, allItems);
            Assert.AreEqual(allItems.Length, allItems.Distinct().Count());
            CollectionAssert.AreEqual(refreshedItems, allItems.Take(refreshedItems.Length).ToArray());
            Assert.IsFalse(page.HasMoreItems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task RefreshClampsLoadedDepthAndRemovesCapturesThatNoLongerMatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"screenman-browsing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var captures = CreateCaptures(root, 95);
            var snapshot = captures;
            var source = new TestCaptureSource((_, _) => snapshot);
            using var catalog = new CaptureCatalog(source);
            await WaitUntilAsync(catalog, () => catalog.IsInitialized);
            var store = new CaptureMetadataStore(Path.Combine(root, "metadata.json"));
            using var page = new CaptureManagerPage(catalog, store, new SettingsManager(Path.Combine(root, "settings.json")));
            page.Filters!.CurrentFilterId = CaptureFilters.UnorganizedId;
            page.LoadMore();
            var originalItems = page.GetItems().OfType<CaptureListItem>().ToArray();

            store.Update(captures[79].FullPath, "Organized", []);

            var filteredItems = page.GetItems().OfType<CaptureListItem>().ToArray();
            Assert.HasCount(80, filteredItems);
            CollectionAssert.AreEqual(originalItems.Take(79).ToArray(), filteredItems.Take(79).ToArray());
            Assert.IsFalse(filteredItems.Contains(originalItems[^1]));

            snapshot = captures.Take(10).ToArray();
            await WaitUntilAsync(catalog, () => page.GetItems().OfType<CaptureListItem>().Count() == 10, source.NotifyChanged);

            CollectionAssert.AreEqual(originalItems.Take(10).ToArray(), page.GetItems().OfType<CaptureListItem>().ToArray());
            Assert.IsFalse(page.HasMoreItems);

            snapshot = [];
            await WaitUntilAsync(catalog, () => page.GetItems().Length == 0, source.NotifyChanged);

            Assert.IsNotNull(page.EmptyContent);
            Assert.IsFalse(page.HasMoreItems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task SearchAndFilterChangesResetLoadedDepth()
    {
        var root = Path.Combine(Path.GetTempPath(), $"screenman-browsing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var captures = CreateCaptures(root, 95);
            using var catalog = new CaptureCatalog(new TestCaptureSource((_, _) => captures));
            await WaitUntilAsync(catalog, () => catalog.IsInitialized);
            using var page = new CaptureManagerPage(
                catalog,
                new CaptureMetadataStore(Path.Combine(root, "metadata.json")),
                new SettingsManager(Path.Combine(root, "settings.json")));
            page.LoadMore();
            Assert.HasCount(80, page.GetItems().OfType<CaptureListItem>().ToArray());

            page.SearchText = "capture";

            Assert.HasCount(40, page.GetItems().OfType<CaptureListItem>().ToArray());
            page.LoadMore();
            Assert.HasCount(80, page.GetItems().OfType<CaptureListItem>().ToArray());

            page.Filters!.CurrentFilterId = CaptureFilters.ScreenshotsId;

            Assert.HasCount(40, page.GetItems().OfType<CaptureListItem>().ToArray());
            Assert.IsTrue(page.HasMoreItems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CaptureFile[] CreateCaptures(string root, int count)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return Enumerable.Range(0, count)
            .Select(index => new CaptureFile(
                Path.Combine(root, $"capture-{index:D3}.png"),
                timestamp.AddMinutes(-index),
                3,
                CaptureMediaKind.Image))
            .ToArray();
    }

    private static async Task WaitUntilAsync(CaptureCatalog catalog, Func<bool> condition, Action? trigger = null)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs args)
        {
            if (condition())
            {
                changed.TrySetResult();
            }
        }

        catalog.Changed += OnChanged;
        try
        {
            trigger?.Invoke();
            if (!condition())
            {
                await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            catalog.Changed -= OnChanged;
        }
    }

    private static CaptureFile CreateCapture()
    {
        return new CaptureFile(
            Path.Combine(Path.GetTempPath(), $"screenman-capture-{Guid.NewGuid():N}.png"),
            DateTimeOffset.UtcNow,
            3,
            CaptureMediaKind.Image);
    }

    private sealed class TestCaptureSource(Func<int, CancellationToken, IReadOnlyList<CaptureFile>> getCaptures) : ICaptureSource
    {
        private int _readCount;

        public event EventHandler? Changed;

        internal int ReadCount => Volatile.Read(ref this._readCount);

        public IReadOnlyList<CaptureFile> GetCaptures(CancellationToken cancellationToken)
        {
            return getCaptures(Interlocked.Increment(ref this._readCount), cancellationToken);
        }

        internal void NotifyChanged() => this.Changed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
        }
    }
}
