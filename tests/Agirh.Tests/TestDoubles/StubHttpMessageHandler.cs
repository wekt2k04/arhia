using System.Net;
using System.Text;
using System.Text.Json;

namespace Agirh.Tests.TestDoubles;

/// <summary>
/// Transport-layer stub for <see cref="HttpMessageHandler"/>.
///
/// NO REAL NETWORK is ever used: every Ollama call performed by the services
/// under test goes through this handler, which returns deterministic canned
/// responses (or throws a configured exception).
///
/// Choice documented: a manual stub is preferred over Moq because the suite
/// currently contains zero Moq usage and the doubles stay small and readable.
/// The captured <see cref="Requests"/> allow asserting method/path/body shape.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    private readonly List<HttpRequestMessage> _requests = [];

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    /// <summary>Stub that always answers with the given raw JSON body.</summary>
    public StubHttpMessageHandler(string jsonContent, HttpStatusCode statusCode = HttpStatusCode.OK)
        : this(_ => JsonResponse(jsonContent, statusCode))
    {
    }

    /// <summary>Stub that always answers with the given status code (no body).</summary>
    public StubHttpMessageHandler(HttpStatusCode statusCode)
        : this(_ => new HttpResponseMessage(statusCode))
    {
    }

    /// <summary>Stub that always throws the given exception (simulates a network failure).</summary>
    public StubHttpMessageHandler(Exception exception)
        : this(_ => throw exception)
    {
    }

    /// <summary>All requests received by this stub, in order.</summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    /// <summary>Builds an Ollama chat-completion envelope: message.content + done flag.</summary>
    public static string OllamaPayload(string? content, bool done = true)
        => $$"""
            {"message":{"content":{{(content is null ? "null" : JsonSerializer.Serialize(content))}}},"done":{{done.ToString().ToLowerInvariant()}}}
            """;

    public static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
