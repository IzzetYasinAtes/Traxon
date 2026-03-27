namespace Traxon.Contracts;

public sealed record TaskDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public TaskStatus Status { get; init; } = TaskStatus.Pending;
    public string? BranchName { get; init; }
    public string? AssignedTo { get; init; }
    public int ReviewIteration { get; init; }
    public List<string> MessageIds { get; init; } = [];
}
