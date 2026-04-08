open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open System.IO
open FsharpStarter.Application.Handlers
open FsharpStarter.Infrastructure.Database
open FsharpStarter.McpServer.Tools

let private getConnectionString () =
    SqliteConnectionStrings.resolveConnectionString
        (SqliteConnectionStrings.isRunningInContainer ())
        (Directory.GetCurrentDirectory())
        "fsharp-starter.db"
        (Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") |> Option.ofObj)

[<EntryPoint>]
let main _ =
    // STDIO MCP servers must avoid default host console logging.
    let builder = Host.CreateEmptyApplicationBuilder(settings = null)

    let connectionString = getConnectionString ()
    Persistence.upgradeDatabaseQuietly connectionString

    Persistence.addInfrastructure builder.Services connectionString |> ignore
    builder.Services.AddScoped<ExampleHandler>() |> ignore

    builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<ExampleMcpTools>()
    |> ignore

    use app = builder.Build()
    app.Run()
    0
