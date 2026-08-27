using Microsoft.Extensions.Logging.Abstractions;

namespace JPSoftworks.ScreenManExtension.Helpers;

internal static class ScreenManLog
{
    private static readonly Action<ILogger, string, Exception?> ErrorMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1, "ScreenManError"),
        "{Message}");

    private static readonly Action<ILogger, string, Exception?> WarningMessage = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "ScreenManWarning"),
        "{Message}");

    private static ILogger _logger = NullLogger.Instance;

    internal static void Initialize(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger("JPSoftworks.ScreenManExtension");
    }

    internal static void Error(string message, Exception exception)
    {
        ErrorMessage(_logger, message, exception);
    }

    internal static void Warning(string message)
    {
        WarningMessage(_logger, message, null);
    }
}
