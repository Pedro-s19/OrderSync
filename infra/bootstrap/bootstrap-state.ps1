<#
 Prepara, UNA sola vez, lo que Terraform necesita antes de guardar su estado en Azure:
   - registra los proveedores de recursos que usa el proyecto,
   - crea el grupo de recursos y la cuenta de almacenamiento del estado remoto
     (con versionado de blobs, para poder recuperar un estado anterior),
   - da permiso sobre ese estado a su usuario y a un service principal para GitHub Actions,
   - escribe los archivos envs/dev/backend.hcl y envs/prod/backend.hcl con el nombre real de la cuenta.

 Requisitos: Azure CLI instalado y haber ejecutado "az login" con una cuenta que sea
 Propietario (Owner) de la suscripcion.

 Uso (desde la carpeta infra\bootstrap):
   .\bootstrap-state.ps1
#>
param(
    [string]$Location = "eastus2",
    [string]$ResourceGroup = "rg-ordersync-tfstate",
    [string]$Container = "tfstate",
    [string]$ServicePrincipalName = "sp-ordersync-github"
)

$ErrorActionPreference = "Stop"

function Test-AzResult([string]$What) {
    if ($LASTEXITCODE -ne 0) { throw "Fallo: $What" }
}

Write-Host "1/7 Verificando la sesion de Azure..."
$subscriptionId = az account show --query id -o tsv
Test-AzResult "az account show (ejecute primero: az login)"
$tenantId = az account show --query tenantId -o tsv
Test-AzResult "leer el tenant"

Write-Host "2/7 Registrando proveedores de recursos (puede tardar unos minutos)..."
foreach ($namespace in @("Microsoft.Storage", "Microsoft.Network", "Microsoft.Web", "Microsoft.Sql")) {
    az provider register --namespace $namespace --wait --output none
    Test-AzResult "registrar $namespace"
}

$suffix = -join ((48..57) + (97..122) | Get-Random -Count 6 | ForEach-Object { [char]$_ })
$storageAccount = "stordersynctf$suffix"

Write-Host "3/7 Creando el grupo de recursos $ResourceGroup y la cuenta $storageAccount..."
az group create --name $ResourceGroup --location $Location --output none
Test-AzResult "crear el grupo de recursos"

az storage account create --name $storageAccount --resource-group $ResourceGroup --location $Location `
    --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false --output none
Test-AzResult "crear la cuenta de almacenamiento"

az storage account blob-service-properties update --account-name $storageAccount --resource-group $ResourceGroup `
    --enable-versioning true --output none
Test-AzResult "activar el versionado de blobs"

$storageId = az storage account show --name $storageAccount --resource-group $ResourceGroup --query id -o tsv
Test-AzResult "leer el id de la cuenta"

# El contenedor se crea con la clave de la cuenta solo en este paso inicial; Terraform despues usa Azure AD.
$key = az storage account keys list --account-name $storageAccount --resource-group $ResourceGroup --query "[0].value" -o tsv
Test-AzResult "leer la clave de la cuenta"
az storage container create --name $Container --account-name $storageAccount --account-key $key --output none
Test-AzResult "crear el contenedor $Container"

Write-Host "4/7 Dando permiso sobre el estado a su usuario..."
$me = az ad signed-in-user show --query id -o tsv
Test-AzResult "leer su usuario de Azure AD"
az role assignment create --assignee-object-id $me --assignee-principal-type User `
    --role "Storage Blob Data Contributor" --scope $storageId --output none
Test-AzResult "asignar el rol a su usuario"

Write-Host "5/7 Creando el service principal para GitHub Actions ($ServicePrincipalName)..."
$sp = az ad sp create-for-rbac --name $ServicePrincipalName --role Contributor `
    --scopes "/subscriptions/$subscriptionId" --output json | ConvertFrom-Json
Test-AzResult "crear el service principal"

Write-Host "6/7 Dando al service principal permiso sobre el estado..."
az role assignment create --assignee $sp.appId --role "Storage Blob Data Contributor" --scope $storageId --output none
Test-AzResult "asignar el rol al service principal"

Write-Host "7/7 Escribiendo los archivos backend.hcl de cada entorno..."
foreach ($environmentName in @("dev", "prod")) {
    $path = Join-Path $PSScriptRoot "..\envs\$environmentName\backend.hcl"
    $content = @"
resource_group_name  = "$ResourceGroup"
storage_account_name = "$storageAccount"
container_name       = "$Container"
key                  = "ordersync-$environmentName.tfstate"
use_azuread_auth     = true
"@
    $content = $content -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($path, $content + "`n", (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host ""
Write-Host "Listo. Cuenta del estado remoto: $storageAccount (ya escrita en envs\dev\backend.hcl y envs\prod\backend.hcl)."
Write-Host ""
Write-Host "Agregue estos 4 secretos en GitHub (repositorio > Settings > Secrets and variables > Actions):"
Write-Host "  ARM_CLIENT_ID       = $($sp.appId)"
Write-Host "  ARM_CLIENT_SECRET   = $($sp.password)"
Write-Host "  ARM_SUBSCRIPTION_ID = $subscriptionId"
Write-Host "  ARM_TENANT_ID       = $tenantId"
Write-Host ""
Write-Host "El secreto del service principal solo se muestra ahora: copielo y no lo guarde en el repositorio."
