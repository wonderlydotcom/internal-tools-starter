module FsharpStarter.Domain.Tests.DomainPropertyTests

open System
open FsCheck
open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.Events
open FsharpStarter.Domain.ValueObjects
open Xunit

let private nonBlank fallback (value: string) =
    let candidate = if isNull value then fallback else value

    if String.IsNullOrWhiteSpace candidate then
        fallback
    else
        candidate

[<Fact>]
let ``ExampleId round-trips arbitrary GUIDs`` () =
    let property (guid: Guid) =
        let id = ExampleId.OfGuid guid

        ExampleId.value id = guid && ExampleId.TryParse(guid.ToString("D")) = Some id

    Check.QuickThrowOnFailure property

[<Fact>]
let ``Create trims arbitrary nonblank names into state and event data`` () =
    let property (rawName: string) =
        let name = nonBlank "example" rawName
        let expected = name.Trim()

        match ExampleAggregate.Create(ExampleId.New(), name, DateTime(2026, 1, 1, 0, 0, 0)) with
        | Error _ -> false
        | Ok aggregate ->
            match aggregate.State, aggregate.GetUncommittedEvents() with
            | Some state, [ event ] ->
                state.Name = expected
                && event.EventType = ExampleCreated
                && event.Data.Name = expected
                && state.CreatedAt.Kind = DateTimeKind.Utc
                && aggregate.Version = 1
            | _ -> false

    Check.QuickThrowOnFailure property

[<Fact>]
let ``Blank generated names are always rejected`` () =
    let property (rawWidth: int) =
        let blank = String(' ', abs (rawWidth % 64))

        match ExampleAggregate.Create(ExampleId.New(), blank, DateTime.UtcNow) with
        | Error(ValidationError _) -> true
        | _ -> false

    Check.QuickThrowOnFailure property

[<Fact>]
let ``FromHistory rehydrates version without uncommitted events`` () =
    let property (rawVersion: int) (rawName: string) =
        let version = abs (rawVersion % 1000) + 1
        let name = nonBlank "rehydrated" rawName
        let id = ExampleId.New()

        let history = [
            {
                EventId = Guid.NewGuid()
                AggregateId = id
                Version = version
                OccurredAt = DateTime(2026, 1, 1, 0, 0, 0)
                EventType = ExampleCreated
                Data = { Name = name.Trim() }
            }
        ]

        match ExampleAggregate.FromHistory history with
        | Error _ -> false
        | Ok aggregate ->
            aggregate.Version = version
            && List.isEmpty (aggregate.GetUncommittedEvents())
            && (aggregate.State
                |> Option.exists (fun state -> state.Id = id && state.Name = name.Trim()))

    Check.QuickThrowOnFailure property