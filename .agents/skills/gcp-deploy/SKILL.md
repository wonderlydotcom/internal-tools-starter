---
name: gcp-deploy
description: Use when deploying the starter into the shared internal-tools GKE platform with Artifact Registry, kubectl, and app-owned OpenTofu. For infra contract design and tenancy guardrails, use the iac skill.
---

# Skill: gcp-deploy

Use this when deploying the starter app into the shared internal-tools GKE cluster.

This skill is deployment-operations focused. For OpenTofu infrastructure design, platform contract shape, and tenancy guardrails, defer to `iac`:
- `.agents/skills/iac/SKILL.md`

## Source Summary
This skill summarizes:
- `scripts/deploy-app-from-tofu.sh`
- `infra/opentofu/*`
- `../internal-tools-infra/docs/app-onboarding.md`

## Runtime Topology
- `../internal-tools-infra` owns namespace, service account, PVC, Service, Ingress, IAP, DNS, and network policy.
- `infra/opentofu` in this repo owns only app deployment resources inside that platform-created namespace.
- `scripts/deploy-app-from-tofu.sh` builds and pushes the image, applies `infra/opentofu`, and waits for the StatefulSet rollout.

## Data/Secrets Expectations
- The workload mounts the platform-managed PVC named by the app contract.
- SQLite stays under `/app/data` by default.
- Runtime secrets come from the platform-managed `SecretProviderClass` when the contract declares one.

## OpenTofu Summary
Main files:
- `infra/opentofu/main.tf`: app-owned StatefulSet plus optional ConfigMap.
- `infra/opentofu/variables.tf`: platform contract inputs and deployment settings.
- `infra/opentofu/providers.tf` and `versions.tf`: provider and version pins.
- `infra/opentofu/outputs.tf`: exported deployment values used by the deploy script.

## Hardening Rules
- Follow `iac` as the source of truth for platform contract ownership boundaries.
- Do not add app-repo logic that recreates namespace, ingress, IAP, DNS, Service, PVC, or NetworkPolicy resources.
- Treat deploy scripts as production code: strict shell mode, clear env overrides, explicit tool checks, and no hidden mutable state.
- Prefer immutable image tags for rollout, even if `PUBLISH_LATEST=true` is used for convenience.
- Validate rollout against the platform-created StatefulSet namespace, not a VM path.

## Deployment Flow
1. Pull the app contract from `../internal-tools-infra/platform/apps output -json app_contracts`.
2. Configure `infra/opentofu/terraform.tfvars` and backend GCS settings from that contract.
3. Run `scripts/deploy-app-from-tofu.sh`.
4. Confirm `kubectl rollout status` succeeds for the StatefulSet.
5. Verify the application responds on the platform health check path through the shared ingress.

## Verification Commands
After deployment-path edits:
1. `bash -n scripts/deploy-app-from-tofu.sh`
2. `tofu -chdir=infra/opentofu fmt -recursive`
3. `tofu -chdir=infra/opentofu validate`
4. `scripts/deploy-app-from-tofu.sh --help`
