using System.Text.Json;
using System.Text.Json.Serialization;
using Traxon.Contracts;
using TaskStatus = Traxon.Contracts.TaskStatus;

namespace Traxon.Mcp.Storage;

public class TaskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    private readonly string _tasksDir;

    public TaskStore(string workspacePath)
    {
        _tasksDir = Path.Combine(workspacePath, "tasks");
        Directory.CreateDirectory(_tasksDir);
    }

    public TaskDefinition? Get(string taskId)
    {
        var filePath = Path.Combine(_tasksDir, $"{taskId}.json");
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<TaskDefinition>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public TaskDefinition Save(TaskDefinition task)
    {
        var filePath = Path.Combine(_tasksDir, $"{task.Id}.json");
        var json = JsonSerializer.Serialize(task, JsonOptions);
        File.WriteAllText(filePath, json);
        return task;
    }

    public TaskDefinition? UpdateStatus(string taskId, TaskStatus status, string? summary = null)
    {
        var task = Get(taskId);
        if (task is null) return null;

        task = task with { Status = status };
        return Save(task);
    }

    public List<TaskDefinition> GetAll()
    {
        var tasks = new List<TaskDefinition>();

        foreach (var file in Directory.GetFiles(_tasksDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var task = JsonSerializer.Deserialize<TaskDefinition>(json, JsonOptions);
                if (task is not null)
                    tasks.Add(task);
            }
            catch (JsonException)
            {
                // Skip corrupted task files
            }
        }

        return tasks;
    }
}
