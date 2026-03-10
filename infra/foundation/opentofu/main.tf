locals {
  github_repository = "${var.github_repository_owner}/${var.github_repository_name}"
}

data "google_project" "current" {
  project_id = var.project_id
}

resource "google_project_service" "required" {
  for_each = toset([
    "artifactregistry.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "storage.googleapis.com",
    "sts.googleapis.com",
  ])

  service            = each.value
  disable_on_destroy = false
}

resource "google_storage_bucket" "terraform_state" {
  name                        = var.state_bucket_name
  location                    = var.region
  storage_class               = var.state_bucket_storage_class
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"
  force_destroy               = var.state_bucket_force_destroy
  project                     = var.project_id

  versioning {
    enabled = true
  }

  lifecycle {
    prevent_destroy = true
  }

  depends_on = [google_project_service.required]
}

resource "google_iam_workload_identity_pool" "github_actions" {
  workload_identity_pool_id = var.github_workload_identity_pool_id
  display_name              = var.github_workload_identity_pool_display_name
  description               = "Allows GitHub Actions OIDC identities to deploy FsharpStarter"
  project                   = var.project_id

  depends_on = [google_project_service.required]
}

resource "google_iam_workload_identity_pool_provider" "github_actions" {
  workload_identity_pool_id          = google_iam_workload_identity_pool.github_actions.workload_identity_pool_id
  workload_identity_pool_provider_id = var.github_workload_identity_provider_id
  display_name                       = var.github_workload_identity_provider_display_name
  description                        = "GitHub Actions OIDC provider for FsharpStarter deploys"
  project                            = var.project_id

  attribute_mapping = {
    "google.subject"             = "assertion.sub"
    "attribute.actor"            = "assertion.actor"
    "attribute.aud"              = "assertion.aud"
    "attribute.ref"              = "assertion.ref"
    "attribute.repository"       = "assertion.repository"
    "attribute.repository_owner" = "assertion.repository_owner"
  }

  attribute_condition = "assertion.repository=='${local.github_repository}' && assertion.ref=='${var.github_deploy_branch}'"

  oidc {
    issuer_uri = "https://token.actions.githubusercontent.com"
  }
}

resource "google_service_account" "deploy" {
  account_id   = var.deploy_service_account_id
  display_name = "FsharpStarter Deploy"
  project      = var.project_id
}

resource "google_service_account_iam_member" "deploy_workload_identity_user" {
  service_account_id = google_service_account.deploy.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "principalSet://iam.googleapis.com/projects/${data.google_project.current.number}/locations/global/workloadIdentityPools/${google_iam_workload_identity_pool.github_actions.workload_identity_pool_id}/attribute.repository/${local.github_repository}"
}

resource "google_project_iam_member" "deploy_roles" {
  for_each = toset([
    "roles/artifactregistry.writer",
    "roles/compute.instanceAdmin.v1",
    "roles/compute.osAdminLogin",
    "roles/iap.tunnelResourceAccessor",
  ])

  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.deploy.email}"
}

resource "google_storage_bucket_iam_member" "deploy_state_bucket_object_admin" {
  bucket = google_storage_bucket.terraform_state.name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${google_service_account.deploy.email}"
}

resource "google_storage_bucket_iam_member" "deploy_state_bucket_reader" {
  bucket = google_storage_bucket.terraform_state.name
  role   = "roles/storage.legacyBucketReader"
  member = "serviceAccount:${google_service_account.deploy.email}"
}
