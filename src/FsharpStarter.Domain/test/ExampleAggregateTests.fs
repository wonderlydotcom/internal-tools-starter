module FsharpStarter.Domain.Tests.ExampleAggregateTests

open System
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