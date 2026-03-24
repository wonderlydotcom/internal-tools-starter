namespace FsharpStarter.McpServer.Tools

open System
open System.Collections.Generic
open System.ComponentModel
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open ModelContextProtocol.Server
open FsharpStarter.Application.DTOs
open FsharpStarter.Application.Handlers
open FsharpStarter.Domain

type ExampleMcpTools(scopeFactory: IServiceScopeFactory) =

    member private _.MapResult(result: Result<ExampleResponseDto, DomainError>) =
        match result with
        | Ok response -> response
        | Error(ValidationError message) -> raise (ArgumentException(message))
        | Error(NotFound message) -> raise (KeyNotFoundException(message))
        | Error(Conflict message) -> raise (InvalidOperationException(message))
        | Error(PersistenceError message) -> raise (InvalidOperationException(message))

    [<McpServerTool;
      Description("Creates a new example via the starter application's application layer. Replace this with your domain-specific write tools in copied repos.")>]
    member this.CreateExampleAsync
        ([<Description("The name to assign to the new example.")>] name: string)
        : Task<ExampleResponseDto> =
        task {
            use scope = scopeFactory.CreateScope()
            let exampleHandler = scope.ServiceProvider.GetRequiredService<ExampleHandler>()
            let! result = exampleHandler.CreateAsync({ Name = name }) |> Async.StartAsTask
            return this.MapResult(result)
        }

    [<McpServerTool;
      Description("Loads an example by id via the starter application's application layer. Replace this with your domain-specific read tools in copied repos.")>]
    member this.GetExampleByIdAsync
        ([<Description("The GUID of the example to load.")>] id: Guid)
        : Task<ExampleResponseDto> =
        task {
            use scope = scopeFactory.CreateScope()
            let exampleHandler = scope.ServiceProvider.GetRequiredService<ExampleHandler>()
            let! result = exampleHandler.GetByIdAsync(id) |> Async.StartAsTask
            return this.MapResult(result)
        }