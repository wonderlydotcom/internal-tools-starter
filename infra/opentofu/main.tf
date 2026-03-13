locals {
  secret_provider_class_name = (
    try(var.platform_contract.secret_provider_class, null) != null &&
    trimspace(try(var.platform_contract.secret_provider_class, "")) != ""
  ) ? trimspace(try(var.platform_contract.secret_provider_class, "")) : null

  image_ref = format(
    "%s-docker.pkg.dev/%s/%s/%s:%s",
    var.artifact_registry_location,
    var.project_id,
    var.platform_contract.artifact_registry_repo,
    var.image_name,
    var.image_tag,
  )

  managed_labels = {
    "app.kubernetes.io/name"       = var.workload_name
    "app.kubernetes.io/managed-by" = "opentofu"
  }

  resource_labels = merge(var.extra_resource_labels, local.managed_labels)
  pod_labels      = merge(var.extra_pod_labels, local.managed_labels, var.platform_contract.required_pod_labels)
  config_map_name = "${var.workload_name}-config"

  pod_annotations = merge(
    var.pod_annotations,
    {
      "internal-tools.wonderly.io/platform-contract-sha" = sha256(jsonencode({
        health_check_path           = var.platform_contract.health_check_path
        iap_jwt_audience            = var.platform_contract.iap_jwt_audience
        runtime_contract_config_map = var.platform_contract.runtime_contract_config_map
        secret_provider_class       = local.secret_provider_class_name
      }))
    },
    length(var.app_config) == 0 ? {} : {
      "internal-tools.wonderly.io/app-config-sha" = sha256(jsonencode(var.app_config))
    }
  )
}

resource "kubernetes_config_map_v1" "app_config" {
  count = length(var.app_config) == 0 ? 0 : 1

  metadata {
    name      = local.config_map_name
    namespace = var.platform_contract.namespace
    labels    = local.resource_labels
  }

  data = var.app_config
}

resource "kubernetes_stateful_set_v1" "app" {
  metadata {
    name      = var.workload_name
    namespace = var.platform_contract.namespace
    labels    = local.resource_labels
  }

  spec {
    replicas     = 1
    service_name = var.platform_contract.service_name

    selector {
      match_labels = var.platform_contract.required_pod_labels
    }

    update_strategy {
      type = "RollingUpdate"
    }

    template {
      metadata {
        labels      = local.pod_labels
        annotations = local.pod_annotations
      }

      spec {
        service_account_name = var.platform_contract.runtime_service_account

        container {
          name  = "api"
          image = local.image_ref

          port {
            name           = "http"
            container_port = 8080
          }

          env_from {
            config_map_ref {
              name = var.platform_contract.runtime_contract_config_map
            }
          }

          dynamic "env_from" {
            for_each = length(var.app_config) == 0 ? [] : [kubernetes_config_map_v1.app_config[0].metadata[0].name]

            content {
              config_map_ref {
                name = env_from.value
              }
            }
          }

          readiness_probe {
            http_get {
              path = var.platform_contract.health_check_path
              port = 8080
            }

            initial_delay_seconds = 5
            period_seconds        = 10
            timeout_seconds       = 5
            failure_threshold     = 3
          }

          liveness_probe {
            http_get {
              path = var.platform_contract.health_check_path
              port = 8080
            }

            initial_delay_seconds = 15
            period_seconds        = 20
            timeout_seconds       = 5
            failure_threshold     = 3
          }

          volume_mount {
            name       = "data"
            mount_path = var.data_mount_path
          }

          dynamic "volume_mount" {
            for_each = local.secret_provider_class_name == null ? [] : [local.secret_provider_class_name]

            content {
              name       = "runtime-secrets"
              mount_path = var.runtime_secrets_mount_path
              read_only  = true
            }
          }
        }

        volume {
          name = "data"

          persistent_volume_claim {
            claim_name = var.platform_contract.pvc_name
          }
        }

        dynamic "volume" {
          for_each = local.secret_provider_class_name == null ? [] : [local.secret_provider_class_name]

          content {
            name = "runtime-secrets"

            csi {
              driver = "secrets-store-gke.csi.k8s.io"

              read_only = true

              volume_attributes = {
                secretProviderClass = volume.value
              }
            }
          }
        }
      }
    }
  }

  depends_on = [kubernetes_config_map_v1.app_config]
}
