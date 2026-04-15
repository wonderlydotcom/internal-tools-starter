module FsharpStarter.Infrastructure.Tests.ExampleMcpToolsTests

open System
open System.Collections.Generic
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

type SaveErrorExampleRepository(saveError: DomainError) =
    interface IExampleRepository with
        member _.Save(_aggregate: ExampleAggregate) = async { return Error saveError }
        member _.GetById(_id: ExampleId) = async { return Ok None }

type MissingExampleRepository() =
    interface IExampleRepository with
        member _.Save(_aggregate: ExampleAggregate) = async { return Ok() }
        member _.GetById(_id: ExampleId) = async { return Ok None }

let private createTools (repository: IExampleRepository) =
    let services = ServiceCollection()
    services.AddSingleton<IExampleRepository>(repository) |> ignore

    services.AddScoped<ExampleHandler>(fun provider ->
        ExampleHandler(
            provider.GetRequiredService<IExampleRepository>(),
            fun () -> DateTime.Parse("2026-01-01T00:00:00Z")
        ))
    |> ignore

    let serviceProvider = services.BuildServiceProvider()
    ExampleMcpTools(serviceProvider.GetRequiredService<IServiceScopeFactory>()), serviceProvider

[<Fact>]
let ``MCP tools create and fetch examples through the application layer`` () = task {
    let repository = InMemoryExampleRepository() :> IExampleRepository
    let tools, serviceProvider = createTools repository
    use _ = serviceProvider

    let! created = tools.CreateExampleAsync("MCP Example")
    let! fetched = tools.GetExampleByIdAsync(created.Id)

    Assert.Equal(created.Id, fetched.Id)
    Assert.Equal("MCP Example", fetched.Name)
    Assert.Equal(DateTime.Parse("2026-01-01T00:00:00Z"), fetched.CreatedAt)
}

[<Fact>]
let ``MCP create maps validation errors to argument exceptions`` () = task {
    let tools, serviceProvider =
        createTools (InMemoryExampleRepository() :> IExampleRepository)

    use _ = serviceProvider

    try
        let! _ = tools.CreateExampleAsync("   ")
        failwith "Expected ArgumentException but got success"
    with :? ArgumentException as ex ->
        Assert.Equal("Name is required.", ex.Message)
}

[<Fact>]
let ``MCP get maps not found errors to key not found exceptions`` () = task {
    let tools, serviceProvider =
        createTools (MissingExampleRepository() :> IExampleRepository)

    use _ = serviceProvider

    try
        let! _ = tools.GetExampleByIdAsync(Guid.NewGuid())
        failwith "Expected KeyNotFoundException but got success"
    with :? KeyNotFoundException as ex ->
        Assert.Contains("was not found", ex.Message)
}

[<Fact>]
let ``MCP create maps repository failures to invalid operation exceptions`` () = task {
    let repository =
        SaveErrorExampleRepository(PersistenceError "save failed") :> IExampleRepository

    let tools, serviceProvider = createTools repository
    use _ = serviceProvider

    try
        let! _ = tools.CreateExampleAsync("MCP Example")
        failwith "Expected InvalidOperationException but got success"
    with :? InvalidOperationException as ex ->
        Assert.Equal("save failed", ex.Message)
}