using System.ComponentModel;
using ModelContextProtocol.Server;
using Traxon.Contracts;
using Traxon.Mcp.Storage;

namespace Traxon.Mcp.Tools;

[McpServerToolType]
public class MessagingTools
{
    private readonly FileMessageStore _store;

    public MessagingTools(FileMessageStore store)
    {
        _store = store;
    }

    [McpServerTool(Name = "send_message"), Description("Send a message to another agent")]
    public string SendMessage(
        [Description("Target agent: Architect, Developer, or Commander")] string to,
        [Description("Message type: TaskPlan, Implementation, CodeReview, ReviewRevision, Approval, Question, Answer, Error")] string type,
        [Description("Short subject line")] string subject,
        [Description("Full message body in markdown")] string body,
        [Description("Agent sending: Architect, Developer, or Commander")] string from,
        [Description("Optional: ID of message being replied to")] string? inReplyTo = null)
    {
        if (!Enum.TryParse<AgentRole>(to, true, out var toRole))
            return $"Error: Invalid 'to' role '{to}'. Use Architect, Developer, or Commander.";

        if (!Enum.TryParse<AgentRole>(from, true, out var fromRole))
            return $"Error: Invalid 'from' role '{from}'. Use Architect, Developer, or Commander.";

        if (!Enum.TryParse<MessageType>(type, true, out var msgType))
            return $"Error: Invalid message type '{type}'.";

        var message = _store.Save(fromRole, toRole, msgType, subject, body, inReplyTo);
        return $"Message sent successfully. ID: {message.Id}, Sequence: {message.Sequence}";
    }

    [McpServerTool(Name = "get_messages", ReadOnly = true), Description("Get pending messages for an agent")]
    public string GetMessages(
        [Description("Agent to get messages for: Architect, Developer, or Commander")] string forAgent,
        [Description("Only return messages after this sequence number (0 for all)")] int sinceSequence = 0)
    {
        if (!Enum.TryParse<AgentRole>(forAgent, true, out var role))
            return $"Error: Invalid agent role '{forAgent}'. Use Architect, Developer, or Commander.";

        var messages = _store.GetMessages(role, sinceSequence);

        if (messages.Count == 0)
            return "No new messages.";

        var result = $"Found {messages.Count} message(s):\n\n";
        foreach (var msg in messages)
        {
            result += $"---\n**[{msg.Sequence}] {msg.Type} from {msg.From}**: {msg.Subject}\n\n{msg.Body}\n\n";
        }

        return result;
    }
}
