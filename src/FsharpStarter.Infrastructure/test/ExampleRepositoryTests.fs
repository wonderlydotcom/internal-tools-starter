module FsharpStarter.Infrastructure.Tests.ExampleRepositoryTests

open System
open System.IO
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
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