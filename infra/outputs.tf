output "resource_group_name" {
  description = "Grupo de recursos del entorno."
  value       = azurerm_resource_group.main.name
}

output "web_app_name" {
  description = "Nombre del App Service (se usa para publicar la API)."
  value       = azurerm_windows_web_app.api.name
}

output "web_app_url" {
  description = "URL pública de la API."
  value       = "https://${azurerm_windows_web_app.api.default_hostname}"
}

output "sql_server_fqdn" {
  description = "Servidor de Azure SQL (para conectarse desde SSMS)."
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "Nombre de la base de datos."
  value       = azurerm_mssql_database.main.name
}

output "sql_admin_password" {
  description = "Contraseña del administrador de Azure SQL (sensible; léala con: terraform output -raw sql_admin_password)."
  value       = random_password.sql_admin.result
  sensitive   = true
}
