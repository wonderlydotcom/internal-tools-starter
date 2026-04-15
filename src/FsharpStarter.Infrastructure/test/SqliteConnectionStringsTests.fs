module FsharpStarter.Infrastructure.Tests.SqliteConnectionStringsTests

open System
open System.IO
open FsharpStarter.Infrastructure.Database
open Xunit

[<Fact>]
let ``Local fallback uses repo data directory when no connection string is configured`` () =
    let baseDirectory =
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "api")

    let expectedPath =
        Path.GetFullPath(Path.Combine(baseDirectory, "data", "fsharp-starter.db"))

    let connectionString =
        SqliteConnectionStrings.resolveConnectionString false baseDirectory "fsharp-starter.db" None

    Assert.Equal($"Data Source={expectedPath}", connectionString)

[<Fact>]
let ``Container fallback uses pvc-backed app data directory when no connection string is configured`` () =
    let connectionString =
        SqliteConnectionStrings.resolveConnectionString true "/workspace/fsharp-starter" "fsharp-starter.db" None

    Assert.Equal("Data Source=/app/data/fsharp-starter.db", connectionString)

[<Fact>]
let ``Container guard accepts sqlite data sources under pvc mount`` () =
    let connectionString =
        SqliteConnectionStrings.resolveConnectionString
            true
            "/workspace/fsharp-starter"
            "fsharp-starter.db"
            (Some "Data Source=/app/data/custom.db")

    Assert.Equal("Data Source=/app/data/custom.db", connectionString)

[<Fact>]
let ``Container guard rejects sqlite data sources outside pvc mount`` () =
    let ex =
        Assert.Throws<InvalidOperationException>(fun () ->
            SqliteConnectionStrings.resolveConnectionString
                true
                "/workspace/fsharp-starter"
                "fsharp-starter.db"
                (Some "Data Source=/app/custom.db")
            |> ignore)

    Assert.Contains("/app/data", ex.Message)
    Assert.Contains("/app/custom.db", ex.Message)

[<Fact>]
let ``Configured connection strings are trimmed before use`` () =
    let connectionString =
        SqliteConnectionStrings.resolveConnectionString
            false
            "/workspace/fsharp-starter"
            "fsharp-starter.db"
            (Some "  Data Source=/tmp/fsharp-starter.db  ")

    Assert.Equal("Data Source=/tmp/fsharp-starter.db", connectionString)

[<Fact>]
let ``Container guard rejects connection strings without a sqlite data source`` () =
    let ex =
        Assert.Throws<InvalidOperationException>(fun () ->
            SqliteConnectionStrings.resolveConnectionString
                true
                "/workspace/fsharp-starter"
                "fsharp-starter.db"
                (Some "Mode=ReadWriteCreate")
            |> ignore)

    Assert.Contains("must set Data Source", ex.Message)

[<Fact>]
let ``EnsureParentDirectoryExists creates the parent directory for file-backed sqlite databases`` () =
    let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
    let dbPath = Path.Combine(root, "data", "fsharp-starter.db")
    let connectionString = $"Data Source={dbPath}"

    try
        let result = SqliteConnectionStrings.ensureParentDirectoryExists connectionString
        Assert.Equal(connectionString, result)
        Assert.True(Directory.Exists(Path.GetDirectoryName(dbPath)))
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, recursive = true)

[<Fact>]
let ``EnsureParentDirectoryExists leaves memory and file URIs unchanged`` () =
    Assert.Equal("Data Source=:memory:", SqliteConnectionStrings.ensureParentDirectoryExists "Data Source=:memory:")

    Assert.Equal(
        "Data Source=file:fsharp-starter.db?mode=memory&cache=shared",
        SqliteConnectionStrings.ensureParentDirectoryExists
            "Data Source=file:fsharp-starter.db?mode=memory&cache=shared"
    )

[<Fact>]
let ``IsRunningInContainer reads the container environment flag case-insensitively`` () =
    let originalValue =
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")

    try
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "TrUe")
        Assert.True(SqliteConnectionStrings.isRunningInContainer ())

        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", null)
        Assert.False(SqliteConnectionStrings.isRunningInContainer ())
    finally
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", originalValue)