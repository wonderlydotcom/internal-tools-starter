---
name: iac
description: Design and update cost-conscious GCP OpenTofu infrastructure for bootstrap/foundation and app stacks. Use when editing `infra/foundation/opentofu` or `infra/opentofu` files, and enforce persistent-disk safety so blue-green deploys and teardown actions never delete application data.
---

# IaC

## Overview

Apply a standardized OpenTofu pattern for GCP compute, load balancing, and rollout safety. Keep infrastructure generic (no product-specific names) and preserve persistent disk data across upgrades, blue-green transitions, and destroy attempts.

This repository now uses two OpenTofu stacks with different responsibilities:
- `infra/foundation/opentofu`: bootstrap/foundation resources that must exist before normal app deploys can run
- `infra/opentofu`: the main app infrastructure stack

Keep that separation intact. Do not fold foundation resources back into the app stack.

## Baseline Defaults

Use these cost-focused defaults unless the user requests otherwise:
- `machine_type = "e2-micro"`
- `boot_disk_size_gb = 10`
- `data_disk_size_gb = 1`
- `data_disk_type = "pd-balanced"`
- `preserve_data_disk_on_destroy = true`

## Workflow

1. Decide which stack owns the change before editing anything:
- `infra/foundation/opentofu` owns the OpenTofu remote-state bucket, GitHub Actions Workload Identity Pool/Provider, deploy service account, and IAM needed for CI/CD bootstrap.
- `infra/opentofu` owns the runtime application topology: network, MIG, load balancer, VM service account, secrets wiring, Artifact Registry, and data-disk/runtime infrastructure.
2. Preserve the reason for the split:
- foundation resources must not depend on the app stack because the app stack uses the remote backend and CI identity they provide
- the state bucket must not be managed by the same state stored inside that bucket
- CI/bootstrap identity changes should be reviewable without coupling them to app rollout changes
3. Inspect the relevant files for the target stack:
- foundation: `infra/foundation/opentofu/*.tf`
- app: `infra/opentofu/main.tf`, `infra/opentofu/variables.tf`, `infra/opentofu/outputs.tf`, and `infra/opentofu/templates/*.tmpl`
4. Implement a generic topology for the app stack:
- VPC/subnet
- VM service account + IAM for registry/logging/secrets
- one persistent data disk used by the blue/green VM
- primary MIG behind HTTPS load balancer (stateless instances)
- optional blue-green single VM + unmanaged instance group as alternate backend
- configure backend health checks to target `GET /healthy` (never Swagger paths)
5. Enforce persistent-disk guardrails:
- Keep `google_compute_disk` in a protected resource with `lifecycle { prevent_destroy = true }`.
- Model a protected/unprotected disk pair, then select active disk self-link via a local expression.
- Keep primary instance templates stateless (no attached persistent data disk source).
- Validate blue-green settings so unsafe combinations fail at plan time.
6. Apply variable validation to block unsafe rollouts:
- Require `preserve_data_disk_on_destroy == true` when blue-green is enabled.
7. Run infra checks for the stack you changed:
- `tofu fmt -recursive`
- `tofu validate`
- `tofu plan -out=tfplan`
- `tofu show -json tfplan > /tmp/tfplan.json`
- `python3 agents/skills/iac/scripts/check_no_disk_delete.py /tmp/tfplan.json`

For foundation changes, also verify:
- the state bucket keeps versioning enabled
- the state bucket keeps `prevent_destroy = true`
- Workload Identity conditions remain scoped to the intended GitHub repo and branch
- deploy service account permissions remain minimal for the current deployment path

## Blue-Green Safety Pattern

Use this validation shape in `variables.tf`:

```hcl
variable "bluegreen_enabled" {
  type    = bool
  default = false

  validation {
    condition = !var.bluegreen_enabled || var.preserve_data_disk_on_destroy
    error_message = "When bluegreen_enabled=true, preserve_data_disk_on_destroy must be true."
  }
}
```

Use a protected disk by default:

```hcl
resource "google_compute_disk" "data_protected" {
  count = var.preserve_data_disk_on_destroy ? 1 : 0

  name = local.data_disk_name
  type = var.data_disk_type
  zone = var.zone
  size = var.data_disk_size_gb

  lifecycle {
    prevent_destroy = true
  }
}
```

## Naming Guidance

Generalize all names:
- Prefer `app`, `service`, or `${var.name_prefix}` instead of copied product names.
- Keep stable resource keys (`primary`, `bluegreen`, `data_protected`) so plans remain predictable.
- Keep foundation resource keys stable as well (`terraform_state`, `github_actions`, `deploy`) so imports and state moves stay predictable.

## Resources

- Use `references/gcp-opentofu-bluegreen.md` for a complete generic resource map and backend switching pattern.
- Use `scripts/check_no_disk_delete.py` to fail plans that attempt to delete/replace persistent disks.
