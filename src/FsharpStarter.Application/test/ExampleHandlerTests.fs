module FsharpStarter.Application.Tests.ExampleHandlerTests

open System
open System.Collections.Generic
open System.Runtime.Serialization
open FsharpStarter.Application.Commands
open FsharpStarter.Application.Handlers
open FsharpStarter.Domain.Ports
open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.ValueObjects
open Xunit

type InMemoryExampleRepository() =
    let store = Dictionary<Guid, ExampleAggregate>()

    interface IExampleRepository with
        member _.Save(aggregate: ExampleAggregate) = async {
            match aggregate.State with
            | None -> return Error(PersistenceError "State missing")
            | Some state ->
                store[ExampleId.value state.Id] <- aggregate
                return Ok()
        }

        member _.GetById(id: ExampleId) = async {
            match store.TryGetValue(ExampleId.value id) with
            | true, aggregate -> return Ok(Some aggregate)
            | false, _ -> return Ok None
        }

type SaveErrorExampleRepository(saveError: DomainError) =
    interface IExampleRepository with
        member _.Save(_aggregate: ExampleAggregate) = async { return Error saveError }
        member _.GetById(_id: ExampleId) = async { return Ok None }

type LoadResultExampleRepository(result: Result<ExampleAggregate option, DomainError>) =
    interface IExampleRepository with
        member _.Save(_aggregate: ExampleAggregate) = async { return Ok() }
        member _.GetById(_id: ExampleId) = async { return result }

let private missingStateAggregate () =
    FormatterServices.GetUninitializedObject(typeof<ExampleAggregate>) :?> ExampleAggregate

[<Fact>]
let ``Create then get returns canonical response`` () =
    let repository = InMemoryExampleRepository() :> IExampleRepository
    let fixedClock = fun () -> DateTime.Parse("2026-01-01T00:00:00Z")
    let handler = ExampleHandler(repository, fixedClock)

    let created = handler.CreateAsync({ Name = "Example" }) |> Async.RunSynchronously

    let createdResponse =
        match created with
        | Error error -> failwithf "Expected creation success but got %A" error
        | Ok response -> response

    let loaded = handler.GetByIdAsync(createdResponse.Id) |> Async.RunSynchronously

    match loaded with
    | Error error -> failwithf "Expected load success but got %A" error
    | Ok response ->
        Assert.Equal(createdResponse.Id, response.Id)
        Assert.Equal("Example", response.Name)
        Assert.Equal(1, response.Version)

[<Fact>]
let ``Create with blank name returns validation error`` () =
    let repository = InMemoryExampleRepository() :> IExampleRepository
    let handler = ExampleHandler(repository)

    let result = handler.CreateAsync({ Name = "  " }) |> Async.RunSynchronously

    match result with
    | Error(ValidationError _) -> ()
    | Ok _ -> failwith "Expected validation error but got success"
    | Error other -> failwithf "Expected ValidationError but got %A" other

[<Fact>]
let ``Create propagates repository save errors`` () =
    let repository =
        SaveErrorExampleRepository(PersistenceError "write failed") :> IExampleRepository

    let handler =
        ExampleHandler(repository, fun () -> DateTime.Parse("2026-01-01T00:00:00Z"))

    let result = handler.CreateAsync({ Name = "Example" }) |> Async.RunSynchronously

    match result with
    | Ok _ -> failwith "Expected persistence error but got success"
    | Error(PersistenceError message) -> Assert.Equal("write failed", message)
    | Error other -> failwithf "Expected PersistenceError but got %A" other

[<Fact>]
let ``GetById returns not found when the repository has no aggregate`` () =
    let repository = InMemoryExampleRepository() :> IExampleRepository
    let handler = ExampleHandler(repository)

    let result = handler.GetByIdAsync(Guid.NewGuid()) |> Async.RunSynchronously

    match result with
    | Ok _ -> failwith "Expected not found error but got success"
    | Error(NotFound message) -> Assert.Contains("was not found", message)
    | Error other -> failwithf "Expected NotFound but got %A" other

[<Fact>]
let ``GetById propagates repository load errors`` () =
    let repository =
        LoadResultExampleRepository(Error(PersistenceError "load failed")) :> IExampleRepository

    let handler = ExampleHandler(repository)
    let result = handler.GetByIdAsync(Guid.NewGuid()) |> Async.RunSynchronously

    match result with
    | Ok _ -> failwith "Expected persistence error but got success"
    | Error(PersistenceError message) -> Assert.Equal("load failed", message)
    | Error other -> failwithf "Expected PersistenceError but got %A" other

[<Fact>]
let ``GetById rejects aggregates with missing state`` () =
    let repository =
        LoadResultExampleRepository(Ok(Some(missingStateAggregate ()))) :> IExampleRepository

    let handler = ExampleHandler(repository)
    let result = handler.GetByIdAsync(Guid.NewGuid()) |> Async.RunSynchronously

    match result with
    | Ok _ -> failwith "Expected persistence error but got success"
    | Error(PersistenceError message) -> Assert.Equal("Aggregate state was missing after load.", message)
    | Error other -> failwithf "Expected PersistenceError but got %A" other