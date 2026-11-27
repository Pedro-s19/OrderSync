# OrderSync ERP

Solución completa del backend ASP.NET Core API y frontend ASP.NET Core MVC/Razor.

## Abrir en Visual Studio
Abre `OrderSync.slnx`. Configura `OrderSync.Api` y `OrderSync.Web` como proyectos de inicio. Revisa `OrderSync-MVC-Razor/OrderSync.Web/appsettings.json` para confirmar `OrderSyncApi:BaseUrl`.

## Frontend
La guía de módulos, roles y ejecución está en `OrderSync-MVC-Razor/OrderSync.Web/LEEME-MODULOS-COMPLETOS.txt`.

La web consume los endpoints de la API y conserva la autorización del backend. No se conecta directamente a SQL Server.
