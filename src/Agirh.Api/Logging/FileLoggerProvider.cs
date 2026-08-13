using Microsoft.Extensions.Logging;

namespace Agirh.Api.Logging;

/// <summary>
/// Fournisseur de logs écrivant dans un fichier, RÉINITIALISÉ (tronqué) à chaque
/// démarrage de l'API : <c>FileMode.Create</c> ouvre le fichier en écrasement au
/// premier writer → un run de l'API produit UN fichier frais contenant tous ses logs.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();

    public FileLoggerProvider(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void Write(string line)
    {
        lock (_sync)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Dispose();
        }
    }
}
