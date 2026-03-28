using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Traxon.Mssql;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "mssql",
                Version = "1.0.0"
            };
        })
        .WithStdioServerTransport()
        .WithTools<DatabaseTools>();
    })
    .Build();

await host.RunAsync();
