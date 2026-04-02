namespace FsharpStarter.Api.Middleware

open System
open Microsoft.AspNetCore.Http
open FsharpStarter.Api.Auth

type DevAuthMiddleware(next: RequestDelegate) =
    member _.InvokeAsync(context: HttpContext) = task {
        let email =
            context.Request.Headers["X-Dev-User-Email"].ToString()
            |> fun value ->
                if String.IsNullOrWhiteSpace(value) then
                    "dev@wonderly.com"
                else
                    value.Trim()

        let name =
            context.Request.Headers["X-Dev-User-Name"].ToString()
            |> fun value ->
                if String.IsNullOrWhiteSpace(value) then
                    Some "Dev User"
                else
                    Some(value.Trim())

        let profile =
            context.Request.Headers["X-Dev-User-Picture"].ToString()
            |> fun value ->
                if String.IsNullOrWhiteSpace(value) then
                    None
                else
                    Some(value.Trim())

        RequestUserContext.set context {
            Name = name
            Email = email
            Profile = profile
            AuthenticationSource = "dev"
        }

        do! next.Invoke(context)
    }