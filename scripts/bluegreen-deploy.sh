#!/usr/bin/env bash
set -euo pipefail

# Blue/green deploy flow for the OpenTofu model in infra/opentofu:
# 1) Mandatory preflight SQLite backup from the current primary MIG instance.
# 2) Build and push image tag.
# 3) Apply OpenTofu with green enabled and primary drained (both capacities 0).
# 4) Wait for green local health check.
# 5) Shift traffic by setting green capacity to 1.

require_cmd() { command -v "$1" >/dev/null 2>&1 || { echo "Missing required command: $1" >&2; exit 1; }; }
log() { printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"; }
now() { date +%s; }

for c in gcloud docker jq tofu; do require_cmd "$c"; done

INFRA_DIR="${INFRA_DIR:-infra/opentofu}"
LOCAL_SQLITE_BACKUP_ROOT="${LOCAL_SQLITE_BACKUP_ROOT:-$HOME/Desktop/fsharp-starter-sqlite-backups}"
REMOTE_DATA_ROOT_DEFAULT="/mnt/fsharp-starter-data"

GREEN_BACKEND_ENABLED=0
cleanup_files=()

cleanup() {
  local f
  for f in "${cleanup_files[@]:-}"; do
    [[ -n "$f" ]] && rm -f "$f" || true
  done
}

hydrate_from_tofu_output() {
  export GCP_PROJECT_ID="${GCP_PROJECT_ID:-$(jq -r '.project_id.value' <<<"$OUT")}" 
  export GCP_REGION="${GCP_REGION:-$(jq -r '.artifact_registry_location.value' <<<"$OUT")}" 
  export GCP_ARTIFACT_REPO="${GCP_ARTIFACT_REPO:-$(jq -r '.artifact_registry_repo_id.value' <<<"$OUT")}" 
  export GCP_VM_ZONE="${GCP_VM_ZONE:-$(jq -r '.vm_zone.value' <<<"$OUT")}" 
  export GCP_MIG_NAME="${GCP_MIG_NAME:-$(jq -r '.managed_instance_group_name.value' <<<"$OUT")}" 
  export GCP_BACKEND_SERVICE="${GCP_BACKEND_SERVICE:-$(jq -r '.backend_service_name.value // empty' <<<"$OUT")}" 
  export FSHARP_STARTER_DATA_ROOT="${FSHARP_STARTER_DATA_ROOT:-$(jq -r '.data_mount_path.value // ""' <<<"$OUT")}" 

  GREEN_VM_NAME="$(jq -r '.bluegreen_vm_name.value // empty' <<<"$OUT")"
  GREEN_IG_NAME="$(jq -r '.bluegreen_instance_group_name.value // empty' <<<"$OUT")"
}

load_tofu_outputs() {
  pushd "${INFRA_DIR}" >/dev/null
  OUT="$(tofu output -json)"
  popd >/dev/null
  hydrate_from_tofu_output
}

apply_tofu_state() {
  local primary_capacity="$1"
  local green_capacity="$2"
  local green_enabled="$3"
  local image_tag="$4"
  local primary_mig_size="$5"

  pushd "${INFRA_DIR}" >/dev/null
  tofu apply -auto-approve \
    -var "primary_backend_capacity=${primary_capacity}" \
    -var "bluegreen_backend_capacity=${green_capacity}" \
    -var "bluegreen_enabled=${green_enabled}" \
    -var "bluegreen_image_tag=${image_tag}" \
    -var "primary_mig_target_size=${primary_mig_size}" >/dev/null
  OUT="$(tofu output -json)"
  popd >/dev/null

  hydrate_from_tofu_output
}

wait_for_ssh() {
  local vm="$1" start
  start="$(now)"
  while true; do
    if gcloud compute ssh "$vm" --project "$GCP_PROJECT_ID" --zone "$GCP_VM_ZONE" --tunnel-through-iap --command "echo ok" >/dev/null 2>&1; then
      return 0
    fi
    if (( "$(now)" - start > 900 )); then
      echo "SSH timeout: $vm" >&2
      exit 1
    fi
    sleep 10
  done
}

wait_local_health() {
  local vm="$1" start
  start="$(now)"
  while true; do
    if gcloud compute ssh "$vm" --project "$GCP_PROJECT_ID" --zone "$GCP_VM_ZONE" --tunnel-through-iap --command "curl -fsS http://127.0.0.1:8080/healthy >/dev/null" >/dev/null 2>&1; then
      return 0
    fi
    if (( "$(now)" - start > 600 )); then
      echo "Health timeout: $vm" >&2
      exit 1
    fi
    sleep 5
  done
}

resolve_old_vm() {
  local mig_vm bluegreen_vm backend_vm labeled_vm
  mig_vm="$(gcloud compute instance-groups managed list-instances "${GCP_MIG_NAME}" \
    --project "${GCP_PROJECT_ID}" \
    --zone "${GCP_VM_ZONE}" \
    --format='value(instance.basename())' | head -n1)"
  if [[ -n "${mig_vm}" ]]; then
    echo "${mig_vm}"
    return 0
  fi

  bluegreen_vm="$(jq -r '.bluegreen_vm_name.value // empty' <<<"${OUT:-}")"
  if [[ -n "${bluegreen_vm}" ]]; then
    echo "${bluegreen_vm}"
    return 0
  fi

  if [[ -n "${GCP_BACKEND_SERVICE:-}" ]]; then
    backend_vm="$(
      gcloud compute backend-services get-health "${GCP_BACKEND_SERVICE}" \
        --project "${GCP_PROJECT_ID}" \
        --global \
        --format=json 2>/dev/null \
        | jq -r '.. | .instance? // empty | capture(".*/instances/(?<name>[^/]+)$").name' 2>/dev/null \
        | head -n1
    )"
    if [[ -n "${backend_vm}" ]]; then
      echo "${backend_vm}"
      return 0
    fi
  fi

  labeled_vm="$(
    gcloud compute instances list \
      --project "${GCP_PROJECT_ID}" \
      --filter="zone:(${GCP_VM_ZONE}) AND labels.app=fsharp-starter AND status=RUNNING" \
      --format='value(name)' 2>/dev/null \
      | head -n1
  )"
  if [[ -n "${labeled_vm}" ]]; then
    echo "${labeled_vm}"
    return 0
  fi

  return 1
}

wait_primary_mig_zero_instances() {
  local start elapsed target_size instance_count
  start="$(now)"
  while true; do
    target_size="$(gcloud compute instance-groups managed describe "${GCP_MIG_NAME}" \
      --project "${GCP_PROJECT_ID}" \
      --zone "${GCP_VM_ZONE}" \
      --format='value(targetSize)')"
    instance_count="$(gcloud compute instance-groups managed list-instances "${GCP_MIG_NAME}" \
      --project "${GCP_PROJECT_ID}" \
      --zone "${GCP_VM_ZONE}" \
      --format='value(instance)' | wc -l | tr -d ' ')"

    if [[ "${target_size}" == "0" && "${instance_count}" == "0" ]]; then
      return 0
    fi

    elapsed="$(( $(now) - start ))"
    if (( elapsed > 900 )); then
      echo "Primary MIG did not scale down to zero instances within 15 minutes (targetSize=${target_size}, instances=${instance_count})" >&2
      exit 1
    fi
    sleep 10
  done
}

on_error() {
  local exit_code="$1"
  set +e

  if (( GREEN_BACKEND_ENABLED == 0 )); then
    log "Failure before green traffic enable; attempting rollback to primary MIG"
    apply_tofu_state 1 0 false "" 1 || true
  fi

  cleanup
  exit "$exit_code"
}

trap cleanup EXIT
trap 'on_error $?' ERR

load_tofu_outputs

: "${GCP_PROJECT_ID:?}"
: "${GCP_REGION:?}"
: "${GCP_ARTIFACT_REPO:?}"
: "${GCP_VM_ZONE:?}"
: "${GCP_MIG_NAME:?}"

IMAGE_NAME="${IMAGE_NAME:-fsharp-starter-api}"
TAG="${TAG:-latest}"
REGISTRY_HOST="${GCP_REGION}-docker.pkg.dev"
IMAGE_URI="${REGISTRY_HOST}/${GCP_PROJECT_ID}/${GCP_ARTIFACT_REPO}/${IMAGE_NAME}:${TAG}"
DATA_ROOT="${FSHARP_STARTER_DATA_ROOT:-$REMOTE_DATA_ROOT_DEFAULT}"

OLD_VM_NAME="$(resolve_old_vm || true)"
if [[ -n "${OLD_VM_NAME}" ]]; then
  log "Preflight: create local SQLite backup from ${OLD_VM_NAME}"
  PRE_FLIGHT_ARCHIVE="$(mktemp /tmp/fsharp-starter-db-preflight.XXXXXX.tgz)"
  cleanup_files+=("${PRE_FLIGHT_ARCHIVE}")
  gcloud compute ssh "${OLD_VM_NAME}" --project "${GCP_PROJECT_ID}" --zone "${GCP_VM_ZONE}" --tunnel-through-iap --command "sudo tar -C ${DATA_ROOT} -czf - fsharp-starter-db openfga" > "${PRE_FLIGHT_ARCHIVE}"

  LOCAL_SQLITE_BACKUP_DIR="${LOCAL_SQLITE_BACKUP_ROOT}/preflight-$(date +%Y%m%d%H%M%S)-${OLD_VM_NAME}"
  mkdir -p "${LOCAL_SQLITE_BACKUP_DIR}"
  tar -C "${LOCAL_SQLITE_BACKUP_DIR}" -xzf "${PRE_FLIGHT_ARCHIVE}" fsharp-starter-db openfga
  find "${LOCAL_SQLITE_BACKUP_DIR}" -type f \( -name '*.db' -o -name '*.db-*' -o -name '*.sqlite' -o -name '*.sqlite-*' \) | sort > "${LOCAL_SQLITE_BACKUP_DIR}/sqlite-files.txt"
  if [[ ! -s "${LOCAL_SQLITE_BACKUP_DIR}/sqlite-files.txt" ]]; then
    echo "Preflight backup validation failed: no SQLite files found in ${LOCAL_SQLITE_BACKUP_DIR}" >&2
    exit 1
  fi
  log "Preflight backup complete: ${LOCAL_SQLITE_BACKUP_DIR}"
fi

T0="$(now)"

log "Build and push image: ${IMAGE_URI}"
gcloud auth configure-docker "${REGISTRY_HOST}" --quiet
docker build --platform linux/amd64 -f src/FsharpStarter.Api/Dockerfile -t "${IMAGE_URI}" .
docker push "${IMAGE_URI}"

log "Apply stage 1: enable green, drain primary, keep both backends at capacity 0"
apply_tofu_state 0 0 true "${TAG}" 0
[[ -n "${GREEN_VM_NAME:-}" ]] || { echo "OpenTofu did not return bluegreen_vm_name" >&2; exit 1; }

wait_for_ssh "${GREEN_VM_NAME}"
wait_local_health "${GREEN_VM_NAME}"

log "Apply stage 2: route traffic to green backend"
apply_tofu_state 0 1 true "${TAG}" 0
GREEN_BACKEND_ENABLED=1
log "Wait for primary MIG to reach zero instances"
wait_primary_mig_zero_instances

T1="$(now)"
TOTAL_SECONDS="$((T1-T0))"

cat <<SUMMARY
Blue/green deploy complete.

Timing:
  total_seconds=${TOTAL_SECONDS}

Resources:
  old_vm=${OLD_VM_NAME:-none}
  green_vm=${GREEN_VM_NAME}
  old_backend_group=${GCP_MIG_NAME}
  green_backend_group=${GREEN_IG_NAME}

State:
  bluegreen_enabled=true
  primary_mig_target_size=0
  primary_backend_capacity=0
  bluegreen_backend_capacity=1
  image_tag=${TAG}
SUMMARY
