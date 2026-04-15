module FsharpStarter.Domain.Tests.ExampleAggregateTests

open System
open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.Events
open FsharpStarter.Domain.ValueObjects
open Xunit

[<Fact>]
let ``Create emits ExampleCreated event with normalized name`` () =
    let id = ExampleId.New()
    let createdAt = DateTime.Parse("2026-01-01T00:00:00Z")

    let result = ExampleAggregate.Create(id, "  Canonical Example  ", createdAt)

    match result with
    | Error error -> failwithf "Expected success but got %A" error
    | Ok aggregate ->
        let events = aggregate.GetUncommittedEvents()
        Assert.Single(events) |> ignore

        let event = List.head events
        Assert.Equal(ExampleCreated, event.EventType)
        Assert.Equal("Canonical Example", event.Data.Name)

[<Fact>]
let ``FromHistory rehydrates aggregate state`` () =
    let id = ExampleId.New()
    let occurredAt = DateTime.Parse("2026-01-01T00:00:00Z")

    let history = [
        {
            EventId = Guid.NewGuid()
            AggregateId = id
            Version = 1
            OccurredAt = occurredAt
            EventType = ExampleCreated
            Data = { Name = "Rehydrated" }
        }
    ]

    let result = ExampleAggregate.FromHistory(history)

    match result with
    | Error error -> failwithf "Expected success but got %A" error
    | Ok aggregate ->
        match aggregate.State with
        | None -> failwith "Expected state to be populated."
        | Some state ->
            Assert.Equal(id.Value, state.Id.Value)
            Assert.Equal("Rehydrated", state.Name)
            Assert.Equal(1, aggregate.Version)

[<Fact>]
let ``Create rejects blank names`` () =
    let result =
        ExampleAggregate.Create(ExampleId.New(), "   ", DateTime.Parse("2026-01-01T00:00:00Z"))

    match result with
    | Ok _ -> failwith "Expected validation error but got success"
    | Error(ValidationError message) -> Assert.Equal("Name is required.", message)
    | Error other -> failwithf "Expected ValidationError but got %A" other

[<Fact>]
let ``FromHistory rejects empty streams`` () =
    let result = ExampleAggregate.FromHistory([])

    match result with
    | Ok _ -> failwith "Expected not found error but got success"
    | Error(NotFound message) -> Assert.Equal("Example event stream was empty.", message)
    | Error other -> failwithf "Expected NotFound but got %A" other

[<Fact>]
let ``Example domain events expose interface metadata and string conversion helpers`` () =
    let occurredAt = DateTime.Parse("2026-01-01T00:00:00Z")
    let aggregateId = ExampleId.New()

    let domainEvent = {
        EventId = Guid.NewGuid()
        AggregateId = aggregateId
        Version = 3
        OccurredAt = occurredAt
        EventType = ExampleCreated
        Data = { Name = "Example" }
    }

    let asInterface = domainEvent :> IDomainEvent

    Assert.Equal(domainEvent.EventId, asInterface.EventId)
    Assert.Equal(aggregateId.Value, asInterface.AggregateId)
    Assert.Equal(3, asInterface.Version)
    Assert.Equal(occurredAt, asInterface.OccurredAt)
    Assert.Equal("ExampleCreated", asInterface.EventTypeName)
    Assert.Equal("ExampleCreated", ExampleDomainEvent.eventTypeToString ExampleCreated)
    Assert.Equal(Some ExampleCreated, ExampleDomainEvent.tryEventTypeFromString "ExampleCreated")
    Assert.Equal(None, ExampleDomainEvent.tryEventTypeFromString "Unknown")

[<Fact>]
let ``ExampleId parses valid GUIDs and rejects invalid input`` () =
    let guid = Guid.NewGuid()

    let parsed = ExampleId.TryParse(guid.ToString())
    let invalid = ExampleId.TryParse("not-a-guid")

    match parsed with
    | None -> failwith "Expected ExampleId.TryParse to succeed"
    | Some exampleId ->
        Assert.Equal(guid, ExampleId.value exampleId)
        Assert.Equal(guid, ExampleId.OfGuid(guid).Value)

    Assert.Equal(None, invalid)

[<Fact>]
let ``DomainError message unwraps every case`` () =
    Assert.Equal("validation", DomainError.message (ValidationError "validation"))
    Assert.Equal("missing", DomainError.message (NotFound "missing"))
    Assert.Equal("conflict", DomainError.message (Conflict "conflict"))
    Assert.Equal("persistence", DomainError.message (PersistenceError "persistence"))