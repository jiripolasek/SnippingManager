using JPSoftworks.CommandPalette.Extensions.Toolkit;
using JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

namespace JPSoftworks.ScreenManExtension;

public static class Program
{
    [MTAThread]
    public static async Task Main(string[] args)
    {
        var host = ExtensionHostConfiguration.Resolve(
            args,
            new ExtensionHostRunnerParameters
            {
                PublisherMoniker = ExtensionHostIdentity.PublisherMoniker,
                ProductMoniker = ExtensionHostIdentity.ProductMoniker,
            });

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddDailyFile(host)
                .AddCommandPalette(host));

        await ExtensionHostRunner.CreateBuilder(host)
            .AddHostedExtensionFactory(context => new ScreenManExtension(context.ExtensionDisposedEvent, loggerFactory))
            .UseMicrosoftExtensionsLogging(loggerFactory)
            .RunAsync();
    }
}
