using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TorrentExtractor;

// ReSharper disable once ClassNeverInstantiated.Global
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(
                (hostContext, services) =>
                {
                    services.AddLogging(opt =>
                    {
                        var format = hostContext.Configuration.GetValue<string>(
                            "Logging:TimestampFormat"
                        );
                        opt.AddCustomFormatter(conf => conf.TimestampFormat = format);
                    });
                    services.AddHostedService<Worker>();
                    services.AddOptions();
                    services.Configure<Settings.Core>(hostContext.Configuration.GetSection("Core"));
                    services.Configure<Settings.Paths>(
                        hostContext.Configuration.GetSection("Paths")
                    );
                }
            );
}
