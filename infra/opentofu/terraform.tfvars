project_id                 = "wonderly-idp-sso"
artifact_registry_location = "us-central1"
image_name                 = "fsharp-starter-api"
workload_name              = "app"
data_mount_path            = "/app/data"
runtime_secrets_mount_path = "/var/run/secrets/app"

# Optional app-owned non-secret config exposed through envFrom.
app_config = {
  ASPNETCORE_ENVIRONMENT = "Production"
}

# Copy the matching object from:
# tofu -chdir=../internal-tools-infra/platform/apps output -json app_contracts
platform_contract = {
  namespace                   = "app-fsharp-starter"
  domain_name                 = "fsharp-starter.wonderly.info"
  runtime_service_account     = "runtime"
  service_name                = "app"
  pvc_name                    = "data"
  health_check_path           = "/healthy"
  runtime_contract_config_map = "platform-contract"
  secret_provider_class       = null
  artifact_registry_repo      = "fsharp-starter"
  state_bucket_name           = "wonderly-idp-sso-fsharp-starter-state"
  iap_jwt_audience            = "/projects/199626281531/global/backendServices/1234567890123456789"
  required_pod_labels = {
    "internal-tools.wonderly.io/service" = "app"
  }
}
