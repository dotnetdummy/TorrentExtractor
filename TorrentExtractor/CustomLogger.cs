using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace TorrentExtractor;

public static class ConsoleLoggerExtensions
{
    public static ILoggingBuilder AddCustomFormatter(this ILoggingBuilder builder) =>
        builder
            .AddConsole(options => options.FormatterName = nameof(CustomLoggingFormatter))
            .AddConsoleFormatter<CustomLoggingFormatter, ConsoleFormatterOptions>();

    public static ILoggingBuilder AddCustomFormatter(
        this ILoggingBuilder builder,
        Action<ConsoleFormatterOptions> configure
    ) =>
        builder
            .AddConsole(options => options.FormatterName = nameof(CustomLoggingFormatter))
            .AddConsoleFormatter<CustomLoggingFormatter, ConsoleFormatterOptions>(configure);
}

public sealed class CustomLoggingFormatter : ConsoleFormatter, IDisposable
{
    private readonly IDisposable _optionsReloadToken;
    private ConsoleFormatterOptions _formatterOptions;

    public CustomLoggingFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
        : base(nameof(CustomLoggingFormatter)) =>
        (_optionsReloadToken, _formatterOptions) = (
            options.OnChange(ReloadLoggerOptions),
            options.CurrentValue
        );

    private void ReloadLoggerOptions(ConsoleFormatterOptions options) =>
        _formatterOptions = options;

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider scopeProvider,
        TextWriter textWriter
    )
    {
        var message = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception);

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var tsFormat = _formatterOptions.TimestampFormat ?? "[yyyy-MM-dd HH:mm:ss.ffffffzzzz]";

        textWriter.WriteLine(
            $"{(tsFormat == "" ? "" : DateTime.UtcNow.ToString(tsFormat))} {GetLogLevelString(logEntry.LogLevel)}: {message}{
                (logEntry.Exception != null ? $". {logEntry.Exception?.GetType()}: {logEntry.Exception?.Message}" : "")}"
                .Replace("..", ".")
                .Trim()
        );
    }

    public void Dispose() => _optionsReloadToken?.Dispose();

    private string GetLogLevelString(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            LogLevel.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
}
