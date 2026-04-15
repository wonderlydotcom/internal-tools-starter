module FsharpStarter.Infrastructure.Tests.RequestUserContextAndDevAuthMiddlewareTests

open System
open System.IO
open System.Text
open System.Threading.Tasks
open FsharpStarter.Api.Auth
open FsharpStarter.Api.Middleware
open Microsoft.AspNetCore.Http
open Xunit

let private createContext (path: string) =
    let context = DefaultHttpContext()
    context.Request.Method <- "GET"
    context.Request.Path <- PathString(path)
    context.Response.Body <- new MemoryStream()
    context

let private readResponseBody (context: HttpContext) =
    context.Response.Body.Seek(0L, SeekOrigin.Begin) |> ignore

    use reader =
        new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen = true)

    reader.ReadToEnd()

[<Fact>]
let ``RequestUserContext can be set and read back from HttpContext items`` () =
    let context = createContext "/api/examples"

    let requestUser = {
        Name = Some "Test User"
        Email = "test@wonderly.com"
        Profile = Some "https://example.test/me.png"
        AuthenticationSource = "iap"
    }

    RequestUserContext.set context requestUser

    Assert.Equal(Some requestUser, RequestUserContext.tryGet context)
    Assert.Equal(requestUser, RequestUserContext.get context)

[<Fact>]
let ``RequestUserContext ignores unexpected item payloads`` () =
    let context = createContext "/api/examples"
    context.Items[RequestUserContext.ItemKey] <- "wrong-type"

    Assert.Equal(None, RequestUserContext.tryGet context)

[<Fact>]
let ``RequestUserContext get includes request details when missing`` () =
    let context = createContext "/api/examples"

    let ex =
        Assert.Throws<InvalidOperationException>(fun () -> RequestUserContext.get context |> ignore)

    Assert.Contains("GET /api/examples", ex.Message)

[<Fact>]
let ``DevAuthMiddleware uses default user values when headers are absent`` () = task {
    let context = createContext "/api/examples"
    let mutable requestUser = None

    let middleware =
        DevAuthMiddleware(
            RequestDelegate(fun ctx ->
                requestUser <- RequestUserContext.tryGet ctx
                ctx.Response.StatusCode <- 204
                Task.CompletedTask)
        )

    do! middleware.InvokeAsync(context)

    Assert.Equal(204, context.Response.StatusCode)

    match requestUser with
    | None -> failwith "Expected request user context to be set"
    | Some user ->
        Assert.Equal(Some "Dev User", user.Name)
        Assert.Equal("dev@wonderly.com", user.Email)
        Assert.Equal(None, user.Profile)
        Assert.Equal("dev", user.AuthenticationSource)
}

[<Fact>]
let ``DevAuthMiddleware trims custom headers before storing the request user`` () = task {
    let context = createContext "/api/examples"
    context.Request.Headers["X-Dev-User-Name"] <- "  Example Person  "
    context.Request.Headers["X-Dev-User-Email"] <- "  person@wonderly.com  "
    context.Request.Headers["X-Dev-User-Picture"] <- "  https://example.test/avatar.png  "

    let mutable requestUser = None

    let middleware =
        DevAuthMiddleware(
            RequestDelegate(fun ctx ->
                requestUser <- RequestUserContext.tryGet ctx
                ctx.Response.WriteAsync("ok"))
        )

    do! middleware.InvokeAsync(context)

    Assert.Equal("ok", readResponseBody context)

    match requestUser with
    | None -> failwith "Expected request user context to be set"
    | Some user ->
        Assert.Equal(Some "Example Person", user.Name)
        Assert.Equal("person@wonderly.com", user.Email)
        Assert.Equal(Some "https://example.test/avatar.png", user.Profile)
}