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
- EF Core + SQLite repository implementation
- Test projects wired into the solution
- Frontend checks/tests and reusable UI components
- Optional AI workflows via the shared `internal-tools` MCP server plus repo-local template skills when needed

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

## Shared MCP Skills

Shared internal-tools skills are served by the deployed `internal-tools-mcp` server.

- Codex reads [`.codex/config.toml`](./.codex/config.toml).
- Claude Code reads [`.mcp.json`](./.mcp.json) and [`.claude/settings.json`](./.claude/settings.json).
- No bearer token or local secret bootstrap is required before starting either client.
- Shared internal-tools workflows are now surfaced locally as thin `.agents/skills/*/SKILL.md` stubs that delegate to `internal-tools.use_workflow`.
- If the right shared workflow is not obvious, call `internal-tools.recommend_workflows` first, then call `internal-tools.use_workflow` for the top match before editing.
- Use `app-observability` for owner-safe app telemetry triage through `internal-tools logs`, `internal-tools traces`, `internal-tools metrics`, and `internal-tools alerts` before reaching for backend-specific debugging.
- Before editing controllers or endpoints, load `new-controller` first.
- Before editing EF Core mappings, repositories, or `DbContext` code, load `entity-framework-fsharp` first.
- Before editing schema or migration code, load `db-migrations` first.
- Consult the matching shared stub before infra, deploy, secret, OpenAPI, or review work when the task clearly maps to one of those workflows.
- After loading a primary workflow, also consult related shared stubs such as `domain-driven-design`, `event-sourcing-audit`, and `otel-tracing` when they exist in this repo and the task touches business rules, audit/events, or new request paths.

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
5. Run the shared `review-backend` skill from the `internal-tools` MCP server on backend changes (`src/FsharpStarter.Domain`, `src/FsharpStarter.Application`, `src/FsharpStarter.Infrastructure`, `src/FsharpStarter.Api`).
6. Run the shared `review-frontend` skill from the `internal-tools` MCP server on frontend changes (`www/`).

## Required PR Signoff Workflow
- After making changes on a branch, run `./scripts/signoff-pr.sh` from that branch with no arguments.
- Run it even if the pull request already exists; the script handles both PR updates and the required CI signoff.
- If the script reports issues, fix them and re-run `./scripts/signoff-pr.sh` until it succeeds.
- Do not bypass this workflow with manual GitHub signoff commands or alternate PR/signoff commands.

## Template Reuse Checklist
When copying this repo for a new project:
1. Pick your project name and replace `FsharpStarter` / `fsharp-starter` tokens.
2. Update deploy/environment values (domain names, image names, cloud project IDs).
3. Update the committed `infra/opentofu/terraform.tfvars` from `infra/opentofu/environments/dev/terraform.tfvars.example` and keep only non-secret values there.
4. Point `infra/opentofu/backend.gcs.hcl.example` at the `state_bucket_name` from `../internal-tools-infra/platform/apps`.
5. Use `scripts/deploy-app-from-tofu.sh` for image build, push, and rollout after the shared app contract exists.
6. Keep the committed MCP wiring. Shared `.agents/skills` stubs, including `app-observability`, are expected alongside repo-specific full local skills.
If you also use the optional bootstrap stack in `infra/foundation/opentofu`, follow the bootstrap and backend-migration steps in `infra/foundation/opentofu/README.md`.
