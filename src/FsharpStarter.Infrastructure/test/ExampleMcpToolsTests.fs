module FsharpStarter.Infrastructure.Tests.ExampleMcpToolsTests

open System
open Microsoft.Extensions.DependencyInjection
open FsharpStarter.Application.Handlers
open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.Ports
open FsharpStarter.Domain.ValueObjects
open FsharpStarter.McpServer.Tools
open Xunit

type InMemoryExampleRepository() =
    let mutable aggregateById: Map<Guid, ExampleAggregate> = Map.empty

    interface IExampleRepository with
        member _.Save(aggregate: ExampleAggregate) = async {
            match aggregate.State with
            | None -> return Error(PersistenceError "State missing")
            | Some state ->
                aggregateById <- aggregateById.Add(ExampleId.value state.Id, aggregate)
                return Ok()
        }

        member _.GetById(id: ExampleId) = async {
            match aggregateById.TryFind(ExampleId.value id) with
            | Some aggregate -> return Ok(Some aggregate)
            | None -> return Ok None
        }

[<Fact>]
let ``MCP tools create and fetch examples through the application layer`` () = task {
    let repository = InMemoryExampleRepository() :> IExampleRepository

    let services = ServiceCollection()
    services.AddSingleton<IExampleRepository>(repository) |> ignore

    services.AddScoped<ExampleHandler>(fun provider ->
        ExampleHandler(
            provider.GetRequiredService<IExampleRepository>(),
            fun () -> DateTime.Parse("2026-01-01T00:00:00Z")
        ))
    |> ignore

    use serviceProvider = services.BuildServiceProvider()

    let tools =
        ExampleMcpTools(serviceProvider.GetRequiredService<IServiceScopeFactory>())

    let! created = tools.CreateExampleAsync("MCP Example")
    let! fetched = tools.GetExampleByIdAsync(created.Id)

    Assert.Equal(created.Id, fetched.Id)
    Assert.Equal("MCP Example", fetched.Name)
    Assert.Equal(DateTime.Parse("2026-01-01T00:00:00Z"), fetched.CreatedAt)
}