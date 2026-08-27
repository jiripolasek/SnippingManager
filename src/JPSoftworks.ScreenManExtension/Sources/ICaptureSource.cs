namespace JPSoftworks.ScreenManExtension.Sources;

internal interface ICaptureSource : IDisposable
{
    event EventHandler? Changed;

    IReadOnlyList<CaptureFile> GetCaptures(CancellationToken cancellationToken);
}
