---
name: deploy-github-actions
description: Create or update a GitHub Actions deployment workflow for this repository that runs automatically after pull requests are merged to the default branch. Use when Codex needs to add `.github/workflows/deploy.yml`, wire GitHub Actions variables for Workload Identity based deploys, or keep this repo's merge-to-deploy automation aligned with the shared-cluster deploy scripts and infra layout.
---

# Deploy Github Actions

Create `.github/workflows/deploy.yml` for this repo's GCP deploy path. Adapt the workflow to this repository's shared-cluster deployment shape instead of copying VM-era names or entrypoints.

## Workflow
1. Inspect the source workflow summary in `references/customer-dash-deploy.md`.
2. Inspect this repo's deploy entrypoints before editing:
   - `scripts/deploy-app-from-tofu.sh`
   - `infra/opentofu/*`
3. Create or update `.github/workflows/deploy.yml`.
4. Preserve the same control flow unless the repo layout requires a change:
   - trigger on `push` to the default branch so merged pull requests deploy automatically
   - `permissions`: `contents: read`, `id-token: write`
   - fixed concurrency group for deploys with `cancel-in-progress: false`
   - validate required GitHub Actions variables before authenticating
   - authenticate with `google-github-actions/auth@v3`
   - install `gcloud`
   - install OpenTofu
   - initialize the OpenTofu backend
   - run the repo deploy entrypoint

## Repo-Specific Rules
- Name the workflow file `.github/workflows/deploy.yml`.
- Keep the job name and step names clear, but they do not need to match `customer-dash` exactly.
- Do not hard-code the branch name without checking the repo default branch first.
- Prefer the existing deploy entrypoint `./scripts/deploy-app-from-tofu.sh`.
- Set `INFRA_DIR` to `infra/opentofu` unless the repo has moved it.
- Pass backend configuration via GitHub Actions variables, not checked-in secrets files.
- Fail early if required repo variables are missing.

## Required GitHub Variables
Keep the validation step aligned with the workflow template in `assets/deploy.yml`.

- `GCP_PROJECT_ID`
- `GCP_WORKLOAD_IDENTITY_PROVIDER`
- `GCP_DEPLOY_SERVICE_ACCOUNT`
- `TOFU_BACKEND_BUCKET`
- `TOFU_BACKEND_PREFIX`

If the repo later requires impersonation or extra backend config, add validation for those values in the same shell step.

## Implementation Notes
- Start from `assets/deploy.yml` as the base template.
- Replace only the values that are repo-specific:
  - branch name
  - concurrency group name if needed
  - any environment variables required by this repo's deploy scripts
- Keep strict shell mode in every multi-line `run` block.
- Prefer `${{ vars.NAME }}` for non-secret configuration. Only switch to `${{ secrets.NAME }}` if the deploy path genuinely requires secrets.

## Validation
After editing the workflow:
1. Read the final `.github/workflows/deploy.yml` once for YAML/indentation mistakes.
2. Confirm every env var referenced in the workflow is either defined in `env:` or provided by GitHub Actions contexts.
3. Confirm the deploy command still matches the repo's actual entrypoint.
4. If the task also asks to add the workflow, mention which repository variables must be created in GitHub before the workflow can succeed.
