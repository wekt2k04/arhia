namespace Agirh.Core.Interfaces;

public readonly record struct ExecutionInput
{
    public required ToolDispatch Dispatch { get; init; }
    public required HardState HardState { get; init; }
    public required DynamicContextVector CognitiveContext { get; init; }
}

public interface IWorkerExecutor
{
    IAsyncEnumerable<string> ExecuteAsync(ExecutionInput input, CancellationToken ct = default);
}
