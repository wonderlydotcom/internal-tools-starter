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

## Shared MCP Skills

Shared internal-tools skills are now served by the deployed `internal-tools-mcp` server.

- Codex reads [`.codex/config.toml`](./.codex/config.toml).
- Claude Code reads [`.mcp.json`](./.mcp.json) and [`.claude/settings.json`](./.claude/settings.json).
- Set `INTERNAL_TOOLS_MCP_BEARER_TOKEN` before starting either client.
- Keep `deploy-github-actions` as the repo-local template skill.
- Treat the remaining shared folders under `.agents/skills` as transitional duplicates until the deletion wave.

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

## Template Reuse Checklist
When copying this repo for a new project:
1. Pick your project name and replace `FsharpStarter` / `fsharp-starter` tokens.
2. Update deploy/environment values (domain names, image names, cloud project IDs).
3. Update the committed `infra/opentofu/terraform.tfvars` from `infra/opentofu/environments/dev/terraform.tfvars.example` and keep only non-secret values there.
4. Point `infra/opentofu/backend.gcs.hcl.example` at the `state_bucket_name` from `../internal-tools-infra/platform/apps`.
5. Use `scripts/deploy-app-from-tofu.sh` for image build, push, and rollout after the shared app contract exists.
6. Keep the committed MCP wiring and only use local `.agents/skills` for repo-specific capabilities.
7. Run `scripts/template-sanity-check.sh` and fix anything it reports.
If you also use the optional bootstrap stack in `infra/foundation/opentofu`, create its `terraform.tfvars` from the committed example before applying it.

## Template Guardrail Script
```bash
scripts/template-sanity-check.sh
```

This script fails if it finds:
- Legacy copied-project markers
- Local deploy/state artifacts that should not be part of the template (`terraform.tfstate*`, `backend.hcl`, `www/dist`)
