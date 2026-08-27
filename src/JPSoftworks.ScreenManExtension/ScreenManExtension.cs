using System.Runtime.InteropServices;

namespace JPSoftworks.ScreenManExtension;

[Guid("90f0314f-b90c-4092-8bef-e5f34b9d0dd2")]
public sealed partial class ScreenManExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly ScreenManCommandsProvider _provider;
    private bool _isDisposed;

    public ScreenManExtension(ManualResetEvent extensionDisposedEvent)
        : this(extensionDisposedEvent, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
    {
    }

    internal ScreenManExtension(ManualResetEvent extensionDisposedEvent, ILoggerFactory loggerFactory)
    {
        this._extensionDisposedEvent = extensionDisposedEvent ?? throw new ArgumentNullException(nameof(extensionDisposedEvent));
        this._provider = new(loggerFactory);
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType == ProviderType.Commands ? this._provider : null;
    }

    public void Dispose()
    {
        if (this._isDisposed)
        {
            return;
        }

        this._isDisposed = true;
        this._provider.Dispose();
        this._extensionDisposedEvent.Set();
    }
}
