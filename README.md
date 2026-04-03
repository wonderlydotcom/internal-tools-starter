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
- Optional AI workflows via the shared `internal-tools` MCP server plus repo-local template skills when needed

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

## Shared MCP Skills

Shared internal-tools skills are now served by the deployed `internal-tools-mcp` server.

- Codex reads [`.codex/config.toml`](./.codex/config.toml).
- Claude Code reads [`.mcp.json`](./.mcp.json) and [`.claude/settings.json`](./.claude/settings.json).
- No bearer token or local secret bootstrap is required before starting either client.
- Generic shared workflows, including deploy, are surfaced locally as thin `.agents/skills/*/SKILL.md` stubs.

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

## Workflow
After each meaningful change:
1. Create a pull request
2. Run the shared `review-backend` skill from the `internal-tools` MCP server on backend changes (`src/FsharpStarter.Domain`, `src/FsharpStarter.Application`, `src/FsharpStarter.Infrastructure`, `src/FsharpStarter.Api`, `src/FsharpStarter.McpServer`).
3. Run the shared `review-frontend` skill from the `internal-tools` MCP server on frontend changes (`www/`).
4. Run `./scripts/signoff-pr.sh` without any arguments - this script runs all validation needed
5. Address any feedback from the script (usually some formatting fails or maybe lint rules)
6. Re-run `./scripts/signoff-pr.sh`
7. Squash merge the PR

## Template Reuse Checklist
When copying this repo for a new project:
1. Pick your project name and replace `FsharpStarter` / `fsharp-starter` tokens.
2. Update deploy/environment values (domain names, image names, cloud project IDs).
   `platform_contract.domain_name` is shared-platform routing data and may later change independently from the repo slug or foundation deploy identity.
3. Update the committed `infra/opentofu/terraform.tfvars` from `infra/opentofu/environments/dev/terraform.tfvars.example` and keep only non-secret values there.
4. Point `infra/opentofu/backend.gcs.hcl.example` at the `state_bucket_name` from `../internal-tools-infra/platform/apps`.
5. Install `gke-gcloud-auth-plugin` before cluster deploys so `kubectl` can authenticate to GKE through kubeconfig.
6. Use `scripts/deploy-app-from-tofu.sh` for image build, push, and rollout after the shared app contract exists.
7. Confirm the shared platform owns your `artifact_registry_repo` lifecycle and enforces the standard cleanup policy for that repo.
8. Keep the committed MCP wiring and only use local `.agents/skills` for repo-specific capabilities.
If you also use the optional bootstrap stack in `infra/foundation/opentofu`, follow the bootstrap and backend-migration steps in `infra/foundation/opentofu/README.md`.
