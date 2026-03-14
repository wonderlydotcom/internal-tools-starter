namespace FsharpStarter.Infrastructure.Database.Repositories

open System
open System.Linq
open System.Text.Json
open Microsoft.EntityFrameworkCore
open FsharpStarter.Domain.Ports
open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.Events
open FsharpStarter.Domain.ValueObjects
open FsharpStarter.Infrastructure.Database

type ExampleRepository(dbContext: FsharpStarterDbContext) =
    let normalizeUtc (value: DateTime) =
        DateTime.SpecifyKind(value, DateTimeKind.Utc)

    let serializePayload (domainEvent: ExampleDomainEvent) =
        JsonSerializer.Serialize(domainEvent.Data)

    let deserializeEvent (row: DomainEventRecord) =
        match ExampleDomainEvent.tryEventTypeFromString row.EventType with
        | None -> Error(PersistenceError $"Unknown event type '{row.EventType}'.")
        | Some eventType ->
            try
                let payload = JsonSerializer.Deserialize<ExampleEventData>(row.PayloadJson)

                if isNull (box payload) then
                    Error(PersistenceError "Event payload was null.")
                else
                    Ok {
                        EventId = row.EventId
                        AggregateId = ExampleId.OfGuid row.AggregateId
                        Version = row.Version
                        OccurredAt = normalizeUtc row.OccurredAt
                        EventType = eventType
                        Data = payload
                    }
            with exn ->
                Error(PersistenceError $"Failed to deserialize event payload: {exn.Message}")

    interface IExampleRepository with
        member _.Save(aggregate: ExampleAggregate) = async {
            match aggregate.State with
            | None -> return Error(PersistenceError "Cannot save aggregate with empty state.")
            | Some state ->
                let! transaction = dbContext.Database.BeginTransactionAsync() |> Async.AwaitTask

                try
                    let id = ExampleId.value state.Id

                    let! exists = dbContext.Examples.AnyAsync(fun row -> row.Id = id) |> Async.AwaitTask

                    if exists then
                        do! transaction.RollbackAsync() |> Async.AwaitTask
                        return Error(Conflict $"Example '{id}' already exists.")
                    else
                        let exampleRow = ExampleRecord()
                        exampleRow.Id <- id
                        exampleRow.Name <- state.Name
                        exampleRow.CreatedAt <- normalizeUtc state.CreatedAt
                        exampleRow.Version <- aggregate.Version

                        dbContext.Examples.Add(exampleRow) |> ignore

                        for domainEvent in aggregate.GetUncommittedEvents() do
                            let eventRow = DomainEventRecord()
                            eventRow.EventId <- domainEvent.EventId
                            eventRow.AggregateId <- ExampleId.value domainEvent.AggregateId
                            eventRow.Version <- domainEvent.Version
                            eventRow.OccurredAt <- normalizeUtc domainEvent.OccurredAt
                            eventRow.EventType <- ExampleDomainEvent.eventTypeToString domainEvent.EventType
                            eventRow.PayloadJson <- serializePayload domainEvent
                            dbContext.DomainEvents.Add(eventRow) |> ignore

                        let! _ = dbContext.SaveChangesAsync() |> Async.AwaitTask
                        do! transaction.CommitAsync() |> Async.AwaitTask
                        aggregate.ClearUncommittedEvents()
                        return Ok()
                with exn ->
                    do! transaction.RollbackAsync() |> Async.AwaitTask
                    return Error(PersistenceError $"Failed to save aggregate: {exn.Message}")
        }

        member _.GetById(exampleId: ExampleId) = async {
            try
                let id = ExampleId.value exampleId

                let! eventRows =
                    dbContext.DomainEvents
                        .Where(fun row -> row.AggregateId = id)
                        .OrderBy(fun row -> row.Version)
                        .ToListAsync()
                    |> Async.AwaitTask

                if eventRows.Count = 0 then
                    return Ok None
                else
                    let parsed = eventRows |> Seq.map deserializeEvent |> Seq.toList

                    match
                        parsed
                        |> List.tryPick (function
                            | Error error -> Some error
                            | Ok _ -> None)
                    with
                    | Some error -> return Error error
                    | None ->
                        let history =
                            parsed
                            |> List.choose (function
                                | Ok event -> Some event
                                | Error _ -> None)

                        match ExampleAggregate.FromHistory(history) with
                        | Error error -> return Error error
                        | Ok aggregate -> return Ok(Some aggregate)
            with exn ->
                return Error(PersistenceError $"Failed to load aggregate: {exn.Message}")
        }