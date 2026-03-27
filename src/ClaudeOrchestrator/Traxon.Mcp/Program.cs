using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Traxon.Mcp.Storage;
using Traxon.Mcp.Tools;

var workspacePath = Environment.GetEnvironmentVariable("TRAXON_WORKSPACE")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "workspace");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(new FileMessageStore(workspacePath));
        services.AddSingleton(new TaskStore(workspacePath));
        services.AddSingleton(new CommandStore(workspacePath));

        services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "traxon",
                Version = "1.0.0"
            };
        })
        .WithStdioServerTransport()
        .WithTools<MessagingTools>()
        .WithTools<TaskTools>()
        .WithTools<ContextTools>();
    })
    .Build();

await host.RunAsync();
