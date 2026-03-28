using System.Text.Json;
using System.Text.Json.Serialization;
using Traxon.Contracts;

namespace Traxon.Mcp.Storage;

public class CommandStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    private readonly string _commandsDir;
    private readonly string _processedDir;

    public CommandStore(string workspacePath)
    {
        _commandsDir = Path.Combine(workspacePath, "commands");
        _processedDir = Path.Combine(_commandsDir, "processed");
        Directory.CreateDirectory(_commandsDir);
        Directory.CreateDirectory(_processedDir);
    }

    public List<Command> GetPending()
    {
        var commands = new List<Command>();

        foreach (var file in Directory.GetFiles(_commandsDir, "*.json").Order())
        {
            try
            {
                var json = File.ReadAllText(file);
                var command = JsonSerializer.Deserialize<Command>(json, JsonOptions);
                if (command is not null && !command.Processed)
                    commands.Add(command);
            }
            catch (JsonException)
            {
                // Skip corrupted command files
            }
        }

        return commands;
    }

    public void Acknowledge(string commandId)
    {
        var sourceFile = Directory.GetFiles(_commandsDir, "*.json")
            .FirstOrDefault(f =>
            {
                var json = File.ReadAllText(f);
                var cmd = JsonSerializer.Deserialize<Command>(json, JsonOptions);
                return cmd?.Id == commandId;
            });

        if (sourceFile is null) return;

        var destFile = Path.Combine(_processedDir, Path.GetFileName(sourceFile));
        File.Move(sourceFile, destFile, overwrite: true);
    }
}
