resource "random_password" "jwt_key" {
  length  = 64
  special = false
}

locals {
  is_free_sku = var.app_service_sku == "F1"

  sql_connection_string = join(";", [
    "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433",
    "Initial Catalog=${azurerm_mssql_database.main.name}",
    "User ID=${var.sql_admin_login}",
    "Password=${random_password.sql_admin.result}",
    "Encrypt=True",
    "TrustServerCertificate=False",
    "Connection Timeout=30",
  ])
}

# App Service sobre Windows: es el entorno IIS que pide la guía.
resource "azurerm_service_plan" "main" {
  name                = "plan-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  os_type             = "Windows"
  sku_name            = var.app_service_sku
  tags                = local.tags
}

resource "azurerm_windows_web_app" "api" {
  name                = "app-${local.name_prefix}-${random_string.suffix.result}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true
  tags                = local.tags

  site_config {
    always_on           = !local.is_free_sku
    use_32_bit_worker   = local.is_free_sku
    ftps_state          = "Disabled"
    http2_enabled       = true
    minimum_tls_version = "1.2"

    application_stack {
      current_stack  = "dotnet"
      dotnet_version = var.dotnet_version
    }
  }

  # ASP.NET Core lee Jwt__Key como Jwt:Key. La cadena de conexión SQLAzure se expone
  # como ConnectionStrings:OrderSyncDb, que es lo que busca AddInfrastructure.
  app_settings = merge(
    {
      ASPNETCORE_ENVIRONMENT = var.aspnetcore_environment
      Jwt__Key               = random_password.jwt_key.result
    },
    var.seed_password == "" ? {} : { Seed__Password = var.seed_password }
  )

  connection_string {
    name  = "OrderSyncDb"
    type  = "SQLAzure"
    value = local.sql_connection_string
  }
}
