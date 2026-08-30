namespace Arhia.Core.Ports;

public sealed record DocumentChunk(
    Guid Id,
    string DocumentSource,
    int ChunkIndex,
    string TitlePath,
    string Content,
    float Score = 0f)
{
    public static Guid ComputeId(string documentSource, int chunkIndex)
    {
        var key = $"{documentSource}#{chunkIndex}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hash = System.Security.Cryptography.MD5.HashData(bytes);
        return new Guid(hash);
    }
}
