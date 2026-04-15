module FsharpStarter.Infrastructure.Tests.ExampleRepositoryTests

open System
open System.IO
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open FsharpStarter.Domain
open FsharpStarter.Domain.Ports
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.ValueObjects
open FsharpStarter.Infrastructure.Database
open FsharpStarter.Infrastructure.Database.Repositories
open Xunit

[<Fact>]
let ``DBUp upgrade creates schema and journal tables`` () =
    let dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db")
    let connectionString = $"Data Source={dbPath}"

    try
        Persistence.upgradeDatabase connectionString

        use connection = new SqliteConnection(connectionString)
        connection.Open()

        use command = connection.CreateCommand()
        command.CommandText <- "SELECT name FROM sqlite_master WHERE type = 'table';"

        use reader = command.ExecuteReader()
        let mutable tables = Set.empty<string>

        while reader.Read() do
            tables <- tables.Add(reader.GetString(0))

        Assert.Contains("examples", tables)
        Assert.Contains("domain_events", tables)
        Assert.Contains("SchemaVersions", tables)
    finally
        if File.Exists(dbPath) then
            File.Delete(dbPath)

[<Fact>]
let ``Repository saves events and rehydrates aggregate`` () =
    use connection = new SqliteConnection("Data Source=:memory:")
    connection.Open()

    let options =
        DbContextOptionsBuilder<FsharpStarterDbContext>().UseSqlite(connection).Options

    use dbContext = new FsharpStarterDbContext(options)
    dbContext.Database.EnsureCreated() |> ignore

    let repository = ExampleRepository(dbContext) :> IExampleRepository

    let aggregate =
        match ExampleAggregate.Create(ExampleId.New(), "Stored Example", DateTime.UtcNow) with
        | Error error -> failwithf "Expected aggregate creation success but got %A" error
        | Ok value -> value

    let saved = repository.Save(aggregate) |> Async.RunSynchronously

    match saved with
    | Error error -> failwithf "Expected save success but got %A" error
    | Ok() -> ()

    let id =
        match aggregate.State with
        | None -> failwith "Missing aggregate state"
        | Some state -> state.Id

    let loaded = repository.GetById(id) |> Async.RunSynchronously

    match loaded with
    | Error error -> failwithf "Expected load success but got %A" error
    | Ok None -> failwith "Expected aggregate to exist"
    | Ok(Some value) ->
        match value.State with
        | None -> failwith "Expected rehydrated state"
        | Some state -> Assert.Equal("Stored Example", state.Name)

[<Fact>]
let ``Repository rejects duplicate saves for the same aggregate id`` () =
    use connection = new SqliteConnection("Data Source=:memory:")
    connection.Open()

    let options =
        DbContextOptionsBuilder<FsharpStarterDbContext>().UseSqlite(connection).Options

    use dbContext = new FsharpStarterDbContext(options)
    dbContext.Database.EnsureCreated() |> ignore

    let repository = ExampleRepository(dbContext) :> IExampleRepository
    let exampleId = ExampleId.New()

    let aggregate =
        match ExampleAggregate.Create(exampleId, "Duplicate Example", DateTime.UtcNow) with
        | Error error -> failwithf "Expected aggregate creation success but got %A" error
        | Ok value -> value

    let history = aggregate.GetUncommittedEvents()
    let firstSave = repository.Save(aggregate) |> Async.RunSynchronously

    let duplicateAggregate =
        match ExampleAggregate.FromHistory(history) with
        | Ok value -> value
        | Error error -> failwithf "Expected duplicate aggregate rehydration success but got %A" error

    let secondSave = repository.Save(duplicateAggregate) |> Async.RunSynchronously

    Assert.Equal(Ok(), firstSave)

    match secondSave with
    | Error(Conflict message) -> Assert.Contains("already exists", message)
    | Ok() -> failwith "Expected conflict error but got success"
    | Error other -> failwithf "Expected Conflict but got %A" other

[<Fact>]
let ``Repository returns none when no event history exists for an id`` () =
    use connection = new SqliteConnection("Data Source=:memory:")
    connection.Open()

    let options =
        DbContextOptionsBuilder<FsharpStarterDbContext>().UseSqlite(connection).Options

    use dbContext = new FsharpStarterDbContext(options)
    dbContext.Database.EnsureCreated() |> ignore

    let repository = ExampleRepository(dbContext) :> IExampleRepository
    let result = repository.GetById(ExampleId.New()) |> Async.RunSynchronously

    Assert.Equal(Ok None, result)

[<Fact>]
let ``Repository surfaces unknown event types as persistence errors`` () =
    use connection = new SqliteConnection("Data Source=:memory:")
    connection.Open()

    let options =
        DbContextOptionsBuilder<FsharpStarterDbContext>().UseSqlite(connection).Options

    use dbContext = new FsharpStarterDbContext(options)
    dbContext.Database.EnsureCreated() |> ignore

    let aggregateId = Guid.NewGuid()

    dbContext.DomainEvents.Add(
        DomainEventRecord(
            EventId = Guid.NewGuid(),
            AggregateId = aggregateId,
            Version = 1,
            OccurredAt = DateTime.UtcNow,
            EventType = "UnknownEvent",
            PayloadJson = """{"Name":"Example"}"""
        )
    )
    |> ignore

    dbContext.SaveChanges() |> ignore

    let repository = ExampleRepository(dbContext) :> IExampleRepository

    let result =
        repository.GetById(ExampleId.OfGuid aggregateId) |> Async.RunSynchronously

    match result with
    | Error(PersistenceError message) -> Assert.Contains("Unknown event type", message)
    | Ok _ -> failwith "Expected persistence error but got success"
    | Error other -> failwithf "Expected PersistenceError but got %A" other

[<Fact>]
let ``Repository surfaces malformed payloads as persistence errors`` () =
    use connection = new SqliteConnection("Data Source=:memory:")
    connection.Open()

    let options =
        DbContextOptionsBuilder<FsharpStarterDbContext>().UseSqlite(connection).Options

    use dbContext = new FsharpStarterDbContext(options)
    dbContext.Database.EnsureCreated() |> ignore

    let aggregateId = Guid.NewGuid()

    dbContext.DomainEvents.Add(
        DomainEventRecord(
            EventId = Guid.NewGuid(),
            AggregateId = aggregateId,
            Version = 1,
            OccurredAt = DateTime.UtcNow,
            EventType = "ExampleCreated",
            PayloadJson = "{invalid-json"
        )
    )
    |> ignore

    dbContext.SaveChanges() |> ignore

    let repository = ExampleRepository(dbContext) :> IExampleRepository

    let result =
        repository.GetById(ExampleId.OfGuid aggregateId) |> Async.RunSynchronously

    match result with
    | Error(PersistenceError message) -> Assert.Contains("Failed to deserialize event payload", message)
    | Ok _ -> failwith "Expected persistence error but got success"
    | Error other -> failwithf "Expected PersistenceError but got %A" other