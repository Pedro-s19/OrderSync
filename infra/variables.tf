variable "environment" {
  description = "Entorno a desplegar: dev o prod. Define nombres, tamaños y el archivo de estado."
  type        = string

  validation {
    condition     = contains(["dev", "prod"], var.environment)
    error_message = "El entorno debe ser dev o prod."
  }
}

variable "project_name" {
  description = "Prefijo que identifica el proyecto en los nombres de los recursos."
  type        = string
  default     = "ordersync"
}

variable "location" {
  description = "Región de Azure donde se crean los recursos."
  type        = string
  default     = "eastus2"
}

variable "vnet_address_space" {
  description = "Rango de direcciones de la red virtual (un rango distinto por entorno)."
  type        = list(string)
  default     = ["10.10.0.0/16"]
}

variable "app_service_sku" {
  description = "Plan de App Service. F1 es la capa gratuita; B1 o superior para producción."
  type        = string
  default     = "F1"
}

variable "dotnet_version" {
  description = "Versión de .NET del App Service (formato vNN.0). Verifique cuál ofrece Azure con: az webapp list-runtimes --os windows"
  type        = string
  default     = "v10.0"
}

variable "aspnetcore_environment" {
  description = "Valor de ASPNETCORE_ENVIRONMENT. Con Development se crean las cuentas de prueba y se expone OpenAPI."
  type        = string
  default     = "Production"
}

variable "sql_admin_login" {
  description = "Usuario administrador de Azure SQL (no puede ser admin, sa ni root)."
  type        = string
  default     = "ordersyncadmin"
}

variable "database_sku_name" {
  description = "Nivel de la base de datos. Basic es el más económico; S0 o superior para producción."
  type        = string
  default     = "Basic"
}

variable "database_max_size_gb" {
  description = "Tamaño máximo de la base de datos en GB (Basic admite hasta 2)."
  type        = number
  default     = 2
}

variable "admin_ip_address" {
  description = "IP pública con permiso para conectarse a Azure SQL desde SSMS. Vacío = solo servicios de Azure."
  type        = string
  default     = ""
}

variable "seed_password" {
  description = "Contraseña de las cuentas de prueba (solo con ASPNETCORE_ENVIRONMENT=Development). Pásela con TF_VAR_seed_password."
  type        = string
  default     = ""
  sensitive   = true
}
