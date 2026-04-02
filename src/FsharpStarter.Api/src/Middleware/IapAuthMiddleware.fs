namespace FsharpStarter.Api.Middleware

open System
open System.IdentityModel.Tokens.Jwt
open System.Net.Http
open System.Security.Claims
open System.Threading
open System.Threading.Tasks
open FsharpStarter.Api.Auth
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.IdentityModel.Tokens

type JwtValidationError =
    | MissingJwtHeader of string
    | Misconfigured of string
    | KeyFetchFailed of string
    | InvalidToken of string

type IapAuthMiddleware(next: RequestDelegate, logger: ILogger<IapAuthMiddleware>) =
    let keyCacheSemaphore = new SemaphoreSlim(1, 1)
    let mutable cachedSigningKeys: (SecurityKey list * DateTimeOffset) option = None

    let defaultValidateJwt = true
    let defaultJwtAssertionHeader = "X-Goog-Iap-Jwt-Assertion"
    let defaultJwtIssuer = "https://cloud.google.com/iap"
    let defaultJwtCertsUrl = "https://www.gstatic.com/iap/verify/public_key-jwk"
    let defaultPlatformJwtAudienceSetting = "IAP_JWT_AUDIENCE"
    let defaultEmailHeader = "X-Goog-Authenticated-User-Email"
    let defaultNameHeader = "X-Goog-Authenticated-User-Name"
    let defaultPictureHeader = "X-Goog-Iap-Attr-Picture"

    let extractHeader (headerKey: string) (context: HttpContext) : string option =
        match context.Request.Headers.TryGetValue headerKey with
        | true, values when values.Count > 0 ->
            let value = values.[0]

            if String.IsNullOrWhiteSpace value then None else Some value
        | _ -> None

    let getConfiguredString (configuration: IConfiguration) (key: string) =
        configuration[key]
        |> Option.ofObj
        |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some value)

    let getJwtAudience (configuration: IConfiguration) =
        getConfiguredString configuration "Auth:IAP:JwtAudience"
        |> Option.orElseWith (fun () -> getConfiguredString configuration defaultPlatformJwtAudienceSetting)

    let parseEmailValue (rawValue: string) =
        let separatorIndex = rawValue.IndexOf(":")

        if separatorIndex >= 0 && separatorIndex < rawValue.Length - 1 then
            rawValue.Substring(separatorIndex + 1)
        else
            rawValue

    let tryParseBool (value: string option) (fallbackValue: bool) =
        match value with
        | Some rawValue ->
            match Boolean.TryParse rawValue with
            | true, parsedValue -> parsedValue
            | false, _ -> fallbackValue
        | None -> fallbackValue

    let findEmailClaim (principal: ClaimsPrincipal) =
        [ "email"; ClaimTypes.Email ]
        |> List.tryPick (fun claimType ->
            principal.FindFirst(claimType)
            |> Option.ofObj
            |> Option.map (fun claim -> claim.Value))
        |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some value)

    let getCachedSigningKeys () =
        let now = DateTimeOffset.UtcNow

        match cachedSigningKeys with
        | Some(signingKeys, expiresAt) when expiresAt > now -> Some signingKeys
        | _ -> None

    let refreshSigningKeysAsync (httpClientFactory: IHttpClientFactory) (jwtCertsUrl: string) = task {
        try
            use client = httpClientFactory.CreateClient()
            let! response = client.GetAsync(jwtCertsUrl)
            response.EnsureSuccessStatusCode() |> ignore
            let! content = response.Content.ReadAsStringAsync()

            let keySet = JsonWebKeySet(content)
            let signingKeys = keySet.GetSigningKeys() |> Seq.toList

            if List.isEmpty signingKeys then
                return Error "No signing keys returned by IAP certs endpoint"
            else
                cachedSigningKeys <- Some(signingKeys, DateTimeOffset.UtcNow.AddHours(1.0))
                return Ok signingKeys
        with ex ->
            return Error ex.Message
    }

    let getSigningKeysAsync (httpClientFactory: IHttpClientFactory) (jwtCertsUrl: string) = task {
        match getCachedSigningKeys () with
        | Some signingKeys -> return Ok signingKeys
        | None ->
            do! keyCacheSemaphore.WaitAsync()

            try
                match getCachedSigningKeys () with
                | Some signingKeys -> return Ok signingKeys
                | None -> return! refreshSigningKeysAsync httpClientFactory jwtCertsUrl
            finally
                keyCacheSemaphore.Release() |> ignore
    }

    let validateJwtToken
        (jwtAssertion: string)
        (jwtAudience: string)
        (jwtIssuer: string)
        (signingKeys: SecurityKey list)
        =
        try
            let handler = JwtSecurityTokenHandler()
            handler.MapInboundClaims <- false

            let parsedToken = handler.ReadJwtToken(jwtAssertion)

            let isSupportedAlgorithm =
                parsedToken.Header.Alg = SecurityAlgorithms.EcdsaSha256
                || parsedToken.Header.Alg = SecurityAlgorithms.RsaSha256

            if not isSupportedAlgorithm then
                Error $"Unsupported JWT algorithm '{parsedToken.Header.Alg}'. Expected ES256 or RS256."
            else
                let validationParameters = TokenValidationParameters()
                validationParameters.ValidateIssuerSigningKey <- true
                validationParameters.IssuerSigningKeys <- signingKeys
                validationParameters.ValidateIssuer <- true
                validationParameters.ValidIssuer <- jwtIssuer
                validationParameters.ValidateAudience <- true
                validationParameters.ValidAudience <- jwtAudience
                validationParameters.ValidateLifetime <- true
                validationParameters.RequireSignedTokens <- true
                validationParameters.RequireExpirationTime <- true
                validationParameters.ClockSkew <- TimeSpan.FromMinutes(2.0)

                let mutable validatedToken: SecurityToken = null

                let principal =
                    handler.ValidateToken(jwtAssertion, validationParameters, &validatedToken)

                Ok principal
        with ex ->
            Error ex.Message

    let validateIapJwtAsync (context: HttpContext) (configuration: IConfiguration) = task {
        let jwtAssertionHeader =
            configuration["Auth:IAP:JwtAssertionHeader"]
            |> Option.ofObj
            |> Option.defaultValue defaultJwtAssertionHeader

        let jwtAudience = getJwtAudience configuration

        let jwtIssuer =
            configuration["Auth:IAP:JwtIssuer"]
            |> Option.ofObj
            |> Option.defaultValue defaultJwtIssuer

        let jwtCertsUrl =
            configuration["Auth:IAP:JwtCertsUrl"]
            |> Option.ofObj
            |> Option.defaultValue defaultJwtCertsUrl

        match extractHeader jwtAssertionHeader context with
        | None -> return Error(MissingJwtHeader jwtAssertionHeader)
        | Some jwtAssertion ->
            match jwtAudience with
            | None
            | Some "" -> return Error(Misconfigured "Auth:IAP:JwtAudience is required when JWT validation is enabled")
            | Some audience when String.IsNullOrWhiteSpace audience ->
                return Error(Misconfigured "Auth:IAP:JwtAudience is required when JWT validation is enabled")
            | Some audience ->
                let httpClientFactory =
                    context.RequestServices.GetRequiredService<IHttpClientFactory>()

                let! signingKeysResult = getSigningKeysAsync httpClientFactory jwtCertsUrl

                match signingKeysResult with
                | Error message -> return Error(KeyFetchFailed message)
                | Ok signingKeys ->
                    return
                        match validateJwtToken jwtAssertion audience jwtIssuer signingKeys with
                        | Ok principal -> Ok(Some principal)
                        | Error message -> Error(InvalidToken message)
    }

    member _.InvokeAsync(context: HttpContext) : Task = task {
        let requestPath = context.Request.Path.Value

        if
            not (String.IsNullOrWhiteSpace(requestPath))
            && String.Equals(requestPath, "/healthy", StringComparison.OrdinalIgnoreCase)
        then
            do! next.Invoke(context)
        else
            let configuration = context.RequestServices.GetRequiredService<IConfiguration>()

            let validateJwt =
                configuration["Auth:IAP:ValidateJwt"]
                |> Option.ofObj
                |> fun value -> tryParseBool value defaultValidateJwt

            let! validatedPrincipalResult =
                if validateJwt then
                    validateIapJwtAsync context configuration
                else
                    Task.FromResult(Ok None)

            match validatedPrincipalResult with
            | Error(MissingJwtHeader headerName) ->
                context.Response.StatusCode <- StatusCodes.Status401Unauthorized
                do! context.Response.WriteAsync $"Unauthorized: Missing or invalid {headerName}"
            | Error(Misconfigured errorMessage) ->
                logger.LogError("IAP JWT validation is misconfigured: {Error}", errorMessage)
                context.Response.StatusCode <- StatusCodes.Status500InternalServerError
                do! context.Response.WriteAsync "Internal Server Error: IAP JWT validation is misconfigured"
            | Error(KeyFetchFailed errorMessage) ->
                logger.LogError("Failed to fetch IAP signing keys: {Error}", errorMessage)
                context.Response.StatusCode <- StatusCodes.Status500InternalServerError
                do! context.Response.WriteAsync "Internal Server Error: Failed to validate IAP JWT assertion"
            | Error(InvalidToken errorMessage) ->
                logger.LogWarning("IAP JWT assertion validation failed: {Error}", errorMessage)
                context.Response.StatusCode <- StatusCodes.Status401Unauthorized
                do! context.Response.WriteAsync "Unauthorized: Invalid IAP JWT assertion"
            | Ok validatedPrincipalOption ->
                let emailHeader =
                    configuration["Auth:IAP:EmailHeader"]
                    |> Option.ofObj
                    |> Option.defaultValue defaultEmailHeader

                let nameHeader =
                    configuration["Auth:IAP:NameHeader"]
                    |> Option.ofObj
                    |> Option.defaultValue defaultNameHeader

                let pictureHeader =
                    configuration["Auth:IAP:PictureHeader"]
                    |> Option.ofObj
                    |> Option.defaultValue defaultPictureHeader

                match extractHeader emailHeader context with
                | None ->
                    context.Response.StatusCode <- StatusCodes.Status401Unauthorized
                    do! context.Response.WriteAsync $"Unauthorized: Missing or invalid {emailHeader}"
                | Some rawEmail ->
                    let email = parseEmailValue rawEmail

                    match validatedPrincipalOption |> Option.bind findEmailClaim with
                    | Some tokenEmail when not (email.Equals(tokenEmail, StringComparison.OrdinalIgnoreCase)) ->
                        logger.LogWarning(
                            "IAP JWT email claim mismatch. Header email: {HeaderEmail}, token email: {TokenEmail}",
                            email,
                            tokenEmail
                        )

                        context.Response.StatusCode <- StatusCodes.Status401Unauthorized
                        do! context.Response.WriteAsync "Unauthorized: IAP identity mismatch"
                    | _ ->
                        RequestUserContext.set context {
                            Name = extractHeader nameHeader context
                            Email = email
                            Profile = extractHeader pictureHeader context
                            AuthenticationSource = "iap"
                        }

                        do! next.Invoke(context)
    }