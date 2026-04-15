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

type SaveErrorExampleRepository(saveError: DomainError) =
    interface IExampleRepository with
        member _.Save(_aggregate: ExampleAggregate) = async { return Error saveError }
        member _.GetById(_id: ExampleId) = async { return Ok None }

type LoadErrorExampleRepository(loadError: DomainError) =
    interface IExampleRepository with
        member _.Save(_aggregate: ExampleAggregate) = async { return Ok() }
        member _.GetById(_id: ExampleId) = async { return Error loadError }

let private createController (repository: IExampleRepository) =
    let exampleHandler =
        ExampleHandler(repository, fun () -> DateTime.Parse("2026-01-01T00:00:00Z"))

    let activitySource = new ActivitySource("Tests")
    ExamplesController(exampleHandler, activitySource), activitySource

[<Fact>]
let ``POST returns CreatedAtAction and GET returns Ok`` () =
    let repository = InMemoryExampleRepository() :> IExampleRepository
    let controller, activitySource = createController repository
    use _ = activitySource

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

[<Fact>]
let ``POST returns BadRequest for validation errors`` () =
    let controller, activitySource =
        createController (InMemoryExampleRepository() :> IExampleRepository)

    use _ = activitySource

    let result = controller.CreateAsync({ Name = "   " }).Result

    let badRequest =
        match result with
        | :? BadRequestObjectResult as value -> value
        | other -> failwithf "Expected BadRequestObjectResult but got %s" (other.GetType().Name)

    let details =
        match badRequest.Value with
        | :? ProblemDetails as value -> value
        | _ -> failwith "Expected ProblemDetails payload"

    Assert.Equal("Validation Error", details.Title)

[<Fact>]
let ``POST returns Conflict when the handler reports a conflict`` () =
    let repository =
        SaveErrorExampleRepository(Conflict "already exists") :> IExampleRepository

    let controller, activitySource = createController repository
    use _ = activitySource

    let result = controller.CreateAsync({ Name = "Controller Example" }).Result

    let conflict =
        match result with
        | :? ConflictObjectResult as value -> value
        | other -> failwithf "Expected ConflictObjectResult but got %s" (other.GetType().Name)

    let details =
        match conflict.Value with
        | :? ProblemDetails as value -> value
        | _ -> failwith "Expected ProblemDetails payload"

    Assert.Equal("Conflict", details.Title)

[<Fact>]
let ``GET returns NotFound when the example does not exist`` () =
    let controller, activitySource =
        createController (InMemoryExampleRepository() :> IExampleRepository)

    use _ = activitySource

    let result = controller.GetByIdAsync(Guid.NewGuid()).Result

    let notFound =
        match result with
        | :? NotFoundObjectResult as value -> value
        | other -> failwithf "Expected NotFoundObjectResult but got %s" (other.GetType().Name)

    let details =
        match notFound.Value with
        | :? ProblemDetails as value -> value
        | _ -> failwith "Expected ProblemDetails payload"

    Assert.Equal("Not Found", details.Title)

[<Fact>]
let ``GET returns 500 when the handler reports persistence errors`` () =
    let repository =
        LoadErrorExampleRepository(PersistenceError "database offline") :> IExampleRepository

    let controller, activitySource = createController repository
    use _ = activitySource

    let result = controller.GetByIdAsync(Guid.NewGuid()).Result

    let serverError =
        match result with
        | :? ObjectResult as value when value.StatusCode = Nullable 500 -> value
        | other -> failwithf "Expected 500 ObjectResult but got %s" (other.GetType().Name)

    let details =
        match serverError.Value with
        | :? ProblemDetails as value -> value
        | _ -> failwith "Expected ProblemDetails payload"

    Assert.Equal("Persistence Error", details.Title)