locals {
  name_prefix = "${var.project_name}-${var.environment}"

  tags = {
    project     = var.project_name
    environment = var.environment
    managed_by  = "terraform"
    course      = "arquitectura-de-software-udec"
  }
}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}
