module FsharpStarter.Application.Tests.ExampleHandlerTests

open System
open System.Collections.Generic
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