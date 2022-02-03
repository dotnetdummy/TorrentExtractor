using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace TorrentExtractor
{
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
                        opt.AddConsole().AddConsoleFormatter<CustomConsoleFormatter, ConsoleFormatterOptions>(
                            c => { c.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] "; });
                    });
                    services.AddHostedService<Worker>();
                    services.AddOptions();
                    services.Configure<Settings.General>(hostContext.Configuration.GetSection("General"));
                    services.Configure<Settings.Paths>(hostContext.Configuration.GetSection("Paths"));
                });
    }

    public class CustomConsoleFormatter : ConsoleFormatter
    {
        public CustomConsoleFormatter(string name) : base(name)
        {
        }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            if (logEntry.Exception == null)
            {
                return;
            }

            var message =
                logEntry.Formatter?.Invoke(
                    logEntry.State, logEntry.Exception);

            if (message == null)
            {
                return;
            }

            textWriter.WriteLine(message);
        }
    }
}