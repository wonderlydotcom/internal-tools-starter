---
name: iac
description: Design and update the app-side OpenTofu stack that deploys this starter into the shared internal-tools GKE platform. Use when editing `infra/opentofu` files and keep ownership aligned with `../internal-tools-infra`.
---

# IaC

## Overview

Apply the shared-cluster pattern used by `../internal-tools-infra`.

This repo is the app-side companion, not the platform owner. `infra/opentofu` should deploy only app-owned resources into the namespace and storage contract created by the shared platform.

## Workflow

1. Inspect `infra/opentofu/main.tf`, `infra/opentofu/variables.tf`, `infra/opentofu/outputs.tf`, and `infra/opentofu/README.md`.
2. Read the app contract expectations from `../internal-tools-infra/README.md` and `../internal-tools-infra/docs/app-onboarding.md`.
3. Keep app-side inputs centered on the `app_contracts` output from `../internal-tools-infra/platform/apps`.
4. Implement only app-owned resources such as:
- `StatefulSet`
- app-owned `ConfigMap`
- pod labels, annotations, probes, and mounts needed to honor the platform contract
5. Do not recreate or mutate platform-owned resources from this repo:
- namespace
- ServiceAccount `runtime`
- PVC `data`
- Service `app`
- Ingress, IAP, DNS, ManagedCertificate, BackendConfig, FrontendConfig
- NetworkPolicy
- SecretProviderClass `app-secrets`
- ConfigMap `platform-contract`
6. Run infra checks:
- `tofu fmt -recursive`
- `tofu validate`
- `tofu plan`

## Naming Guidance

Generalize all names:
- Prefer `app`, `service`, or contract-derived names instead of copied product names.
- Keep variable names aligned with the platform contract so app repos can copy values directly from `app_contracts`.

## Resources

- Use `infra/opentofu/README.md` for the repo-specific deployment path.
- Use `../internal-tools-infra/docs/app-onboarding.md` for the platform-side contract flow.
