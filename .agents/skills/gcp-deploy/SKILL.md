---
name: gcp-deploy
description: Use when performing blue-green deployments of the starter to Google Compute Engine with Docker Compose and OpenTofu. For infra architecture and safety guardrails, use the iac skill.
---

# Skill: gcp-deploy

Use this when performing blue-green deployments of the starter to Google Compute Engine.

This skill is deployment-operations focused. For OpenTofu infrastructure design, resource topology, and persistent-disk safety constraints, defer to `iac`:
- `.agents/skills/iac/SKILL.md`

## Source Summary
This skill summarizes:
- `docker-compose.gce.yml`
- `infra/opentofu/*`

## Runtime Topology (docker-compose.gce)
- `fsharp-starter-api`: ASP.NET API container, mounts persistent sqlite data at `/app/data`.

## Data/Secrets Expectations
- App data root default: `${FSHARP_STARTER_DATA_ROOT:-/mnt/fsharp-starter-data}`.
- SQLite DB file under mounted `/app/data`.

## OpenTofu Summary
Main files:
- `infra/opentofu/main.tf`: VM, firewall, startup template wiring.
- `infra/opentofu/variables.tf`: project/region/zone/network/image/instance parameters.
- `infra/opentofu/providers.tf` and `versions.tf`: provider and version pins.
- `infra/opentofu/templates/startup.sh.tmpl`: bootstrap script installing Docker/Compose and running deployment.
- `infra/opentofu/outputs.tf`: exported deployment values.

Blue-green topology, validation, and data-disk lifecycle guardrails are defined by the `iac` skill and must be preserved.

## Deployment Scope
Only blue-green deployments are in scope. Do not add or reference non-blue-green deployment paths in this skill.
Deployment health must use `GET /healthy`; do not rely on Swagger endpoints for readiness or load-balancer checks.

## Blue-Green Deployment Flow
1. Configure `terraform.tfvars` from `environments/dev/terraform.tfvars.example`.
2. Confirm blue-green settings are enabled in infra variables and compliant with `iac` safety rules.
3. `tofu init`.
4. `tofu plan`.
5. `tofu apply`.
6. Verify the active backend switched as expected and health checks pass before traffic cutover is considered complete.
   - Health validation target: `http://127.0.0.1:8080/healthy` (or equivalent backend health check path `/healthy`).
7. Confirm persistent application data remains intact after rollout.

## Hardening Rules
- Follow `iac` as the source of truth for blue-green safety constraints and disk-protection requirements.
- Reject any change that introduces single-path/in-place deployment guidance.
- Treat deploy scripts and infra templates as production code (strict shell mode, robust auth paths, explicit compatibility checks).
- Avoid brittle inline token expansion in templated shell/systemd snippets; isolate auth logic in dedicated scripts.
- Handle environment variance explicitly (Compose v1/v2 behavior, IAP tunnel flags, image tag policy like `latest`).
- When changing startup/deploy auth flows, validate in a realistic VM path, not only local shell execution.

## Verification Commands
After blue-green infra edits:
1. `cd infra/opentofu && tofu fmt -recursive`
2. `cd infra/opentofu && tofu validate`
3. `cd infra/opentofu && tofu plan -out=tfplan`
4. `cd infra/opentofu && tofu show -json tfplan > /tmp/tfplan.json`
5. `python3 .agents/skills/iac/scripts/check_no_disk_delete.py /tmp/tfplan.json`
6. `docker compose -f docker-compose.gce.yml config`
