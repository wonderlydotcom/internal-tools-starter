namespace FsharpStarter.Domain.Entities

open System
open FsharpStarter.Domain
open FsharpStarter.Domain.Events
open FsharpStarter.Domain.ValueObjects

type ExampleState = {
    Id: ExampleId
    Name: string
    CreatedAt: DateTime
}

type ExampleAggregate private () =
    inherit EventSourcingAggregate<ExampleDomainEvent>()

    let mutable state: ExampleState option = None

    static member private NormalizeUtc(value: DateTime) =
        DateTime.SpecifyKind(value, DateTimeKind.Utc)

    member _.State = state

    member private _.Apply(domainEvent: ExampleDomainEvent) =
        match domainEvent.EventType with
        | ExampleCreated ->
            state <-
                Some {
                    Id = domainEvent.AggregateId
                    Name = domainEvent.Data.Name
                    CreatedAt = ExampleAggregate.NormalizeUtc domainEvent.OccurredAt
                }

    member private _.EventVersion(domainEvent: ExampleDomainEvent) = domainEvent.Version

    static member Create(id: ExampleId, name: string, createdAt: DateTime) =
        if String.IsNullOrWhiteSpace(name) then
            Error(ValidationError "Name is required.")
        else
            let aggregate = ExampleAggregate()
            let normalizedCreatedAt = ExampleAggregate.NormalizeUtc createdAt

            let eventData = {
                EventId = Guid.NewGuid()
                AggregateId = id
                Version = 1
                OccurredAt = normalizedCreatedAt
                EventType = ExampleCreated
                Data = { Name = name.Trim() }
            }

            aggregate.Raise(eventData, aggregate.Apply, aggregate.EventVersion)
            Ok aggregate

    static member FromHistory(events: ExampleDomainEvent list) =
        if List.isEmpty events then
            Error(NotFound "Example event stream was empty.")
        else
            let aggregate = ExampleAggregate()
            aggregate.LoadFromHistory(events, aggregate.Apply, aggregate.EventVersion)
            Ok aggregate