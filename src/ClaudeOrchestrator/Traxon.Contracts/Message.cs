namespace Traxon.Contracts;

public sealed record Message
{
    public required string Id { get; init; }
    public required int Sequence { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required AgentRole From { get; init; }
    public required AgentRole To { get; init; }
    public required MessageType Type { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? InReplyTo { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
