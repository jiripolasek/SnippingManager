using System.Text.Json;
using JPSoftworks.ScreenManExtension.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class SettingsManagerTests
{
    private const string ShowDetailsAutomaticallyKey = "jpsoftworks.screenman.Layout.ShowDetailsAutomatically";
    private const string OpenInPreviewKey = "jpsoftworks.screenman.Behavior.OpenInPreview";
    private static readonly string[] OpenCaptureActionTitles =
        ["Default (Open in default app)", "Open in default app", "Preview in Command Palette"];
    private static readonly string[] OpenCaptureActionValues = ["default", "false", "true"];

    [TestMethod]
    public void OpenCaptureActionShowsDropdownWithSeparateDefaultChoice()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var manager = new SettingsManager(Path.Combine(root, "settings.json"));

            Assert.IsFalse(manager.OpenInPreview);
            Assert.AreEqual("default", manager.Settings.GetSetting<string>(OpenInPreviewKey));
            var form = (SettingsForm)manager.Settings.ToContent().Single();
            using var template = JsonDocument.Parse(form.TemplateJson);
            var input = template.RootElement.GetProperty("body").EnumerateArray()
                .Single(element => element.TryGetProperty("id", out var id) && id.GetString() == OpenInPreviewKey);
            Assert.AreEqual("Input.ChoiceSet", input.GetProperty("type").GetString());
            Assert.AreEqual("default", input.GetProperty("value").GetString());
            var choices = input.GetProperty("choices").EnumerateArray().ToArray();
            CollectionAssert.AreEqual(
                OpenCaptureActionTitles,
                choices.Select(choice => choice.GetProperty("title").GetString()).ToArray());
            CollectionAssert.AreEqual(
                OpenCaptureActionValues,
                choices.Select(choice => choice.GetProperty("value").GetString()).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("default", false)]
    [DataRow("false", false)]
    [DataRow("true", true)]
    public void OpenCaptureActionPersistsWithoutRefreshingSources(string choice, bool openInPreview)
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
            form.SubmitForm($$"""{"{{OpenInPreviewKey}}":"{{choice}}"}""", string.Empty);

            Assert.AreEqual(openInPreview, manager.OpenInPreview);
            Assert.AreEqual(0, sourceChanges);
            using var persistedSettings = JsonDocument.Parse(File.ReadAllText(settingsPath));
            Assert.AreEqual(choice, persistedSettings.RootElement.GetProperty(OpenInPreviewKey).GetString());
            var reloadedManager = new SettingsManager(settingsPath);
            Assert.AreEqual(openInPreview, reloadedManager.OpenInPreview);
            Assert.AreEqual(choice, reloadedManager.Settings.GetSetting<string>(OpenInPreviewKey));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("false", false)]
    [DataRow("true", true)]
    public void OpenCaptureActionPreservesSavedToggleChoices(string savedChoice, bool openInPreview)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            File.WriteAllText(settingsPath, $$"""{"{{OpenInPreviewKey}}":"{{savedChoice}}"}""");

            var manager = new SettingsManager(settingsPath);

            Assert.AreEqual(openInPreview, manager.OpenInPreview);
            Assert.AreEqual(savedChoice, manager.Settings.GetSetting<string>(OpenInPreviewKey));
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
