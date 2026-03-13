provider "kubernetes" {
  config_path = pathexpand(var.kubeconfig_path)
}
