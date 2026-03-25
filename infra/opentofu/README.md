# App Repo OpenTofu

This stack is the app-side companion to `../internal-tools-infra`.

It intentionally does **not** create cluster, namespace, ingress, DNS, IAP, service accounts, PVCs, services, network policies, or `platform-contract`. Those are platform-owned resources created from the shared app catalog.

This stack manages only app-owned deployment resources:

- one `StatefulSet` that runs in the platform-created namespace
- one optional app-owned `ConfigMap` exposed through `envFrom`

## Expected Input

Get the contract for your app from the shared infra repo:

```bash
tofu -chdir=../internal-tools-infra/platform/apps output -json app_contracts
```

Keep the committed, non-secret `infra/opentofu/terraform.tfvars` up to date and copy the entry for your app into `platform_contract`.

The values that matter here are:

- `namespace`
- `runtime_service_account`
- `service_name`
- `pvc_name`
- `health_check_path`
- `runtime_contract_config_map`
- `secret_provider_class`
- `artifact_registry_repo`
- `state_bucket_name`
- `iap_jwt_audience`
- `required_pod_labels`

## Backend

The shared platform creates a per-app GCS bucket for this repo's state. Populate [`backend.gcs.hcl.example`](./backend.gcs.hcl.example) with `state_bucket_name`, then initialize with:

```bash
tofu init -migrate-state -backend-config=backend.gcs.hcl.example
```

For syntax-only validation without a configured backend, use:

```bash
tofu init -backend=false
```

## Runtime Contract

The deployed workload is expected to:

- run in the platform-created `app-*` namespace
- use `serviceAccountName: runtime`
- mount the platform PVC
- expose the platform-required pod labels
- answer the platform health check path on port `8080`
- mount the platform-managed `SecretProviderClass` when the contract declares one

The `platform-contract` ConfigMap is injected with `envFrom` exactly as the shared platform docs describe.

## Deploy

After the stack has been initialized, the normal app repo deployment path is:

```bash
scripts/deploy-app-from-tofu.sh
```

Local prerequisites for cluster deploys:

- `kubectl` configured for the shared GKE cluster
- `gke-gcloud-auth-plugin` installed so `kubectl` can authenticate through the generated kubeconfig

The script:

- builds and pushes the app image to the per-app Artifact Registry repo
- syncs `TFVARS_PATH` to the selected immutable `image_tag` when that file exists
- applies `infra/opentofu` with the selected `image_tag`
- waits for the `StatefulSet` rollout in the platform-created namespace

Useful overrides:

```bash
IMAGE_TAG=$(git rev-parse --short HEAD) scripts/deploy-app-from-tofu.sh
PUBLISH_LATEST=true scripts/deploy-app-from-tofu.sh
TFVARS_PATH=infra/opentofu/environments/dev/terraform.tfvars.example scripts/deploy-app-from-tofu.sh -var-file=environments/dev/terraform.tfvars.example
```

## App-Owned Changes

In app repos, the normal changes are:

- update `image_tag` to roll out a new container image
- add or change `app_config` keys for non-secret runtime config
- copy refreshed contract values after shared-platform changes

If the app needs namespace shape changes such as storage size, runtime secrets, IAP access members, deployer subjects, or egress exceptions, make those changes in `../internal-tools-infra`, not here.
