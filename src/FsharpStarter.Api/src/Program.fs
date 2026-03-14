open System
open System.Diagnostics
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open FsharpStarter.Application.Handlers
open FsharpStarter.Infrastructure.Database

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddControllers() |> ignore
    builder.Services.AddEndpointsApiExplorer() |> ignore
    builder.Services.AddSwaggerGen() |> ignore

    let connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")

    Persistence.upgradeDatabase connectionString

    Persistence.addInfrastructure builder.Services connectionString |> ignore
    builder.Services.AddScoped<ExampleHandler>() |> ignore

    let activitySource = new ActivitySource("FsharpStarter.Api")
    builder.Services.AddSingleton<ActivitySource>(activitySource) |> ignore

    builder.Services
        .AddOpenTelemetry()
        .WithTracing(fun tracing ->
            tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("fsharp-starter-api"))
                .AddSource("FsharpStarter.Api")
                .AddAspNetCoreInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
            |> ignore)
    |> ignore

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.UseSwagger() |> ignore
        app.UseSwaggerUI() |> ignore

    app.UseHttpsRedirection() |> ignore
    app.UseStaticFiles() |> ignore
    app.MapGet("/healthy", Func<string>(fun () -> "OK")) |> ignore
    app.MapControllers() |> ignore
    app.MapFallbackToFile("index.html") |> ignore
    app.Run()
    0