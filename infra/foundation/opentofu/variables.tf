variable "project_id" {
  description = "GCP project ID"
  type        = string
}

variable "region" {
  description = "GCP region for foundation resources"
  type        = string
  default     = "us-central1"
}

variable "state_bucket_name" {
  description = "GCS bucket name used for OpenTofu remote state"
  type        = string
  default     = "replace-me-fsharp-starter-tfstate"
}

variable "state_bucket_storage_class" {
  description = "Storage class for the OpenTofu state bucket"
  type        = string
  default     = "STANDARD"
}

variable "state_bucket_force_destroy" {
  description = "Whether the foundation stack may destroy the state bucket"
  type        = bool
  default     = false
}

variable "github_repository_owner" {
  description = "GitHub organization or user that owns the repository allowed to deploy"
  type        = string
  default     = "your-github-org"
}

variable "github_repository_name" {
  description = "GitHub repository name allowed to deploy"
  type        = string
  default     = "fsharp-starter"
}

variable "github_deploy_branch" {
  description = "Git ref allowed to deploy via GitHub Actions"
  type        = string
  default     = "refs/heads/main"
}

variable "github_workload_identity_pool_id" {
  description = "Workload Identity Pool ID used for GitHub Actions"
  type        = string
  default     = "github-actions"
}

variable "github_workload_identity_pool_display_name" {
  description = "Display name for the GitHub Actions Workload Identity Pool"
  type        = string
  default     = "GitHub Actions"
}

variable "github_workload_identity_provider_id" {
  description = "Workload Identity Provider ID used for GitHub Actions"
  type        = string
  default     = "fsharp-starter"
}

variable "github_workload_identity_provider_display_name" {
  description = "Display name for the GitHub Actions Workload Identity Provider"
  type        = string
  default     = "FsharpStarter GitHub Provider"
}

variable "deploy_service_account_id" {
  description = "Service account ID used by GitHub Actions deployments"
  type        = string
  default     = "fsharp-starter-deploy"
}
