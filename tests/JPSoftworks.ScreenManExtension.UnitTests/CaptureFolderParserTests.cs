using JPSoftworks.ScreenManExtension.Helpers;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class CaptureFolderParserTests
{
    [TestMethod]
    public void DefaultsIncludeSnippingToolScreenshotAndRecordingFolders()
    {
        var defaults = CaptureFolderParser.GetDefaultFolders();

        CollectionAssert.Contains(
            defaults.ToArray(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"));
        CollectionAssert.Contains(
            defaults.ToArray(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Screen Recordings"));
    }

    [TestMethod]
    public void ParseExpandsEnvironmentVariablesAndDeduplicatesPaths()
    {
        var temporaryPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var input = $"%TEMP%{Environment.NewLine}\"{temporaryPath}\\\"{Environment.NewLine}# ignored{Environment.NewLine}relative";

        var result = CaptureFolderParser.Parse(input);

        Assert.HasCount(1, result);
        Assert.AreEqual(temporaryPath, result[0], ignoreCase: true);
    }
}
