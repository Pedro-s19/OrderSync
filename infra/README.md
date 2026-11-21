# Infraestructura como código (HU-07)

Terraform + Azure para OrderSync. Crea una red, una base de datos Azure SQL y un App Service
(Windows/IIS) para la API. El estado vive en Azure Storage con bloqueo, y hay dos entornos
separados (`dev` y `prod`) con su propio archivo de estado.

## Qué crea

| Recurso | Archivo |
|---|---|
| Grupo de recursos | `main.tf` |
| Red virtual con subred de aplicación y de datos | `network.tf` |
| Servidor y base Azure SQL (`OrderSyncDB`) + reglas de firewall | `database.tf` |
| Plan de App Service (Windows) + App Service de la API | `compute.tf` |
| Estado remoto (partial backend `azurerm`) | `backend.tf`, `envs/*/backend.hcl` |
| Variables y valores por entorno | `variables.tf`, `envs/dev/dev.tfvars`, `envs/prod/prod.tfvars` |

```
infra/
  backend.tf  compute.tf  database.tf  locals.tf  main.tf  network.tf  outputs.tf  variables.tf  versions.tf
  envs/dev/   (dev.tfvars, backend.hcl)
  envs/prod/  (prod.tfvars, backend.hcl)
  bootstrap/  bootstrap-state.ps1      <- se corre una sola vez
  sql/        scripts de la base para Azure SQL
.github/workflows/terraform-plan.yml   <- terraform plan en cada PR a develop
```

## Paso 0. Requisitos (cada integrante)

```powershell
winget install Hashicorp.Terraform
winget install Microsoft.AzureCLI
```

Cierre y abra la terminal. Compruebe con `terraform version` (1.9 o superior) y `az version`.

## Paso 1. Estado remoto y credenciales (UNA sola vez, un solo integrante)

El estado de Terraform no puede guardarse en el propio Terraform, así que se prepara con un script:

```powershell
az login
cd infra\bootstrap
.\bootstrap-state.ps1
```

El script registra los proveedores de Azure, crea la cuenta de almacenamiento del estado (con
versionado de blobs), da permisos, crea el service principal de GitHub y **escribe el nombre real de
la cuenta en `envs\dev\backend.hcl` y `envs\prod\backend.hcl`**. Al final imprime 4 valores.

Haga commit de los dos `backend.hcl` (no contienen secretos) para que todo el equipo use el mismo estado.

## Paso 2. GitHub

1. Cree la rama `develop` desde `main`.
2. **Settings → Secrets and variables → Actions → New repository secret**: cree `ARM_CLIENT_ID`,
   `ARM_CLIENT_SECRET`, `ARM_SUBSCRIPTION_ID` y `ARM_TENANT_ID` con los valores que imprimió el script.
3. **Settings → Branches → Add rule** para `main` y para `develop`: exigir Pull Request antes de fusionar y,
   en `develop`, exigir que pase la verificación **plan** (aparece en la lista después de la primera ejecución).
4. Abra un PR de prueba a `develop` que modifique un `.tf`: debe ejecutarse el workflow y comentar el plan.

## Paso 3. Usar Terraform en local

```powershell
az login
$env:ARM_SUBSCRIPTION_ID = (az account show --query id -o tsv)
cd infra
terraform init -backend-config="envs/dev/backend.hcl"
terraform fmt -recursive
terraform validate
terraform plan  -var-file="envs/dev/dev.tfvars"
terraform apply -var-file="envs/dev/dev.tfvars"
```

- La primera vez, haga commit de `.terraform.lock.hcl` (fija las versiones de los proveedores).
- Para las cuentas de prueba (solo `dev`): `$env:TF_VAR_seed_password = "SuContraseña123"` antes del apply.
- Para conectarse desde SSMS, agregue su IP: `-var "admin_ip_address=203.0.113.10"`.
- Para cambiar de entorno, repita `terraform init -reconfigure -backend-config="envs/prod/backend.hcl"`
  y use `envs/prod/prod.tfvars`.
- **Antes de abrir un PR** ejecute `terraform fmt -recursive`: el workflow falla si el formato no es el estándar.

## Paso 4. Base de datos y publicación de la API

1. Lea los datos de conexión: `terraform output` y `terraform output -raw sql_admin_password`.
2. En SSMS conéctese a `sql_server_fqdn` (usuario `ordersyncadmin`) **a la base `OrderSyncDB`**
   (Opciones → Propiedades de conexión → Conectar a la base de datos). Su IP debe estar permitida (`admin_ip_address`).
3. Ejecute `sql\OrderSyncDB_azure.sql` y luego `sql\OrderSyncDB_ParametrosNomina_azure.sql`. Son los mismos
   scripts del proyecto sin `CREATE DATABASE` ni `USE`, que Azure SQL no admite.
4. Publique la API:

```powershell
cd ..    # raíz de la solución
dotnet publish src\OrderSync.Api -c Release -o publish
Compress-Archive -Path publish\* -DestinationPath publish.zip -Force
az webapp deploy --resource-group <resource_group_name> --name <web_app_name> --src-path publish.zip --type zip
```

5. Pruebe en Postman: `POST <web_app_url>/api/auth/login`.

## Costos y limpieza

- `dev` usa App Service **F1 (gratis)** y SQL **Basic** (cuesta unos pocos dólares al mes). `prod` usa B1 y S0 (de pago).
- Para eliminar un entorno: `terraform destroy -var-file="envs/dev/dev.tfvars"` (borra también los datos de la base).
- El estado contiene la contraseña de SQL y la clave JWT generadas: por eso la cuenta de estado es privada,
  cifrada y con acceso solo por Azure AD. No comparta archivos `.tfstate`.

## Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| `Error acquiring the state lock` | Otra persona o un PR está ejecutando plan/apply. Espere. Si quedó colgado: `terraform force-unlock <ID>`. |
| `init` falla con 403 o `AuthorizationPermissionMismatch` | Falta el rol "Storage Blob Data Contributor" (puede tardar unos minutos en aplicarse tras el bootstrap). |
| `MissingSubscriptionRegistration` | Ejecute de nuevo el bootstrap (registra Microsoft.Web, Sql, Network y Storage). |
| `Operation cannot be completed without additional quota` (F1) | Solo se permite un plan gratuito por región: cambie `location` o `app_service_sku`. |
| `dotnet_version` no válido | Revise `az webapp list-runtimes --os windows` y la versión del proveedor `azurerm`. Si .NET 11 aún no está disponible, publique la API autocontenida (`dotnet publish -c Release -r win-x86 --self-contained true`) y deje una versión que exista. |
| `fmt -check` falla en el PR | Ejecute `terraform fmt -recursive` y haga commit. |
