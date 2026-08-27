using System.Text.Json;
using JPSoftworks.ScreenManExtension.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class SettingsManagerTests
{
    private const string ShowDetailsAutomaticallyKey = "jpsoftworks.screenman.Layout.ShowDetailsAutomatically";
    private const string OpenInPreviewKey = "jpsoftworks.screenman.Behavior.OpenInPreview";

    [TestMethod]
    public void OpenInPreviewDefaultsToDisabled()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var manager = new SettingsManager(Path.Combine(root, "settings.json"));

            Assert.IsFalse(manager.OpenInPreview);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void OpenInPreviewPersistsWithoutRefreshingSources()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var manager = new SettingsManager(settingsPath);
            var sourceChanges = 0;
            manager.SourcesChanged += (_, _) => sourceChanges++;
            var form = (SettingsForm)manager.Settings.ToContent().Single();

            form.SubmitForm($$"""{"{{OpenInPreviewKey}}":"true"}""", string.Empty);

            Assert.IsTrue(manager.OpenInPreview);
            Assert.AreEqual(0, sourceChanges);
            var reloadedManager = new SettingsManager(settingsPath);
            Assert.IsTrue(reloadedManager.OpenInPreview);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ShowDetailsAutomaticallyDefaultsToEnabled()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var manager = new SettingsManager(Path.Combine(root, "settings.json"));

            Assert.IsTrue(manager.ShowDetailsAutomatically);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ShowDetailsAutomaticallyCanBeDisabledWithoutRefreshingSources()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var manager = new SettingsManager(settingsPath);
            var sourceChanges = 0;
            manager.SourcesChanged += (_, _) => sourceChanges++;
            var form = (SettingsForm)manager.Settings.ToContent().Single();

            form.SubmitForm($$"""{"{{ShowDetailsAutomaticallyKey}}":"false"}""", string.Empty);

            Assert.IsFalse(manager.ShowDetailsAutomatically);
            Assert.AreEqual(0, sourceChanges);
            using var persistedSettings = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.AreEqual(
                "false",
                persistedSettings.RootElement.GetProperty(ShowDetailsAutomaticallyKey).GetString());

            var reloadedManager = new SettingsManager(settingsPath);
            Assert.IsFalse(reloadedManager.ShowDetailsAutomatically);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"screenman-settings-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
