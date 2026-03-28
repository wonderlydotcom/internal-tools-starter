# Foundation OpenTofu

This stack bootstraps the repo-owned Google Cloud identity used by GitHub Actions deployments.

It creates:

- one GCS bucket for the foundation stack's own remote state
- one repo-specific GitHub Actions Workload Identity Provider attached to the shared pool
- one deploy service account for this repository

It reuses the existing shared Workload Identity Pool `github-actions`; it does not create a new project-global pool per repo.

It does not create the app-owned state bucket used by `infra/opentofu`. The app stack keeps its own remote backend target for this repo.

## Existing Clickops Resources

If the deploy service account, the Workload Identity provider, or the `iac-state-<app>` bucket already exist, import them before the first apply instead of recreating them.

Typical imports:

```bash
tofu import google_storage_bucket.terraform_state iac-state-my-internal-tool
tofu import google_service_account.deploy projects/wonderly-idp-sso/serviceAccounts/my-internal-tool-deploy@wonderly-idp-sso.iam.gserviceaccount.com
tofu import google_iam_workload_identity_pool_provider.github_actions projects/199626281531/locations/global/workloadIdentityPools/github-actions/providers/my-internal-tool
```

## Usage

First-time bootstrap must start from local state because this stack creates its own backend bucket.

1. Copy `terraform.tfvars.example` to `terraform.tfvars`.
2. Apply once from local state with `tofu init -backend=false -input=false` and `tofu apply`, or import existing resources into that local state.
3. Create `backend.hcl` from `backend.hcl.example`.
4. Run `tofu init -migrate-state -force-copy -input=false -backend-config=backend.hcl`.

After that migration, normal commands can run directly from `infra/foundation/opentofu`.

## Outputs

Use these outputs to keep the GitHub repository variables in `Settings -> Secrets and variables -> Actions` in sync:

- `project_id` -> `GCP_PROJECT_ID`
- `github_workload_identity_provider_name` -> `GCP_WORKLOAD_IDENTITY_PROVIDER`
- `github_deploy_service_account_email` -> `GCP_DEPLOY_SERVICE_ACCOUNT`
- `app_catalog_deployer_subject` -> copy into `deployer_subjects` in `../internal-tools-infra/catalog/apps/<app>.yaml`

The app deploy workflow still needs the shared cluster and app-contract values:

- `GKE_CLUSTER_NAME`
- `GKE_CLUSTER_LOCATION`

The app deploy workflow reads committed `infra/opentofu/terraform.tfvars` directly from checkout.
