using System.Text.Json;
using Traxon.Contracts;

namespace Traxon.Mcp.Storage;

public class FileMessageStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _messagesDir;
    private readonly object _lock = new();
    private int _nextSequence;

    public FileMessageStore(string workspacePath)
    {
        _messagesDir = Path.Combine(workspacePath, "messages");
        Directory.CreateDirectory(_messagesDir);
        _nextSequence = LoadNextSequence();
    }

    public Message Save(AgentRole from, AgentRole to, MessageType type, string subject, string body, string? inReplyTo = null)
    {
        lock (_lock)
        {
            var message = new Message
            {
                Id = Guid.NewGuid().ToString("N"),
                Sequence = _nextSequence++,
                Timestamp = DateTimeOffset.UtcNow,
                From = from,
                To = to,
                Type = type,
                Subject = subject,
                Body = body,
                InReplyTo = inReplyTo
            };

            var fileName = $"{message.Sequence:D6}_{message.From}_{message.Type}.json";
            var filePath = Path.Combine(_messagesDir, fileName);
            var json = JsonSerializer.Serialize(message, JsonOptions);
            File.WriteAllText(filePath, json);

            return message;
        }
    }

    public List<Message> GetMessages(AgentRole forAgent, int sinceSequence = 0)
    {
        var messages = new List<Message>();

        foreach (var file in Directory.GetFiles(_messagesDir, "*.json").Order())
        {
            var json = File.ReadAllText(file);
            var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);
            if (message is null) continue;
            if (message.Sequence <= sinceSequence) continue;
            if (message.To == forAgent)
                messages.Add(message);
        }

        return messages;
    }

    public List<Message> GetAll()
    {
        var messages = new List<Message>();

        foreach (var file in Directory.GetFiles(_messagesDir, "*.json").Order())
        {
            var json = File.ReadAllText(file);
            var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);
            if (message is not null)
                messages.Add(message);
        }

        return messages;
    }

    private int LoadNextSequence()
    {
        var files = Directory.GetFiles(_messagesDir, "*.json");
        if (files.Length == 0) return 1;

        int max = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var parts = name.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[0], out var seq))
                max = Math.Max(max, seq);
        }
        return max + 1;
    }
}
