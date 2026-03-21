# Repo Secret Surfaces

Use this file to rebuild context for secret work without searching the whole tree.

## 1. App Repo: `internal-tools-starter`

### App-side OpenTofu

- `infra/opentofu/main.tf`
  - mounts the platform `SecretProviderClass` when `platform_contract.secret_provider_class` is set
  - exposes `app_config` through `envFrom`
- `infra/opentofu/variables.tf`
  - `runtime_secrets_mount_path`
  - `app_config`
  - `platform_contract`
- `infra/opentofu/README.md`
  - current deployment contract for starter-derived app repos
- `infra/opentofu/environments/dev/terraform.tfvars.example`
  - example `app_config` shape

### Backend entry points

- `src/FsharpStarter.Api/src/Program.fs`
  - API startup
  - no generic `AddKeyPerFile` call exists today
- If the derived app later adds workers or other entry points, apply the same secret-loading pattern there too.

### Docker and CI/CD

- `.github/workflows/deploy.yml`
  - check whether repo vars such as `TOFU_TFVARS_BASE64` need refresh after non-secret tfvars changes
- `scripts/encode-tofu-tfvars-base64.sh`
  - refreshes `TOFU_TFVARS_BASE64` when non-secret tfvars change
- `src/FsharpStarter.Api/Dockerfile`
  - confirm appsettings defaults are still published if changed

## 2. Shared Platform Repo: `../internal-tools-infra`

### App catalog and onboarding docs

- `../internal-tools-infra/catalog/apps/<app-id>.yaml`
  - `secret_files` declares which secret ids should exist and what file names they mount as
- `../internal-tools-infra/catalog/apps/README.md`
  - catalog field reference
- `../internal-tools-infra/docs/app-onboarding.md`
  - explains `secret_files`
  - confirms app workloads mount secrets through `SecretProviderClass app-secrets`

### Infra implementation

- `../internal-tools-infra/modules/app/main.tf`
  - `google_secret_manager_secret.runtime`
  - `google_secret_manager_secret_iam_member.runtime_accessor`
  - `kubernetes_manifest.secret_provider_class`
- `../internal-tools-infra/platform/apps/main.tf`
  - passes `secret_files` from the fleet catalog into the app module
- `../internal-tools-infra/platform/app/main.tf`
  - same for single-app state scope

### Secret value upload pattern

- `../internal-tools-infra/README.md`
  - states the default approach: keep secrets in Google Secret Manager and mount them as files
- `../internal-tools-infra/docs/operations.md`
  - shows the canonical `gcloud secrets versions add ...` pattern

## 3. Runtime Pattern Decision

### Prefer key-per-file when:

- the app wants a normal config key
- typed options + validation are appropriate
- a `.NET` config section should read the secret directly

Typical file name:

```text
ExternalService__ApiKey
```

### Prefer direct file/path when:

- the consumer naturally wants a file path
- the secret is multiline PEM or similar
- path stability is the important runtime contract

Typical file names:

```text
external-service-cert.pem
external-service-key.pem
```

## 4. GitHub Actions Rule

Add a GitHub Actions secret only when the workflow itself needs the secret during CI/CD.

Do not mirror runtime-only app secrets into GitHub if the running workload can read them from GCP Secret Manager.
