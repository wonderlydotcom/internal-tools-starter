# FsharpStarter Agent Skills

Reusable implementation guides for optional capabilities in this template.

## Skills
- `add-secret`: End-to-end runtime secret delivery across app repo, shared infra repo, backend startup, and deploy/runtime verification.
- `iap-auth`: Google Cloud IAP authn/authz integration and local dev fallback.
- `gcp-deploy`: GCE deployment summary from `docker-compose.gce.yml` and `infra/opentofu`.
- `openfga`: Relationship-based authorization with OpenFGA and architecture placement.
- `entity-framework-fsharp`: EF Core + SQLite patterns for F# records, DU/option/result conversion.
- `new-controller`: End-to-end controller workflow from domain change to API endpoint.
- `event-sourcing-audit`: Transactional event persistence and audit display integration.
- `otel-tracing`: AutoTracing + OpenTelemetry wiring for handlers/controllers.

## Standard Verification Loop
After each significant change:
1. `dotnet tool run fantomas .`
2. `dotnet build FsharpStarter.sln -c Release`
3. `dotnet test FsharpStarter.sln`
4. `cd www && npm run check && npm run lint && npm test`
