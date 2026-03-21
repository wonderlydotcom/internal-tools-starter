---
name: add-secret
description: Add or update secret-backed settings end-to-end for this starter repo, including app-side OpenTofu, shared-platform changes in ../internal-tools-infra, backend runtime changes under src/, and any required deploy/CI wiring. Use when a token, private key, client secret, certificate, or other sensitive value must be introduced or when runtime startup fails because a mounted secret or secret-backed config key is missing.
---

# Add Secret

## Overview

Use this skill to add a new runtime secret without leaking values into git, `appsettings*.json`, `terraform.tfvars`, Docker images, or GitHub Actions vars.

Drive the change through four surfaces in order:

1. shared-platform secret delivery in `../internal-tools-infra`
2. app-repo deploy config in `infra/opentofu`
3. backend runtime consumption under `src/`
4. one-time secret value upload to GCP Secret Manager, plus CI secrets only if the workflow itself truly needs them

Read [references/repo-secret-surfaces.md](references/repo-secret-surfaces.md) before making changes.

## Workflow

### 1. Classify The Inputs

- Secret: API token, private key, client secret, PEM cert, refresh token, webhook signing secret.
- Config: hostname, namespace, queue name, timeout, feature flag, file path to a mounted secret.
- Split secret vs config before touching code. Most mistakes come from mixing them.

### 2. Patch `../internal-tools-infra` First

- This repo does not own Secret Manager secret containers.
- Add or update `secret_files` in `../internal-tools-infra/catalog/apps/<app-id>.yaml`.
- Reconcile `../internal-tools-infra/platform/apps` or `../internal-tools-infra/platform/app` so the platform creates:
  - Secret Manager secret containers
  - runtime accessor IAM
  - the namespace `SecretProviderClass`
- Remember:
  - `tofu plan` previews only
  - `tofu apply` creates the secret plumbing
  - neither step uploads secret values
- If the current environment cannot write `../internal-tools-infra`, ask for approval before editing there.

### 3. Patch This Repo's `infra/opentofu` Only For Non-Secret Wiring

- Put only non-secret deployment config in `infra/opentofu`.
- Common changes here are:
  - `app_config` entries for non-secret env vars
  - refreshed `platform_contract` values if the shared contract changed
  - docs/examples when the new secret changes the expected runtime shape
- Never put the secret value itself in:
  - `terraform.tfvars`
  - `app_config`
  - `appsettings*.json`
  - Dockerfile args/env
  - GitHub Actions vars

### 4. Patch Backend Runtime Under `src/`

- Choose the runtime consumption pattern deliberately.

Pattern A: Key-per-file for standard application settings
- Use when the app wants a normal config key such as `Section:Subsection:Secret`.
- Add `builder.Configuration.AddKeyPerFile("/var/run/secrets/app", true)` to every relevant entry point.
- Use `.NET` file names in `secret_files`, e.g. `ExternalService__ApiKey`.
- Bind typed options and fail fast with validation on startup.

Pattern B: Direct file/path consumption for certs and SDKs
- Use when the library naturally wants PEM/file contents or the mounted file path is the stable contract.
- Keep the file path as non-secret config in `app_config`.
- Teach the runtime code to read the mounted file directly.
- Use this pattern for PEM-backed SDKs, mTLS clients, and other consumers that naturally want mounted files or file contents.

- Apply the same pattern to both API and worker processes when both consume the secret.
- Startup must fail clearly when a required secret is missing.

### 5. Handle CI/CD And Docker Only If Necessary

- Default rule: runtime app secrets belong only in GCP Secret Manager.
- Do not copy runtime-only secrets into GitHub Actions unless the workflow itself needs them during build or deploy.
- Add a GitHub Actions secret only when CI/CD truly needs the secret outside the running app.
- If CI needs one, use `gh secret set` or the repo settings UI.
- If only non-secret `terraform.tfvars` changed, refresh `TOFU_TFVARS_BASE64` with `scripts/encode-tofu-tfvars-base64.sh`.
- If you changed appsettings defaults, confirm the published image still includes those files.

### 6. Prompt For One-Time Secret Upload

- Once the Secret Manager containers exist, stop and tell the user you are ready for the one-time paste step.
- Say explicitly which secret ids you will upload.
- Then run `scripts/upload_gcp_secret_versions.py` in a TTY.
- That script prompts the user to paste each secret value once and uploads a new version to GCP Secret Manager.
- Do not ask the user to edit files with secret values or commit them anywhere.

### 7. Verify

- App repo:
  - `dotnet build FsharpStarter.sln -c Release`
  - `dotnet test FsharpStarter.sln`
- App-side OpenTofu when touched:
  - `tofu fmt -recursive`
  - `tofu validate`
- Shared infra repo when touched:
  - relevant `tofu plan`
  - relevant `tofu apply` if the user asked for it and credentials are available
- Deploy/runtime when touched:
  - `./scripts/deploy-app-from-tofu.sh`
  - `kubectl -n <namespace> rollout status statefulset/app`
  - `kubectl -n <namespace> logs statefulset/app --all-containers --tail=200`

## Output Requirements

- Report the exact secret ids added or changed.
- Report every non-secret config key added alongside them.
- State which of the four surfaces changed:
  - `../internal-tools-infra`
  - `infra/opentofu`
  - `src/`
  - CI/CD
- State whether the one-time GCP Secret Manager upload was completed.
- State whether any GitHub Actions secret was added, and why it was actually necessary.
