# FsharpStarter Agent Skills

Shared internal-tools skills are now served by the deployed `internal-tools-mcp` server configured in [`.codex/config.toml`](../.codex/config.toml) and [`.mcp.json`](../.mcp.json).

## Local Skills

- `deploy-github-actions`: repo-specific GitHub Actions deployment guidance

The starter should not keep shared local skills. They must come from the `internal-tools` MCP server instead.

## Standard Verification Loop
After each significant change:
1. `dotnet tool run fantomas .`
2. `dotnet build FsharpStarter.sln -c Release`
3. `dotnet test FsharpStarter.sln`
4. `cd www && npm run check && npm run lint && npm test`
