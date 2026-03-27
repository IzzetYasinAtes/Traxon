using System.ComponentModel;
using ModelContextProtocol.Server;
using Traxon.Contracts;
using Traxon.Mcp.Storage;
using TaskStatus = Traxon.Contracts.TaskStatus;

namespace Traxon.Mcp.Tools;

[McpServerToolType]
public class TaskTools
{
    private readonly TaskStore _store;

    public TaskTools(TaskStore store)
    {
        _store = store;
    }

    [McpServerTool(Name = "get_task", ReadOnly = true), Description("Get task details by ID")]
    public string GetTask([Description("Task ID")] string taskId)
    {
        var task = _store.Get(taskId);
        if (task is null)
            return $"Error: Task '{taskId}' not found.";

        return $"**Task: {task.Title}**\n" +
               $"- ID: {task.Id}\n" +
               $"- Status: {task.Status}\n" +
               $"- Branch: {task.BranchName ?? "none"}\n" +
               $"- Assigned: {task.AssignedTo ?? "unassigned"}\n" +
               $"- Review iteration: {task.ReviewIteration}\n" +
               $"- Description: {task.Description}";
    }

    [McpServerTool(Name = "update_task_status"), Description("Update the status of a task")]
    public string UpdateTaskStatus(
        [Description("Task ID")] string taskId,
        [Description("New status: Pending, InProgress, UnderReview, RevisionNeeded, Approved, Merging, Completed, Failed")] string status,
        [Description("Optional summary of what was done")] string? summary = null)
    {
        if (!Enum.TryParse<TaskStatus>(status, true, out var taskStatus))
            return $"Error: Invalid status '{status}'.";

        var task = _store.UpdateStatus(taskId, taskStatus, summary);
        if (task is null)
            return $"Error: Task '{taskId}' not found.";

        return $"Task '{taskId}' status updated to {taskStatus}.";
    }

    [McpServerTool(Name = "list_tasks", ReadOnly = true), Description("List all tasks")]
    public string ListTasks()
    {
        var tasks = _store.GetAll();
        if (tasks.Count == 0)
            return "No tasks found.";

        var result = $"Found {tasks.Count} task(s):\n\n";
        foreach (var task in tasks)
        {
            result += $"- **{task.Id}**: {task.Title} [{task.Status}]\n";
        }

        return result;
    }
}
