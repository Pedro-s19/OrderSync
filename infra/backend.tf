# Estado remoto en Azure Storage (blob). El bloqueo del estado lo da el "lease" del blob:
# si dos personas (o un PR y alguien en local) corren plan/apply a la vez, uno espera o falla.
# Los datos de la cuenta de almacenamiento NO van aquí: se pasan al inicializar, por entorno:
#   terraform init -backend-config=envs/dev/backend.hcl
terraform {
  backend "azurerm" {}
}
