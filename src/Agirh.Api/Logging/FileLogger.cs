using Microsoft.Extensions.Logging;

namespace Agirh.Api.Logging;

/// <summary>
/// Logger écrivant une ligne horodatée dans le <see cref="FileLoggerProvider"/>.
/// Seuil : <see cref="LogLevel.Information"/> (identique à la console) — les logs
/// de diagnostic du pipeline IA (Profiler/Synthèse/Checker/HTTP) sont donc capturés.
/// </summary>
public sealed class FileLogger : ILogger
{
    private readonly FileLoggerProvider _provider;
    private readonly string _category;

    public FileLogger(FileLoggerProvider provider, string category)
    {
        _provider = provider;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category}: {formatter(state, exception)}";

        if (exception is not null)
            line += $" --- {exception}";

        _provider.Write(line);
    }
}
