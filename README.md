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
- Frontend checks/tests and reusable UI components
- Optional AI skills in `.agents/skills` for common advanced workflows

## Solution
- `FsharpStarter.sln`
- `src/FsharpStarter.Domain`
- `src/FsharpStarter.Application`
- `src/FsharpStarter.Infrastructure`
- `src/FsharpStarter.Api`

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

## Layer Rules
- Domain: pure business logic, no framework/network/database dependencies.
- Application: commands, DTOs, orchestration, interfaces.
- Infrastructure: EF Core, SQLite, external integrations.
- API: controllers, middleware, DI composition.

## Skills
Optional capabilities are documented under `.agents/skills`:
- `entity-framework-fsharp`
- `new-controller`
- `event-sourcing-audit`
- `otel-tracing`
- `fsharp-debugger`
- `iap-auth`
- `openfga`
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
5. Run `review-backend` skill on backend changes (`src/FsharpStarter.Domain`, `src/FsharpStarter.Application`, `src/FsharpStarter.Infrastructure`, `src/FsharpStarter.Api`).
6. Run `review-frontend` skill on frontend changes (`www/`).

## Template Reuse Checklist
When copying this repo for a new project:
1. Pick your project name and replace `FsharpStarter` / `fsharp-starter` tokens.
2. Update deploy/environment values (domain names, image names, cloud project IDs).
3. Create your own `infra/foundation/opentofu/terraform.tfvars` from `infra/foundation/opentofu/terraform.tfvars.example`.
4. Create your own `infra/opentofu/terraform.tfvars` from `infra/opentofu/environments/dev/terraform.tfvars.example`.
5. Run `scripts/template-sanity-check.sh` and fix anything it reports.

## Infrastructure Stacks
- `infra/foundation/opentofu`: bootstrap resources that must exist before the main app stack can use remote state or CI deploy identity
- `infra/opentofu`: the app/runtime stack that owns network, compute, load balancing, Artifact Registry, secrets wiring, and blue/green resources

Foundation should be applied first. The app stack should keep using a GCS backend config sourced from the foundation bucket.

Example bootstrap flow:

```bash
cp infra/foundation/opentofu/terraform.tfvars.example infra/foundation/opentofu/terraform.tfvars
cd infra/foundation/opentofu
tofu init -backend=false
tofu apply

cp backend.hcl.example backend.hcl
tofu init -migrate-state -backend-config=backend.hcl

cd ../../opentofu
cp backend.hcl.example backend.hcl
cp environments/dev/terraform.tfvars.example terraform.tfvars
tofu init -backend-config=backend.hcl
tofu apply
```

## Template Guardrail Script
```bash
scripts/template-sanity-check.sh
```

This script fails if it finds:
- Legacy copied-project markers
- Local deploy/state artifacts that should not be part of the template (`terraform.tfstate*`, `terraform.tfvars`, `backend.hcl`, `www/dist`)
