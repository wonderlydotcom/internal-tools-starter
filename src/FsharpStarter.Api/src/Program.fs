open System
open System.Diagnostics
open FsharpStarter.Api.Auth
open FsharpStarter.Api.Middleware
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
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
    builder.Services.AddHttpClient() |> ignore

    if
        String.IsNullOrWhiteSpace(builder.Configuration["Auth:IAP:JwtAudience"])
        && not (String.IsNullOrWhiteSpace(builder.Configuration["IAP_JWT_AUDIENCE"]))
    then
        builder.Configuration["Auth:IAP:JwtAudience"] <- builder.Configuration["IAP_JWT_AUDIENCE"]

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
        app.UseMiddleware<DevAuthMiddleware>() |> ignore
    else
        app.UseMiddleware<IapAuthMiddleware>() |> ignore

    app.UseHttpsRedirection() |> ignore
    app.UseStaticFiles() |> ignore
    app.MapGet("/healthy", Func<string>(fun () -> "OK")) |> ignore

    app.MapGet(
        "/__api/me",
        Func<HttpContext, IResult>(fun context ->
            let requestUser = RequestUserContext.get context

            Results.Json(
                {|
                    email = requestUser.Email
                    name = requestUser.Name
                    profile = requestUser.Profile
                    authenticationSource = requestUser.AuthenticationSource
                |}
            ))
    )
    |> ignore

    app.MapControllers() |> ignore
    app.MapFallbackToFile("index.html") |> ignore
    app.Run()
    0