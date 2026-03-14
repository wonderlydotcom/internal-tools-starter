namespace FsharpStarter.Api.Controllers

open System
open System.Diagnostics
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open FsharpStarter.Application.Commands
open FsharpStarter.Application.DTOs
open FsharpStarter.Application.Handlers
open FsharpStarter.Domain

[<ApiController>]
[<Route("api/examples")>]
type ExamplesController(exampleHandler: ExampleHandler, activitySource: ActivitySource) =
    inherit ControllerBase()

    [<HttpGet("{id:guid}")>]
    [<ProducesResponseType(typeof<ExampleResponseDto>, StatusCodes.Status200OK)>]
    [<ProducesResponseType(typeof<ProblemDetails>, StatusCodes.Status404NotFound)>]
    [<ProducesResponseType(typeof<ProblemDetails>, StatusCodes.Status500InternalServerError)>]
    member this.GetByIdAsync(id: Guid) = task {
        use _activity = activitySource.StartActivity("examples.get")
        let! result = exampleHandler.GetByIdAsync(id) |> Async.StartAsTask

        return
            match result with
            | Ok response -> this.Ok(response) :> IActionResult
            | Error(NotFound message) ->
                this.NotFound(ProblemDetails(Title = "Not Found", Detail = message)) :> IActionResult
            | Error(ValidationError message) ->
                this.BadRequest(ProblemDetails(Title = "Validation Error", Detail = message)) :> IActionResult
            | Error(Conflict message) ->
                this.Conflict(ProblemDetails(Title = "Conflict", Detail = message)) :> IActionResult
            | Error(PersistenceError message) ->
                this.StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ProblemDetails(Title = "Persistence Error", Detail = message)
                )
                :> IActionResult
    }

    [<HttpPost>]
    [<ProducesResponseType(typeof<ExampleResponseDto>, StatusCodes.Status201Created)>]
    [<ProducesResponseType(typeof<ProblemDetails>, StatusCodes.Status400BadRequest)>]
    [<ProducesResponseType(typeof<ProblemDetails>, StatusCodes.Status409Conflict)>]
    [<ProducesResponseType(typeof<ProblemDetails>, StatusCodes.Status500InternalServerError)>]
    member this.CreateAsync([<FromBody>] request: CreateExampleRequestDto) = task {
        use _activity = activitySource.StartActivity("examples.create")
        let! result = exampleHandler.CreateAsync({ Name = request.Name }) |> Async.StartAsTask

        return
            match result with
            | Ok response ->
                this.CreatedAtAction(nameof (this.GetByIdAsync), {| id = response.Id |}, response) :> IActionResult
            | Error(ValidationError message) ->
                this.BadRequest(ProblemDetails(Title = "Validation Error", Detail = message)) :> IActionResult
            | Error(Conflict message) ->
                this.Conflict(ProblemDetails(Title = "Conflict", Detail = message)) :> IActionResult
            | Error(NotFound message) ->
                this.NotFound(ProblemDetails(Title = "Not Found", Detail = message)) :> IActionResult
            | Error(PersistenceError message) ->
                this.StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ProblemDetails(Title = "Persistence Error", Detail = message)
                )
                :> IActionResult
    }