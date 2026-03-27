using System.ComponentModel;
using ModelContextProtocol.Server;
using Traxon.Mcp.Storage;

namespace Traxon.Mcp.Tools;

[McpServerToolType]
public class ContextTools
{
    private readonly TaskStore _taskStore;
    private readonly FileMessageStore _messageStore;
    private readonly CommandStore _commandStore;

    public ContextTools(TaskStore taskStore, FileMessageStore messageStore, CommandStore commandStore)
    {
        _taskStore = taskStore;
        _messageStore = messageStore;
        _commandStore = commandStore;
    }

    [McpServerTool(Name = "get_project_context", ReadOnly = true), Description("Get current project context: active tasks, recent messages, and pending commands")]
    public string GetProjectContext()
    {
        var tasks = _taskStore.GetAll();
        var activeTasks = tasks.Where(t => t.Status is not (Contracts.TaskStatus.Completed or Contracts.TaskStatus.Failed)).ToList();
        var messages = _messageStore.GetAll();
        var recentMessages = messages.TakeLast(5).ToList();
        var pendingCommands = _commandStore.GetPending();

        var result = "# Project Context\n\n";

        if (pendingCommands.Count > 0)
        {
            result += "## PENDING USER COMMANDS (handle these first!)\n\n";
            foreach (var cmd in pendingCommands)
            {
                result += $"- **[{cmd.Id}]** To {cmd.To}: {cmd.Content}\n";
            }
            result += "\n";
        }

        result += $"## Active Tasks ({activeTasks.Count})\n\n";
        foreach (var task in activeTasks)
        {
            result += $"- **{task.Id}**: {task.Title} [{task.Status}] (Branch: {task.BranchName ?? "none"})\n";
        }

        result += $"\n## Recent Messages ({recentMessages.Count})\n\n";
        foreach (var msg in recentMessages)
        {
            result += $"- [{msg.Sequence}] {msg.From} → {msg.To}: {msg.Subject} ({msg.Type})\n";
        }

        return result;
    }

    [McpServerTool(Name = "get_commands", ReadOnly = true), Description("Get pending user commands")]
    public string GetCommands()
    {
        var commands = _commandStore.GetPending();

        if (commands.Count == 0)
            return "No pending commands.";

        var result = $"Found {commands.Count} pending command(s):\n\n";
        foreach (var cmd in commands)
        {
            result += $"- **[{cmd.Id}]** To: {cmd.To} | Command: {cmd.Content}\n";
        }

        return result;
    }

    [McpServerTool(Name = "acknowledge_command"), Description("Mark a user command as processed")]
    public string AcknowledgeCommand([Description("Command ID to acknowledge")] string id)
    {
        _commandStore.Acknowledge(id);
        return $"Command '{id}' acknowledged and moved to processed.";
    }
}
