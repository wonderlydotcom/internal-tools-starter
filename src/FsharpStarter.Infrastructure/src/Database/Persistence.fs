namespace FsharpStarter.Infrastructure.Database

open System.Reflection
open DbUp
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open FsharpStarter.Domain.Ports
open FsharpStarter.Infrastructure.Database.Repositories

module Persistence =

    let addInfrastructure (services: IServiceCollection) (connectionString: string) =
        services.AddDbContext<FsharpStarterDbContext>(fun options -> options.UseSqlite(connectionString) |> ignore)
        |> ignore

        services.AddScoped<IExampleRepository, ExampleRepository>() |> ignore
        services

    let upgradeDatabase (connectionString: string) =
        let upgrader =
            DeployChanges.To
                .SQLiteDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    Assembly.GetExecutingAssembly(),
                    (fun (scriptName: string) -> scriptName.Contains("DatabaseUpgradeScripts.DBUP"))
                )
                .WithTransactionPerScript()
                .LogToConsole()
                .Build()

        let result = upgrader.PerformUpgrade()

        if not result.Successful then
            match result.Error with
            | null -> failwith "Failed to upgrade database."
            | ex -> failwithf "Failed to upgrade database: %s" ex.Message