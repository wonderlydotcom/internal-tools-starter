namespace FsharpStarter.Infrastructure.Database

open System
open System.IO
open Microsoft.Data.Sqlite

module SqliteConnectionStrings =
    let private containerDataRoot = "/app/data"

    let isRunningInContainer () =
        String.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase
        )

    let private normalizeConfiguredConnectionString (configuredConnectionString: string option) =
        configuredConnectionString
        |> Option.bind (fun value ->
            let trimmed = value.Trim()
            if String.IsNullOrWhiteSpace(trimmed) then None else Some trimmed)

    let private defaultDataSource (runningInContainer: bool) (baseDirectory: string) (databaseFileName: string) =
        if runningInContainer then
            Path.Combine(containerDataRoot, databaseFileName)
        else
            Path.GetFullPath(Path.Combine(baseDirectory, "data", databaseFileName))

    let private getDataSource (connectionString: string) =
        let builder = SqliteConnectionStringBuilder(connectionString)

        if String.IsNullOrWhiteSpace(builder.DataSource) then
            None
        else
            Some builder.DataSource

    let private ensureContainerDataSourceInvariant (runningInContainer: bool) (connectionString: string) =
        if runningInContainer then
            let dataSource =
                match getDataSource connectionString with
                | Some value -> value
                | None ->
                    invalidOp
                        "SQLite connection string must set Data Source when DOTNET_RUNNING_IN_CONTAINER=true."

            let requiredRoot = Path.GetFullPath(containerDataRoot)
            let effectivePath = Path.GetFullPath(dataSource)
            let requiredPrefix = requiredRoot + string Path.DirectorySeparatorChar

            if
                not (
                    String.Equals(effectivePath, requiredRoot, StringComparison.Ordinal)
                    || effectivePath.StartsWith(requiredPrefix, StringComparison.Ordinal)
                )
            then
                invalidOp
                    $"SQLite Data Source must resolve under {containerDataRoot} when DOTNET_RUNNING_IN_CONTAINER=true. Effective path: {effectivePath}"

        connectionString

    let resolveConnectionString
        (runningInContainer: bool)
        (baseDirectory: string)
        (databaseFileName: string)
        (configuredConnectionString: string option)
        =
        let connectionString =
            match normalizeConfiguredConnectionString configuredConnectionString with
            | Some value -> value
            | None -> $"Data Source={defaultDataSource runningInContainer baseDirectory databaseFileName}"

        ensureContainerDataSourceInvariant runningInContainer connectionString

    let ensureParentDirectoryExists (connectionString: string) =
        match getDataSource connectionString with
        | None -> connectionString
        | Some dataSource when String.Equals(dataSource, ":memory:", StringComparison.Ordinal) -> connectionString
        | Some dataSource when dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase) -> connectionString
        | Some dataSource ->
            let fullPath = Path.GetFullPath(dataSource)
            let parentDirectory = Path.GetDirectoryName(fullPath)

            if not (String.IsNullOrWhiteSpace parentDirectory) then
                Directory.CreateDirectory(parentDirectory) |> ignore

            connectionString
