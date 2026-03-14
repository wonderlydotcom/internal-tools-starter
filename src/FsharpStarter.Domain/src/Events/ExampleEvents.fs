namespace FsharpStarter.Domain.Events

open System
open FsharpStarter.Domain
open FsharpStarter.Domain.ValueObjects

type ExampleEventType = | ExampleCreated

type ExampleEventData = { Name: string }

type ExampleDomainEvent = {
    EventId: Guid
    AggregateId: ExampleId
    Version: int
    OccurredAt: DateTime
    EventType: ExampleEventType
    Data: ExampleEventData
} with

    interface IDomainEvent with
        member this.EventId = this.EventId
        member this.AggregateId = ExampleId.value this.AggregateId
        member this.Version = this.Version
        member this.OccurredAt = this.OccurredAt

        member this.EventTypeName =
            match this.EventType with
            | ExampleCreated -> "ExampleCreated"

module ExampleDomainEvent =
    let eventTypeToString =
        function
        | ExampleCreated -> "ExampleCreated"

    let tryEventTypeFromString =
        function
        | "ExampleCreated" -> Some ExampleCreated
        | _ -> None