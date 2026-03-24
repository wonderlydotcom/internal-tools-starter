open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open FsharpStarter.Application.Handlers
open FsharpStarter.Infrastructure.Database
open FsharpStarter.McpServer.Tools

let private defaultConnectionString = "Data Source=/app/data/fsharp-starter.db"

let private getConnectionString () =
    match Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") with
    | null
    | "" -> defaultConnectionString
    | value -> value

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