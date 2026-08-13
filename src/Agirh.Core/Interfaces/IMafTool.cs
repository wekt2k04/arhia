using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agirh.Core.Interfaces;

[Flags]
public enum RoleFlags
{
    None = 0,
    Collaborator = 1,
    Manager = 2,
    Admin = 4,
    All = Collaborator | Manager | Admin
}

public interface IMafTool
{
    string Name { get; }
    string Description { get; }
    RoleFlags RequiredRoles { get; }
    Task<string> ExecuteAsync(JsonElement parameters, string? requestingUserId = null, CancellationToken ct = default);
}

public abstract class MafToolBase<TInput> : IMafTool where TInput : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract RoleFlags RequiredRoles { get; }

    public async Task<string> ExecuteAsync(JsonElement parameters, string? requestingUserId = null, CancellationToken ct = default)
    {
        var input = parameters.Deserialize<TInput>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        })
            ?? throw new InvalidOperationException($"Failed to deserialize {typeof(TInput).Name} from parameters.");
        return await ExecuteTypedAsync(input, requestingUserId, ct);
    }

    protected abstract Task<string> ExecuteTypedAsync(TInput input, string? requestingUserId = null, CancellationToken ct = default);
}
