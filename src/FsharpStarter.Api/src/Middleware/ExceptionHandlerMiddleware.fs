namespace FsharpStarter.Api.Middleware

open System
open System.Text.Json
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type ExceptionHandlerMiddleware
    (next: RequestDelegate, logger: ILogger<ExceptionHandlerMiddleware>, env: IHostEnvironment) =

    member _.Invoke(context: HttpContext) = task {
        try
            do! next.Invoke(context)
        with ex ->
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path)

            context.Response.StatusCode <- StatusCodes.Status500InternalServerError
            context.Response.ContentType <- "application/problem+json"

            let problem = ProblemDetails(Status = 500, Title = "Internal Server Error")

            if env.IsDevelopment() then
                problem.Detail <- ex.Message
                problem.Extensions["stackTrace"] <- ex.StackTrace

            let options =
                JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

            do! JsonSerializer.SerializeAsync(context.Response.Body, problem, options)
    }