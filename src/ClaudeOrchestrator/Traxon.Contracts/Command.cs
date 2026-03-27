namespace Traxon.Contracts;

public sealed record Command
{
    public required string Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required AgentRole To { get; init; }
    public required string Content { get; init; }
    public bool Processed { get; init; }
}
