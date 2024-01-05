using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TorrentExtractor;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(opt =>
                {
                    opt.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                    });
                });
                services.AddHostedService<Worker>();
                services.AddOptions();
                services.Configure<Settings.General>(hostContext.Configuration.GetSection("General"));
                services.Configure<Settings.Paths>(hostContext.Configuration.GetSection("Paths"));
            });
}