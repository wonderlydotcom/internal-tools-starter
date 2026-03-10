output "project_id" {
  value       = var.project_id
  description = "GCP project ID used by the foundation stack"
}

output "project_number" {
  value       = data.google_project.current.number
  description = "GCP project number used by the foundation stack"
}

output "terraform_state_bucket_name" {
  value       = google_storage_bucket.terraform_state.name
  description = "GCS bucket name used for OpenTofu remote state"
}

output "github_workload_identity_pool_name" {
  value       = google_iam_workload_identity_pool.github_actions.name
  description = "Full Workload Identity Pool resource name for GitHub Actions"
}

output "github_workload_identity_provider_name" {
  value       = google_iam_workload_identity_pool_provider.github_actions.name
  description = "Full Workload Identity Provider resource name for GitHub Actions"
}

output "github_deploy_service_account_email" {
  value       = google_service_account.deploy.email
  description = "Service account email used by GitHub Actions deployments"
}
