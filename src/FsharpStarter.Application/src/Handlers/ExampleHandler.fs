namespace FsharpStarter.Application.Handlers

open System
open FsharpStarter.Application.Commands
open FsharpStarter.Application.DTOs
open FsharpStarter.Domain.Ports
open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.ValueObjects

type ExampleHandler(repository: IExampleRepository, clock: unit -> DateTime) =
    new(repository: IExampleRepository) = ExampleHandler(repository, fun () -> DateTime.UtcNow)

    member _.CreateAsync(command: CreateExampleCommand) = async {
        let exampleId = ExampleId.New()

        match ExampleAggregate.Create(exampleId, command.Name, clock ()) with
        | Error error -> return Error error
        | Ok aggregate ->
            let! persisted = repository.Save(aggregate)

            match persisted, aggregate.State with
            | Error error, _ -> return Error error
            | Ok _, None -> return Error(PersistenceError "Aggregate state was missing after creation.")
            | Ok _, Some state ->
                return
                    Ok {
                        Id = ExampleId.value state.Id
                        Name = state.Name
                        CreatedAt = state.CreatedAt
                        Version = aggregate.Version
                    }
    }

    member _.GetByIdAsync(id: Guid) = async {
        let exampleId = ExampleId.OfGuid id
        let! loaded = repository.GetById(exampleId)

        match loaded with
        | Error error -> return Error error
        | Ok None -> return Error(NotFound $"Example '{id}' was not found.")
        | Ok(Some aggregate) ->
            match aggregate.State with
            | None -> return Error(PersistenceError "Aggregate state was missing after load.")
            | Some state ->
                return
                    Ok {
                        Id = ExampleId.value state.Id
                        Name = state.Name
                        CreatedAt = state.CreatedAt
                        Version = aggregate.Version
                    }
    }