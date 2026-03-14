module FsharpStarter.Infrastructure.Tests.ExamplesControllerTests

open System
open System.Diagnostics
open Microsoft.AspNetCore.Mvc
open FsharpStarter.Api.Controllers
open FsharpStarter.Application.DTOs
open FsharpStarter.Application.Handlers
open FsharpStarter.Domain.Ports
open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.ValueObjects
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
let ``POST returns CreatedAtAction and GET returns Ok`` () =
    let repository = InMemoryExampleRepository() :> IExampleRepository

    let exampleHandler =
        ExampleHandler(repository, fun () -> DateTime.Parse("2026-01-01T00:00:00Z"))

    use activitySource = new ActivitySource("Tests")
    let controller = ExamplesController(exampleHandler, activitySource)

    let createdResult = controller.CreateAsync({ Name = "Controller Example" }).Result

    let createdAt =
        match createdResult with
        | :? CreatedAtActionResult as result -> result
        | other -> failwithf "Expected CreatedAtActionResult but got %s" (other.GetType().Name)

    let responseDto =
        match createdAt.Value with
        | :? ExampleResponseDto as response -> response
        | _ -> failwith "Expected ExampleResponseDto payload"

    let getResult = controller.GetByIdAsync(responseDto.Id).Result

    match getResult with
    | :? OkObjectResult -> ()
    | other -> failwithf "Expected OkObjectResult but got %s" (other.GetType().Name)