# AGENTS.md

FsharpStarter is an opinionated starter template for new internal products.

It intentionally standardizes on:
- Backend: F# + ASP.NET Core
- Persistence: Entity Framework Core + SQLite
- Architecture: Domain / Application / Infrastructure / API
- Frontend: minimal React landing page + reusable UI components (`www/`)

It is not an "anything goes" scaffold.

## What You Get
- A canonical backend vertical slice with one controller (`GET` + `POST`)
- Event-sourced domain aggregate + persisted domain events
- EF Core + SQLite repository implementation with DBUp-managed SQL migrations
- Test projects wired into the solution
- Optional stdio MCP server host using the official .NET SDK
- Frontend checks/tests and reusable UI components
- Optional AI skills in `.agents/skills` for common advanced workflows

## Solution
- `FsharpStarter.sln`
- `src/FsharpStarter.Domain`
- `src/FsharpStarter.Application`
- `src/FsharpStarter.Infrastructure`
- `src/FsharpStarter.Api`
- `src/FsharpStarter.McpServer`

## Architecture

```text
API -> Application -> Domain
API -> Infrastructure -> Domain
API -> Domain
```

- `src/FsharpStarter.Domain`: entities, value objects, domain events, pure business logic
- `src/FsharpStarter.Application`: commands, DTOs, and handler orchestration
- `src/FsharpStarter.Infrastructure`: EF Core DbContext + repository persistence
- `src/FsharpStarter.Api`: one canonical `ExamplesController` with:
  - `GET /api/examples/{id}`
  - `POST /api/examples`
- `src/FsharpStarter.McpServer`: optional stdio MCP host built on the official .NET SDK with starter tools that call the same application layer

## Layer Rules
- Domain: pure business logic, no framework/network/database dependencies.
- Application: commands, DTOs, orchestration, interfaces.
- Infrastructure: EF Core, SQLite, external integrations.
- API: controllers, middleware, DI composition.

## Skills
Optional capabilities are documented under `.agents/skills`:
- `db-migrations`
- `entity-framework-fsharp`
- `iac`
- `new-controller`
- `event-sourcing-audit`
- `otel-tracing`
- `fsharp-debugger`
- `iap-auth`
- `openfga`
- `opentofu`
- `deploy-github-actions`
- `gcp-deploy`
- `review-backend`
- `review-frontend`

Use skills for advanced/optional features so template docs stay minimal.

## Quick Start
```bash
dotnet restore FsharpStarter.sln
dotnet build FsharpStarter.sln -c Release
dotnet run --project src/FsharpStarter.Api/FsharpStarter.Api.fsproj
```

## Optional MCP Server
If a copied repo needs MCP support, the starter includes `src/FsharpStarter.McpServer` as a separate stdio host using the official `ModelContextProtocol` .NET SDK.

Build it first so your MCP client does not see `dotnet run` build output on stdout:

```bash
dotnet build src/FsharpStarter.McpServer/FsharpStarter.McpServer.fsproj -c Release
ConnectionStrings__DefaultConnection="Data Source=/absolute/path/to/app.db" \
  dotnet src/FsharpStarter.McpServer/bin/Release/net10.0/FsharpStarter.McpServer.dll
```

Example Claude Desktop-style config:

```json
{
  "mcpServers": {
    "fsharp-starter": {
      "command": "dotnet",
      "args": [
        "/ABSOLUTE/PATH/TO/FsharpStarter.McpServer.dll"
      ],
      "env": {
        "ConnectionStrings__DefaultConnection": "Data Source=/ABSOLUTE/PATH/TO/app.db"
      }
    }
  }
}
```

The shipped tools expose the starter `ExampleHandler` so downstream repos have a concrete pattern to replace with domain-specific MCP tools.

## Core Commands
```bash
dotnet restore FsharpStarter.sln
dotnet build FsharpStarter.sln -c Release
dotnet test FsharpStarter.sln
dotnet tool run fantomas .

cd www
npm install
npm run check
npm run lint
npm test
```

## Change Validation
After each meaningful change:
1. `dotnet tool run fantomas .`
2. `dotnet build FsharpStarter.sln -c Release`
3. `dotnet test FsharpStarter.sln`
4. `cd www && npm run check && npm run lint && npm test`
5. Run `review-backend` skill on backend changes (`src/FsharpStarter.Domain`, `src/FsharpStarter.Application`, `src/FsharpStarter.Infrastructure`, `src/FsharpStarter.Api`, `src/FsharpStarter.McpServer`).
6. Run `review-frontend` skill on frontend changes (`www/`).

## Template Reuse Checklist
When copying this repo for a new project:
1. Pick your project name and replace `FsharpStarter` / `fsharp-starter` tokens.
2. Update deploy/environment values (domain names, image names, cloud project IDs).
3. Update the committed `infra/opentofu/terraform.tfvars` from `infra/opentofu/environments/dev/terraform.tfvars.example` and keep only non-secret values there.
4. Point `infra/opentofu/backend.gcs.hcl.example` at the `state_bucket_name` from `../internal-tools-infra/platform/apps`.
5. Install `gke-gcloud-auth-plugin` before cluster deploys so `kubectl` can authenticate to GKE through kubeconfig.
6. Use `scripts/deploy-app-from-tofu.sh` for image build, push, and rollout after the shared app contract exists.
7. Confirm the shared platform owns your `artifact_registry_repo` lifecycle and enforces the standard cleanup policy for that repo.
8. Run `scripts/template-sanity-check.sh` and fix anything it reports.
If you also use the optional bootstrap stack in `infra/foundation/opentofu`, create its `terraform.tfvars` from the committed example before applying it.

## Template Guardrail Script
```bash
scripts/template-sanity-check.sh
```

This script fails if it finds:
- Legacy copied-project markers
- Local deploy/state artifacts that should not be part of the template (`terraform.tfstate*`, `backend.hcl`, `www/dist`)
