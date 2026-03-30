# FsharpStarter Agent Skills

Shared internal-tools workflows are checked in here as thin `SKILL.md` stubs that delegate to the deployed `internal-tools-mcp` server configured in [`.codex/config.toml`](../.codex/config.toml) and [`.mcp.json`](../.mcp.json).

## Shared Stubs

- Use the matching shared stub before controller/endpoint, persistence, migration, infra, deploy, secret, OpenAPI, and review work.
- If the right shared workflow is not obvious, call `internal-tools.recommend_workflows` first, then `internal-tools.use_workflow` for the top match before editing.
- After loading a primary workflow, also consult related shared stubs such as `domain-driven-design`, `event-sourcing-audit`, and `otel-tracing` when they exist in this repo and the task touches business rules, audit/events, or new request paths.
- Keep `deploy-github-actions` as the full repo-local template skill until the shared MCP version is promoted.

## Standard Verification Loop
After each significant change:
1. `dotnet tool run fantomas .`
2. `dotnet build FsharpStarter.sln -c Release`
3. `dotnet test FsharpStarter.sln`
4. `cd www && npm run check && npm run lint && npm test`
