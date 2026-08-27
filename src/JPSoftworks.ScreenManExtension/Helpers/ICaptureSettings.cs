namespace JPSoftworks.ScreenManExtension.Helpers;

internal interface ICaptureSettings
{
    event EventHandler? SourcesChanged;

    IReadOnlyList<string> FolderPaths { get; }

    bool IncludeSubfolders { get; }
}
