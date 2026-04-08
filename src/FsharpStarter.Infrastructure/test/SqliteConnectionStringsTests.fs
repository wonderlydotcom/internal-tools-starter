module FsharpStarter.Infrastructure.Tests.SqliteConnectionStringsTests

open System
open System.IO
open FsharpStarter.Infrastructure.Database
open Xunit

[<Fact>]
let ``Local fallback uses repo data directory when no connection string is configured`` () =
    let baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "api")
    let expectedPath = Path.GetFullPath(Path.Combine(baseDirectory, "data", "fsharp-starter.db"))

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
