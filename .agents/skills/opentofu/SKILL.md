---
name: opentofu
description: Work safely with the repo's GCS-backed OpenTofu state by pulling the live state into a local scratch workflow, rebuilding local `terraform.tfvars`, running iterative `tofu plan` and `tofu apply`, and then either restoring the original state or promoting the new state back to GCS. Use when changing `infra/opentofu` or `infra/foundation/opentofu`, when the operator wants a reversible local state loop before touching the shared bucket, or when recovering/restoring state from the Terraform state bucket.
---

# OpenTofu

Use this skill for stateful infra work in this repo. Keep the shared GCS backend as the source of truth, but do risky iteration against a local copy of the state first.

## Stack Map

Choose the stack before touching state:
- App stack:
  `infra/opentofu`
  bucket object: `gs://iac-state-fsharp-starter/fsharp-starter/dev/default.tfstate`
  tfvars template: `infra/opentofu/environments/dev/terraform.tfvars.example`
  backend template: `infra/opentofu/backend.hcl.example`
- Foundation stack:
  `infra/foundation/opentofu`
  bucket object: `gs://iac-state-fsharp-starter/fsharp-starter/foundation/default.tfstate`
  tfvars template: `infra/foundation/opentofu/terraform.tfvars.example`
  backend template: `infra/foundation/opentofu/backend.hcl.example`

For derived projects, keep the same layout pattern but replace `fsharp-starter` with the new project slug. The default bucket convention in this repo is `iac-state-<project-name>`. The current starter uses GCP project `wonderly-idp-sso` in region `us-central1`.

Read `references/gcs-state-workflow.md` for the exact commands.

## Workflow

1. Capture the current remote object metadata before changing anything.
   Record at least `generation`, `storage_url`, and `update_time` with `gcloud storage objects describe`.
2. Download two local copies of the remote state:
   One immutable backup named with the captured generation.
   One working copy named `terraform.tfstate`.
3. Create a scratch worktree outside the repo or in `/tmp`, then copy the target stack directory into it.
   Do not experiment directly in the shared repo directory.
4. Rebuild local `terraform.tfvars` from the committed example plus the downloaded state payload.
   For the app stack, `infra/opentofu/terraform.tfvars` is intentionally committed and must stay non-secret.
   Prefer state outputs for values that are already in state.
   Never commit `backend.hcl` or any `.tfstate` file.
5. Run the local loop from the scratch copy:
   `tofu init -backend=false`
   `tofu plan`
   `tofu apply`
6. Decide the exit path:
   Restore: push the original immutable snapshot back to the bucket.
   Promote: push the updated local `terraform.tfstate` back to the bucket.
7. Use generation-checked uploads when writing to GCS.
   Refetch the live generation immediately before upload and pass `--if-generation-match`.
   If the generation changed underneath you, stop and reconcile instead of clobbering someone else's state.
8. After the final state is in GCS, reattach the real backend in a clean working directory with `tofu init -reconfigure -backend-config=backend.hcl`.

## Tfvars Boundary

For app-stack work, treat `infra/opentofu/terraform.tfvars` as committed, reviewable config:
- commit only non-secret values such as project IDs, regions, hostnames, namespaces, Artifact Registry repo names, queue names, and mounted secret file paths or other identifiers
- never commit API tokens, passwords, private keys, PEM blocks, service account JSON, or other long-lived credentials
- route secret payloads through GCP Secret Manager and the platform-managed secret delivery path instead of placing them directly in tfvars
- keep `backend.hcl`, any `.tfstate` file, downloaded restore snapshots, and scratch artifacts uncommitted

## Local tfvars Reconstruction

Start from the committed example file for the chosen stack. Then fill what you can from the downloaded state:
- App state outputs usually provide `project_id`, `artifact_registry_location`, `artifact_registry_repo_id`, `artifact_registry_repo`, `data_mount_path`, `iap_jwt_audience`, `validate_iap_jwt`, and the `google_directory_*` values.
- Foundation state outputs usually provide `project_id`, `terraform_state_bucket_name`, `github_deploy_service_account_email`, `github_workload_identity_provider_name`, and `github_repository_name`.
- Resource names in state can confirm stable names such as the backend service, disk, MIG, VM, bucket, and service account.
- Secrets and one-off bootstrap values are not fully recoverable from state. If the value is not in outputs or resource attributes, stop and ask for it instead of guessing.

Keep the reconstruction explicit. A half-right `terraform.tfvars` is worse than a missing one.

## Restore and Promotion Rules

- Prefer GCS object generation checks for manual restore/promotion.
- Keep the original downloaded snapshot unchanged so rollback remains one command.
- If you are attached to the real backend and need an OpenTofu-native restore path, use `tofu state pull` before changes and `tofu state push` only when you intentionally want to overwrite the backend with a known-good local file.
- Treat the bucket as shared state. Never upload without first describing the current object generation.

## Safety

- Do not delete old state snapshots until the PR is merged.
- Do not run `tofu init` against the remote backend in the scratch loop.
- Do not mix app and foundation state files.
- Do not overwrite the bucket with a local file whose lineage or serial is unexpected.
- If the remote object path or bucket name changes, confirm the new layout with `gcloud storage ls --recursive` before continuing.
