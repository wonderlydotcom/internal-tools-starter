#!/usr/bin/env bash
set -euo pipefail

INFRA_DIR="${INFRA_DIR:-infra/opentofu}"
TOFU_BACKEND_CONFIG_FILE="${TOFU_BACKEND_CONFIG_FILE:-}"
TOFU_BACKEND_BUCKET="${TOFU_BACKEND_BUCKET:-}"
TOFU_BACKEND_PREFIX="${TOFU_BACKEND_PREFIX:-}"
TOFU_BACKEND_IMPERSONATE_SERVICE_ACCOUNT="${TOFU_BACKEND_IMPERSONATE_SERVICE_ACCOUNT:-}"

if ! command -v tofu >/dev/null 2>&1; then
  echo "tofu is required" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required" >&2
  exit 1
fi

if [[ ! -d "${INFRA_DIR}" ]]; then
  echo "Infra directory not found: ${INFRA_DIR}" >&2
  exit 1
fi

tofu_init_args=(init -input=false)

if [[ -n "${TOFU_BACKEND_CONFIG_FILE}" ]]; then
  if [[ ! -f "${TOFU_BACKEND_CONFIG_FILE}" ]]; then
    echo "Missing backend config file: ${TOFU_BACKEND_CONFIG_FILE}" >&2
    exit 1
  fi

  tofu_init_args+=("-backend-config=${TOFU_BACKEND_CONFIG_FILE}")
else
  if [[ -n "${TOFU_BACKEND_BUCKET}" ]]; then
    tofu_init_args+=("-backend-config=bucket=${TOFU_BACKEND_BUCKET}")
  fi

  if [[ -n "${TOFU_BACKEND_PREFIX}" ]]; then
    tofu_init_args+=("-backend-config=prefix=${TOFU_BACKEND_PREFIX}")
  fi

  if [[ -n "${TOFU_BACKEND_IMPERSONATE_SERVICE_ACCOUNT}" ]]; then
    tofu_init_args+=("-backend-config=impersonate_service_account=${TOFU_BACKEND_IMPERSONATE_SERVICE_ACCOUNT}")
  fi
fi

pushd "${INFRA_DIR}" >/dev/null
tofu "${tofu_init_args[@]}" >/dev/null
output_json="$(tofu output -json)"
popd >/dev/null

if ! jq -e 'type == "object" and length > 0' >/dev/null <<<"${output_json}"; then
  echo "OpenTofu outputs are missing in ${INFRA_DIR}. Run 'tofu apply' first." >&2
  exit 1
fi

require_output() {
  local key="$1"
  local value
  value="$(jq -r --arg key "${key}" '.[$key].value // empty' <<<"${output_json}")"
  if [[ -z "${value}" || "${value}" == "null" ]]; then
    echo "Missing required OpenTofu output: ${key}" >&2
    exit 1
  fi
  printf '%s' "${value}"
}

export GCP_PROJECT_ID="$(require_output project_id)"
export GCP_REGION="$(require_output artifact_registry_location)"
export GCP_ARTIFACT_REPO="$(require_output artifact_registry_repo_id)"
export GCP_VM_ZONE="$(require_output vm_zone)"
export GCP_BACKEND_SERVICE="$(jq -r '.backend_service_name.value? // empty' <<<"${output_json}")"
export GCP_BLUEGREEN_VM_NAME="$(jq -r '.bluegreen_vm_name.value? // empty' <<<"${output_json}")"
export FSHARP_STARTER_IAP_JWT_AUDIENCE="$(require_output iap_jwt_audience)"
export FSHARP_STARTER_DATA_ROOT="$(require_output data_mount_path)"
export FSHARP_STARTER_VALIDATE_IAP_JWT="$(jq -r '.validate_iap_jwt.value? // "true"' <<<"${output_json}")"
export FSHARP_STARTER_GOOGLE_DIRECTORY_ENABLED="$(jq -r '.google_directory_enabled.value? // "false"' <<<"${output_json}")"
export FSHARP_STARTER_GOOGLE_DIRECTORY_ADMIN_USER_EMAIL="$(jq -r '.google_directory_admin_user_email.value? // empty' <<<"${output_json}")"
export FSHARP_STARTER_GOOGLE_DIRECTORY_SCOPE="$(jq -r '.google_directory_scope.value? // empty' <<<"${output_json}")"
export FSHARP_STARTER_GOOGLE_DIRECTORY_OU_KEY_PREFIX="$(jq -r '.google_directory_org_unit_key_prefix.value? // empty' <<<"${output_json}")"
export FSHARP_STARTER_GOOGLE_DIRECTORY_INCLUDE_OU_HIERARCHY="$(jq -r '.google_directory_include_org_unit_hierarchy.value? // "true"' <<<"${output_json}")"
export FSHARP_STARTER_GOOGLE_DIRECTORY_CUSTOM_KEY_PREFIX="$(jq -r '.google_directory_custom_attribute_key_prefix.value? // empty' <<<"${output_json}")"
export FSHARP_STARTER_GOOGLE_DIRECTORY_CREDENTIALS_SECRET_NAME="$(jq -r '.google_directory_credentials_secret_name.value? // empty' <<<"${output_json}")"

MIG_NAME="$(jq -r '.managed_instance_group_name.value? // empty' <<<"${output_json}")"
if [[ -n "${MIG_NAME}" ]]; then
  export GCP_MIG_NAME="${MIG_NAME}"
  unset GCP_VM_NAME || true
else
  export GCP_VM_NAME="$(require_output vm_name)"
fi

if [[ -z "${FSHARP_STARTER_ORG_ADMIN_EMAIL:-}" ]]; then
  FSHARP_STARTER_ORG_ADMIN_EMAIL_FROM_TF="$(jq -r '.org_admin_email.value? // empty' <<<"${output_json}")"
  if [[ -n "${FSHARP_STARTER_ORG_ADMIN_EMAIL_FROM_TF}" ]]; then
    export FSHARP_STARTER_ORG_ADMIN_EMAIL="${FSHARP_STARTER_ORG_ADMIN_EMAIL_FROM_TF}"
  fi
fi

export REMOTE_DIR="${REMOTE_DIR:-/opt/fsharp-starter}"

"$(dirname "$0")/deploy-gce.sh"
