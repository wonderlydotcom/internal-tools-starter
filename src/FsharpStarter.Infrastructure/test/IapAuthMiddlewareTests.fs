module FsharpStarter.Infrastructure.Tests.IapAuthMiddlewareTests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Security.Claims
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open FsharpStarter.Api.Auth
open FsharpStarter.Api.Middleware
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.Primitives
open Microsoft.IdentityModel.Tokens
open System.IdentityModel.Tokens.Jwt
open Xunit

type StubHttpMessageHandler(responder: HttpRequestMessage -> HttpResponseMessage) =
    inherit HttpMessageHandler()

    override _.SendAsync(request: HttpRequestMessage, _cancellationToken: CancellationToken) =
        responder request |> Task.FromResult

type StubHttpClientFactory(client: HttpClient) =
    interface IHttpClientFactory with
        member _.CreateClient(_name: string) = client

let createContext (path: string) =
    let context = DefaultHttpContext()
    context.Request.Path <- PathString(path)
    context.Response.Body <- new MemoryStream()
    context

let addHeader (context: HttpContext) (key: string) (value: string) =
    context.Request.Headers[key] <- StringValues(value)
    context

let readResponseBody (context: HttpContext) =
    context.Response.Body.Seek(0L, SeekOrigin.Begin) |> ignore

    use reader =
        new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen = true)

    reader.ReadToEnd()

let tamperTokenSignature (token: string) =
    let lastCharacter = token[token.Length - 1]
    let replacement = if lastCharacter = 'a' then 'b' else 'a'
    token.Substring(0, token.Length - 1) + string replacement

let createJwtTokenWithClaims (audience: string) (claims: Claim list) =
    let rsa = RSA.Create(2048)
    let key = RsaSecurityKey(rsa.ExportParameters(true))
    key.KeyId <- "test-key"

    let signingCredentials = SigningCredentials(key, SecurityAlgorithms.RsaSha256)
    let now = DateTime.UtcNow

    let token =
        JwtSecurityToken(
            issuer = "https://cloud.google.com/iap",
            audience = audience,
            claims = claims,
            notBefore = Nullable(now.AddMinutes(-1.0)),
            expires = Nullable(now.AddMinutes(5.0)),
            signingCredentials = signingCredentials
        )
        |> JwtSecurityTokenHandler().WriteToken

    let jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key)
    jwk.Kid <- key.KeyId
    let jwksJson = JsonSerializer.Serialize({| keys = [| jwk |] |})

    rsa.Dispose()
    token, jwksJson

let createJwtTestMaterial (audience: string) (email: string) =
    createJwtTokenWithClaims audience [ Claim("email", email) ]

let configureServicesWithResponder
    (configValues: (string * string) list)
    (responder: HttpRequestMessage -> HttpResponseMessage)
    =
    let services = ServiceCollection()

    let configuration =
        ConfigurationBuilder().AddInMemoryCollection(configValues |> dict).Build()

    let messageHandler = new StubHttpMessageHandler(responder)

    services.AddSingleton<IConfiguration>(configuration) |> ignore

    services.AddSingleton<IHttpClientFactory>(StubHttpClientFactory(new HttpClient(messageHandler)))
    |> ignore

    services.BuildServiceProvider()

let configureServices (configValues: (string * string) list) (jwksJson: string) =
    configureServicesWithResponder configValues (fun request ->
        let response = new HttpResponseMessage(HttpStatusCode.OK)

        match request.RequestUri with
        | null ->
            response.StatusCode <- HttpStatusCode.BadRequest
            response.Content <- new StringContent("")
            response
        | _ ->
            response.Content <- new StringContent(jwksJson, Encoding.UTF8, "application/json")
            response)

[<Fact>]
let ``Missing IAP assertion returns 401`` () = task {
    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtAudience", "expected-audience"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            """{"keys":[]}"""

    context.RequestServices <- services

    addHeader context "X-Goog-Authenticated-User-Email" "accounts.google.com:test@wonderly.com"
    |> ignore

    let mutable nextInvoked = false

    let middleware =
        IapAuthMiddleware(
            RequestDelegate(fun _ ->
                nextInvoked <- true
                Task.CompletedTask),
            NullLogger<IapAuthMiddleware>.Instance
        )

    do! middleware.InvokeAsync(context)

    Assert.False(nextInvoked)
    Assert.Equal(401, context.Response.StatusCode)
    Assert.Contains("X-Goog-Iap-Jwt-Assertion", readResponseBody context)
}

[<Fact>]
let ``Wrong audience returns 401`` () = task {
    let invalidToken, jwksJson =
        createJwtTestMaterial "wrong-audience" "test@wonderly.com"

    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtAudience", "expected-audience"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            jwksJson

    context.RequestServices <- services
    addHeader context "X-Goog-Iap-Jwt-Assertion" invalidToken |> ignore

    addHeader context "X-Goog-Authenticated-User-Email" "accounts.google.com:test@wonderly.com"
    |> ignore

    let middleware =
        IapAuthMiddleware(RequestDelegate(fun _ -> Task.CompletedTask), NullLogger<IapAuthMiddleware>.Instance)

    do! middleware.InvokeAsync(context)

    Assert.Equal(401, context.Response.StatusCode)
    Assert.Contains("Invalid IAP JWT assertion", readResponseBody context)
}

[<Fact>]
let ``Invalid signature returns 401`` () = task {
    let validToken, jwksJson =
        createJwtTestMaterial "expected-audience" "test@wonderly.com"

    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtAudience", "expected-audience"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            jwksJson

    context.RequestServices <- services

    addHeader context "X-Goog-Iap-Jwt-Assertion" (tamperTokenSignature validToken)
    |> ignore

    addHeader context "X-Goog-Authenticated-User-Email" "accounts.google.com:test@wonderly.com"
    |> ignore

    let middleware =
        IapAuthMiddleware(RequestDelegate(fun _ -> Task.CompletedTask), NullLogger<IapAuthMiddleware>.Instance)

    do! middleware.InvokeAsync(context)

    Assert.Equal(401, context.Response.StatusCode)
    Assert.Contains("Invalid IAP JWT assertion", readResponseBody context)
}

[<Fact>]
let ``Missing email header falls back to JWT email`` () = task {
    let validToken, jwksJson =
        createJwtTestMaterial "expected-audience" "test@wonderly.com"

    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtAudience", "expected-audience"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            jwksJson

    context.RequestServices <- services
    addHeader context "X-Goog-Iap-Jwt-Assertion" validToken |> ignore

    let mutable nextInvoked = false
    let mutable requestUser = None

    let middleware =
        IapAuthMiddleware(
            RequestDelegate(fun ctx ->
                nextInvoked <- true
                requestUser <- RequestUserContext.tryGet ctx
                ctx.Response.StatusCode <- 204
                Task.CompletedTask),
            NullLogger<IapAuthMiddleware>.Instance
        )

    do! middleware.InvokeAsync(context)

    Assert.True(nextInvoked)
    Assert.Equal(204, context.Response.StatusCode)

    match requestUser with
    | None -> failwith "Expected request user context to be set"
    | Some user -> Assert.Equal("test@wonderly.com", user.Email)
}

[<Fact>]
let ``JWT email remains canonical when IAP email header differs`` () = task {
    let validToken, jwksJson =
        createJwtTestMaterial "expected-audience" "token-user@wonderly.com"

    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtAudience", "expected-audience"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            jwksJson

    context.RequestServices <- services
    addHeader context "X-Goog-Iap-Jwt-Assertion" validToken |> ignore

    addHeader context "X-Goog-Authenticated-User-Email" "accounts.google.com:header-user@wonderly.com"
    |> ignore

    let mutable nextInvoked = false
    let mutable requestUser = None

    let middleware =
        IapAuthMiddleware(
            RequestDelegate(fun ctx ->
                nextInvoked <- true
                requestUser <- RequestUserContext.tryGet ctx
                ctx.Response.StatusCode <- 204
                Task.CompletedTask),
            NullLogger<IapAuthMiddleware>.Instance
        )

    do! middleware.InvokeAsync(context)

    Assert.True(nextInvoked)
    Assert.Equal(204, context.Response.StatusCode)

    match requestUser with
    | None -> failwith "Expected request user context to be set"
    | Some user -> Assert.Equal("token-user@wonderly.com", user.Email)
}

[<Fact>]
let ``Healthy endpoint remains public`` () = task {
    let context = createContext "/healthy"
    let mutable nextInvoked = false

    let middleware =
        IapAuthMiddleware(
            RequestDelegate(fun ctx ->
                nextInvoked <- true
                ctx.Response.StatusCode <- 204
                Task.CompletedTask),
            NullLogger<IapAuthMiddleware>.Instance
        )

    do! middleware.InvokeAsync(context)

    Assert.True(nextInvoked)
    Assert.Equal(204, context.Response.StatusCode)
}

[<Fact>]
let ``Valid request writes normalized request user context`` () = task {
    let validToken, jwksJson =
        createJwtTestMaterial "expected-audience" "test@wonderly.com"

    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
                "IAP_JWT_AUDIENCE", "expected-audience"
            ]
            jwksJson

    context.RequestServices <- services
    addHeader context "X-Goog-Iap-Jwt-Assertion" validToken |> ignore

    addHeader context "X-Goog-Authenticated-User-Email" "accounts.google.com:test@wonderly.com"
    |> ignore

    addHeader context "X-Goog-Authenticated-User-Name" "Test User" |> ignore

    addHeader context "X-Goog-Iap-Attr-Picture" "https://example.test/me.png"
    |> ignore

    let mutable nextInvoked = false
    let mutable requestUser = None

    let middleware =
        IapAuthMiddleware(
            RequestDelegate(fun ctx ->
                nextInvoked <- true
                requestUser <- RequestUserContext.tryGet ctx
                ctx.Response.StatusCode <- 204
                Task.CompletedTask),
            NullLogger<IapAuthMiddleware>.Instance
        )

    do! middleware.InvokeAsync(context)

    Assert.True(nextInvoked)
    Assert.Equal(204, context.Response.StatusCode)

    match requestUser with
    | None -> failwith "Expected request user context to be set"
    | Some user ->
        Assert.Equal("test@wonderly.com", user.Email)
        Assert.Equal(Some "Test User", user.Name)
        Assert.Equal(Some "https://example.test/me.png", user.Profile)
        Assert.Equal("iap", user.AuthenticationSource)
}

[<Fact>]
let ``Missing audience when validation is enabled returns 500`` () = task {
    let validToken, jwksJson =
        createJwtTestMaterial "expected-audience" "test@wonderly.com"

    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            jwksJson

    context.RequestServices <- services
    addHeader context "X-Goog-Iap-Jwt-Assertion" validToken |> ignore

    addHeader context "X-Goog-Authenticated-User-Email" "accounts.google.com:test@wonderly.com"
    |> ignore

    let middleware =
        IapAuthMiddleware(RequestDelegate(fun _ -> Task.CompletedTask), NullLogger<IapAuthMiddleware>.Instance)

    do! middleware.InvokeAsync(context)

    Assert.Equal(500, context.Response.StatusCode)
    Assert.Contains("misconfigured", readResponseBody context)
}

[<Fact>]
let ``JWKS fetch failures return 500`` () = task {
    let validToken, _ = createJwtTestMaterial "expected-audience" "test@wonderly.com"

    let context = createContext "/api/examples"

    let services =
        configureServicesWithResponder
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtAudience", "expected-audience"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            (fun _ ->
                let response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                response.Content <- new StringContent("boom", Encoding.UTF8, "text/plain")
                response)

    context.RequestServices <- services
    addHeader context "X-Goog-Iap-Jwt-Assertion" validToken |> ignore

    addHeader context "X-Goog-Authenticated-User-Email" "accounts.google.com:test@wonderly.com"
    |> ignore

    let middleware =
        IapAuthMiddleware(RequestDelegate(fun _ -> Task.CompletedTask), NullLogger<IapAuthMiddleware>.Instance)

    do! middleware.InvokeAsync(context)

    Assert.Equal(500, context.Response.StatusCode)
    Assert.Contains("Failed to validate IAP JWT assertion", readResponseBody context)
}

[<Fact>]
let ``Disabled JWT validation falls back to the configured email header`` () = task {
    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "false"
                "Auth:IAP:EmailHeader", "X-Forwarded-Email"
                "Auth:IAP:NameHeader", "X-Forwarded-Name"
                "Auth:IAP:PictureHeader", "X-Forwarded-Picture"
            ]
            """{"keys":[]}"""

    context.RequestServices <- services

    addHeader context "X-Forwarded-Email" "accounts.google.com:fallback@wonderly.com"
    |> ignore

    addHeader context "X-Forwarded-Name" "Fallback User" |> ignore

    addHeader context "X-Forwarded-Picture" "https://example.test/fallback.png"
    |> ignore

    let mutable nextInvoked = false
    let mutable requestUser = None

    let middleware =
        IapAuthMiddleware(
            RequestDelegate(fun ctx ->
                nextInvoked <- true
                requestUser <- RequestUserContext.tryGet ctx
                ctx.Response.StatusCode <- 204
                Task.CompletedTask),
            NullLogger<IapAuthMiddleware>.Instance
        )

    do! middleware.InvokeAsync(context)

    Assert.True(nextInvoked)
    Assert.Equal(204, context.Response.StatusCode)

    match requestUser with
    | None -> failwith "Expected request user context to be set"
    | Some user ->
        Assert.Equal("fallback@wonderly.com", user.Email)
        Assert.Equal(Some "Fallback User", user.Name)
        Assert.Equal(Some "https://example.test/fallback.png", user.Profile)
}

[<Fact>]
let ``Validated JWTs without an email claim return 401`` () = task {
    let tokenWithoutEmail, jwksJson =
        createJwtTokenWithClaims "expected-audience" [ Claim("sub", "user-123") ]

    let context = createContext "/api/examples"

    let services =
        configureServices
            [
                "Auth:IAP:ValidateJwt", "true"
                "Auth:IAP:JwtAudience", "expected-audience"
                "Auth:IAP:JwtCertsUrl", "https://example.test/jwks"
            ]
            jwksJson

    context.RequestServices <- services
    addHeader context "X-Goog-Iap-Jwt-Assertion" tokenWithoutEmail |> ignore

    let middleware =
        IapAuthMiddleware(RequestDelegate(fun _ -> Task.CompletedTask), NullLogger<IapAuthMiddleware>.Instance)

    do! middleware.InvokeAsync(context)

    Assert.Equal(401, context.Response.StatusCode)
    Assert.Contains("Invalid IAP JWT assertion", readResponseBody context)
}