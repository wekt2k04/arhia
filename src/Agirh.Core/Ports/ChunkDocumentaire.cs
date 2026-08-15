namespace Agirh.Core.Ports;

public sealed record ChunkDocumentaire(
    Guid Id,
    string DocumentSource,
    int ChunkIndex,
    string CheminTitres,
    string Contenu,
    float Score = 0f)
{
    public static Guid CalculerId(string documentSource, int chunkIndex)
    {
        var cle = $"{documentSource}#{chunkIndex}";
        var octets = System.Text.Encoding.UTF8.GetBytes(cle);
        var hash = System.Security.Cryptography.MD5.HashData(octets);
        return new Guid(hash);
    }
}
