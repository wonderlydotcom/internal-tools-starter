variable "kubeconfig_path" {
  description = "Path to the kubeconfig that has credentials for the shared internal-tools GKE cluster."
  type        = string
  default     = "~/.kube/config"
}

variable "project_id" {
  description = "GCP project ID that owns the shared Artifact Registry repositories."
  type        = string

  validation {
    condition     = trimspace(var.project_id) != ""
    error_message = "Set project_id to the shared internal-tools GCP project."
  }
}

variable "artifact_registry_location" {
  description = "Artifact Registry location used by the shared platform."
  type        = string
  default     = "us-central1"

  validation {
    condition     = trimspace(var.artifact_registry_location) != ""
    error_message = "artifact_registry_location must not be empty."
  }
}

variable "image_name" {
  description = "Container image name inside the per-app Artifact Registry repository."
  type        = string
  default     = "fsharp-starter-api"

  validation {
    condition     = trimspace(var.image_name) != ""
    error_message = "image_name must not be empty."
  }
}

variable "image_tag" {
  description = "Container image tag to deploy."
  type        = string
  default     = "latest"

  validation {
    condition     = trimspace(var.image_tag) != ""
    error_message = "image_tag must not be empty."
  }
}

variable "workload_name" {
  description = "Name for the app-owned StatefulSet."
  type        = string
  default     = "app"

  validation {
    condition     = can(regex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$", var.workload_name))
    error_message = "workload_name must be a DNS-safe Kubernetes resource name."
  }
}

variable "data_mount_path" {
  description = "Mount path for the platform-managed PVC named by platform_contract.pvc_name."
  type        = string
  default     = "/app/data"

  validation {
    condition     = startswith(var.data_mount_path, "/")
    error_message = "data_mount_path must be an absolute path."
  }
}

variable "runtime_secrets_mount_path" {
  description = "Mount path for the platform-managed SecretProviderClass when runtime secrets are declared."
  type        = string
  default     = "/var/run/secrets/app"

  validation {
    condition     = startswith(var.runtime_secrets_mount_path, "/")
    error_message = "runtime_secrets_mount_path must be an absolute path."
  }
}

variable "app_config" {
  description = "Optional app-owned ConfigMap entries exposed to the workload through envFrom."
  type        = map(string)
  default     = {}
}

variable "extra_resource_labels" {
  description = "Additional labels applied to app-owned Kubernetes resources."
  type        = map(string)
  default     = {}
}

variable "extra_pod_labels" {
  description = "Additional labels applied to the pod template."
  type        = map(string)
  default     = {}
}

variable "pod_annotations" {
  description = "Additional annotations applied to the pod template."
  type        = map(string)
  default     = {}
}

variable "platform_contract" {
  description = "Subset of the internal-tools-infra app_contracts entry for this app."
  type = object({
    namespace                   = string
    domain_name                 = string
    runtime_service_account     = string
    service_name                = string
    pvc_name                    = string
    health_check_path           = string
    runtime_contract_config_map = string
    secret_provider_class       = optional(string, null)
    artifact_registry_repo      = string
    state_bucket_name           = string
    iap_jwt_audience            = string
    required_pod_labels         = map(string)
  })

  validation {
    condition = alltrue([
      trimspace(var.platform_contract.namespace) != "",
      startswith(var.platform_contract.namespace, "app-"),
      trimspace(var.platform_contract.domain_name) != "",
      trimspace(var.platform_contract.runtime_service_account) != "",
      trimspace(var.platform_contract.service_name) != "",
      trimspace(var.platform_contract.pvc_name) != "",
      startswith(var.platform_contract.health_check_path, "/"),
      trimspace(var.platform_contract.runtime_contract_config_map) != "",
      trimspace(var.platform_contract.artifact_registry_repo) != "",
      trimspace(var.platform_contract.state_bucket_name) != "",
      trimspace(var.platform_contract.iap_jwt_audience) != "",
      length(var.platform_contract.required_pod_labels) > 0,
    ])
    error_message = "platform_contract must be populated from internal-tools-infra app_contracts for this app."
  }
}
