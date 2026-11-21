resource "random_password" "sql_admin" {
  length           = 24
  special          = true
  override_special = "!#%*-_+"
  min_lower        = 2
  min_upper        = 2
  min_numeric      = 2
  min_special      = 2
}

resource "azurerm_mssql_server" "main" {
  name                         = "sql-${local.name_prefix}-${random_string.suffix.result}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = random_password.sql_admin.result
  minimum_tls_version          = "1.2"
  tags                         = local.tags
}

resource "azurerm_mssql_database" "main" {
  name        = "OrderSyncDB"
  server_id   = azurerm_mssql_server.main.id
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  sku_name    = var.database_sku_name
  max_size_gb = var.database_max_size_gb
  tags        = local.tags
}

# Permite que el App Service (y otros servicios de Azure) llegue al servidor.
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Opcional: la IP de quien administra la base desde SSMS.
resource "azurerm_mssql_firewall_rule" "admin_ip" {
  count            = var.admin_ip_address == "" ? 0 : 1
  name             = "AllowAdminIp"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = var.admin_ip_address
  end_ip_address   = var.admin_ip_address
}
