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
