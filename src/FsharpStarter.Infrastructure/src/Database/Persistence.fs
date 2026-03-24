namespace FsharpStarter.Infrastructure.Database

open System.Reflection
open DbUp
open DbUp.Builder
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open FsharpStarter.Domain.Ports
open FsharpStarter.Infrastructure.Database.Repositories

module Persistence =

    let private createUpgrader (connectionString: string) (logToConsole: bool) : UpgradeEngineBuilder =
        let upgraderBuilder: UpgradeEngineBuilder =
            DeployChanges.To
                .SQLiteDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    Assembly.GetExecutingAssembly(),
                    (fun (scriptName: string) -> scriptName.Contains("DatabaseUpgradeScripts.DBUP"))
                )
                .WithTransactionPerScript()

        if logToConsole then
            upgraderBuilder.LogToConsole()
        else
            upgraderBuilder

    let private performUpgrade connectionString logToConsole =
        let upgrader =
            createUpgrader connectionString logToConsole |> fun builder -> builder.Build()

        let result = upgrader.PerformUpgrade()

        if not result.Successful then
            match result.Error with
            | null -> failwith "Failed to upgrade database."
            | ex -> failwithf "Failed to upgrade database: %s" ex.Message

    let addInfrastructure (services: IServiceCollection) (connectionString: string) =
        services.AddDbContext<FsharpStarterDbContext>(fun options -> options.UseSqlite(connectionString) |> ignore)
        |> ignore

        services.AddScoped<IExampleRepository, ExampleRepository>() |> ignore
        services

    let upgradeDatabase (connectionString: string) = performUpgrade connectionString true

    let upgradeDatabaseQuietly (connectionString: string) = performUpgrade connectionString false