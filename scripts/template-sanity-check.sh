#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

FAIL=0

echo "Running template sanity checks..."

declare -a BLOCKED_PATHS=(
  "infra/foundation/opentofu/backend.hcl"
  "infra/foundation/opentofu/terraform.tfvars"
  "infra/foundation/opentofu/terraform.tfstate"
  "infra/foundation/opentofu/terraform.tfstate.backup"
  "infra/opentofu/backend.hcl"
  "infra/opentofu/terraform.tfvars"
  "infra/opentofu/terraform.tfstate"
  "infra/opentofu/terraform.tfstate.backup"
  "infra/opentofu/terraform.tfstate.1770575441.backup"
  "www/dist"
)

for path in "${BLOCKED_PATHS[@]}"; do
  if [ -e "$path" ]; then
    echo "[FAIL] Local artifact should not exist in template: $path"
    FAIL=1
  fi
done

LEGACY_MATCHES=$(rg -n "freetool|wonderly-idp-sso|wonderly\\.com" \
  --glob '!**/node_modules/**' \
  --glob '!**/bin/**' \
  --glob '!**/obj/**' \
  --glob '!**/.terraform/**' \
  --glob '!**/*.min.js' \
  --glob '!**/*.map' \
  --glob '!README.md' \
  --glob '!scripts/template-sanity-check.sh' \
  --glob '!**/package-lock.json' \
  --glob '!**/pnpm-lock.yaml' \
  --glob '!**/yarn.lock' \
  || true)

if [ -n "$LEGACY_MATCHES" ]; then
  echo "[FAIL] Found copied-project markers that should be replaced or removed:"
  echo "$LEGACY_MATCHES"
  FAIL=1
fi

if [ "$FAIL" -ne 0 ]; then
  echo "Template sanity checks failed."
  exit 1
fi

echo "Template sanity checks passed."
