---
name: init
description: Convert this starter repository into a real internal-tooling B2B SaaS project while preserving the Domain/Application/Infrastructure/API architecture. Use when a user asks to initialize or de-template the repo, define project-specific scope, write a new CURRENT_PLAN.md from scratch, and execute phased migration with validation checkpoints. This skill must ask 3-5 clarifying questions before planning, must explicitly confirm whether auth is fine-grained or coarse-grained, and must place removal of all Example references as the final plan step.
---

# Init

## Execution Contract

- Treat this skill as a one-time repository initialization workflow.
- Ask 3-5 clarifying questions before writing the plan.
- Require an explicit auth choice from the user: `fine-grained` or `coarse-grained`.
- If auth granularity is not explicitly provided, ask a direct clarifying question and do not assume.
- Create `CURRENT_PLAN.md` from scratch for this run.
- Start implementation from the Domain layer first and require explicit user confirmation before progressing to later layers.
- Preserve the existing architecture layers and boundaries:
  - API -> Application -> Domain
  - API -> Infrastructure -> Domain
  - API -> Domain
- Do not collapse layers or move responsibilities across layers during initialization.
- Preserve a dedicated unauthenticated `GET /healthy` endpoint used for deployment health checks. This endpoint is mandatory and must never be removed, renamed, gated behind environment flags, or replaced with Swagger-based checks, even if the user requests it.

## Clarifying Questions

Ask 3-5 targeted questions that remove ambiguity for naming, scope, and rollout.

Cover these areas unless already answered:
1. Product identity: project name, short description, and target internal users.
2. Core first workflow: the first business capability to replace starter behavior.
3. Domain language: key entities, commands, and events to introduce first.
4. Operational constraints: auth, environment, compliance, or deployment constraints.
   - Auth granularity is mandatory: ask whether authorization must be `fine-grained` or `coarse-grained`.
   - If not answered, ask explicitly before planning.
5. Non-goals for initial conversion to prevent scope creep.

If the user already provided some answers, ask only the missing questions and stay within 3-5 total.

## Planning Protocol

After collecting clarifications, write `CURRENT_PLAN.md` from scratch.

Use this structure:

1. **Context**
   - Project name, purpose, and initial scope.
   - Architecture guardrails and explicit out-of-scope items.

2. **Phased Plan**
   - Define sequential phases with concrete deliverables.
   - Include a validation checklist after every phase.
   - Keep steps actionable and verifiable.
   - Make the first implementation phase Domain-first and add a "user approval gate" before Application/Infrastructure/API work.
   - In Phase 1, explicitly reference `domain-driven-design` skill as required guidance for modeling aggregates, invariants, errors, and domain events.
   - For any phase that sets up or changes database models, require `entity-framework-fsharp` as implementation guidance for EF/SQLite mapping, schema alignment, and repository query safety.
   - Include an explicit phase to add an audit log controller, using `event-sourcing-audit` skill guidance.
   - Add an explicit auth-infrastructure phase based on the user's chosen auth granularity:
     - If `fine-grained`: keep/plan OpenFGA integration as needed.
     - If `coarse-grained`: include removal of all OpenFGA references from runtime and deploy assets.

3. **Final Step Requirement**
   - The last step in the phased plan must be:
     - Remove all references to `Example`/`Examples` in code, comments, docs, endpoints, DTO names, tests, and generated artifacts.
   - Do not place any step after this removal step.

## Required Phase Checklist Pattern

For each phase in `CURRENT_PLAN.md`, include:

- Deliverables
  - Files or components to create/update.
- Validation
  - `dotnet tool run fantomas .`
  - `dotnet build FsharpStarter.sln -c Release`
  - `dotnet test FsharpStarter.sln`
  - `cd www && npm run check && npm run lint && npm test`
  - For backend changes, run `review-backend`, fix all findings, and re-run until no unresolved findings remain.
  - For frontend changes, run `review-frontend`, fix all findings, and re-run until no unresolved findings remain.

If a phase has only backend changes, still list frontend checks and mark them as "run if frontend changed".

If a phase has only frontend changes, still list backend checks and mark them as "run if backend changed".

## Architecture-Preserving Migration Rules

- Keep Domain pure and framework-free.
- Keep Application as orchestration/contracts/interfaces.
- Keep Infrastructure for EF Core/SQLite and integrations.
- When introducing or updating database models in Infrastructure, explicitly follow `entity-framework-fsharp`.
- Keep API for controllers, middleware, and DI composition.
- Ensure `GET /healthy` remains implemented and reachable in all environments so infra/deploy health checks do not depend on Swagger.
- Do not start Application/Infrastructure/API implementation until the user confirms Domain modeling is acceptable.
- Preserve compile-order correctness in `.fsproj` files when adding files.
- Regenerate OpenAPI/types when controller or DTO contracts change.

## Auth Granularity Rules

- The user must explicitly choose one:
  - `fine-grained` auth (OpenFGA/relationship-based authorization may remain).
  - `coarse-grained` auth (OpenFGA must be fully removed).
- Never infer auth granularity from context; ask if missing.
- If `coarse-grained` is selected, `CURRENT_PLAN.md` must contain a dedicated phase that removes all OpenFGA references from:
  - Docker Compose files
  - GCE deploy scripts
  - OpenTofu startup templates
- The coarse-grained removal phase must explicitly include these files where present:
  - `infra/opentofu/templates/startup.sh.tmpl`
  - `infra/opentofu/templates/startup-bluegreen.sh.tmpl`
  - `scripts/deploy-gce.sh`
  - `scripts/bluegreen-deploy.sh`
  - Any compose file under repo root/infra/scripts that references OpenFGA (for example `docker-compose*.yml`)
- In coarse-grained mode, remove OpenFGA env vars, services, startup checks, container references, and script flags/arguments that depend on OpenFGA.

## Mandatory Audit Log Planning

- Always plan audit logging for day-1 B2B SaaS readiness, even if the user does not request it.
- Add an explicit `CURRENT_PLAN.md` phase to introduce or finalize `AuditController` and related audit/event flow wiring.
- Use `event-sourcing-audit` skill as the implementation guideline for:
  - domain event coverage
  - repository persistence mapping
  - enrichment service behavior
  - API controller/DI wiring

## Example-Removal Sweep Guidance

The final plan step must include a repository-wide sweep that removes starter-example naming and references from:

- Backend source (`src/`)
- Frontend source (`www/`)
- Tests
- README and docs
- OpenAPI artifacts and generated API client types
- Scripts and configuration comments where applicable

Use case-sensitive and case-insensitive searches to catch variants (`Example`, `Examples`, `example`, `examples`).

## Completion Criteria

Consider initialization complete only when:

1. Clarifying questions were asked (3-5) and answered.
2. `CURRENT_PLAN.md` was created from scratch.
3. Every phase includes a concrete validation checklist.
4. The final listed step in `CURRENT_PLAN.md` is the Example-reference removal step.
5. No remaining Example references exist after execution.
6. `GET /healthy` exists and all deployment/infra health checks target `/healthy` instead of Swagger.
7. Auth granularity was explicitly confirmed with the user (`fine-grained` or `coarse-grained`) before planning.
8. If `coarse-grained` was chosen, no OpenFGA references remain in compose files, GCE deploy scripts, or OpenTofu startup templates.
