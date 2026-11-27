# OrderSync.Web — ASP.NET Core MVC + Razor

Frontend MVC independiente para la API existente de OrderSync.

## Requisitos
- Visual Studio compatible con el SDK .NET 11 Preview (el backend actual apunta a `net11.0`).
- API OrderSync ejecutándose y base de datos configurada.

## Configuración
En `appsettings.json`, ajusta `OrderSyncApi:BaseUrl` al puerto HTTP real de tu API. Valor inicial: `http://localhost:5092/`.

## Ejecutar
1. Abre `OrderSync.Web.csproj` en Visual Studio.
2. Confirma que OrderSync.Api esté ejecutándose.
3. Ejecuta OrderSync.Web (F5).
4. Inicia sesión con una cuenta de desarrollo creada por el seeder de la API.

El login envía `{ email, password, rol }` a `POST api/auth/login` y conserva el JWT en Session del servidor MVC. Las llamadas protegidas envían `Authorization: Bearer`.

## Incluido en esta primera entrega
- Login conectado a `POST api/auth/login`.
- Dashboard base (sin cifras inventadas).
- Pedidos: listado y acciones de confirmar / marcar listo.
- Productos: catálogo.
- Inventario: alertas abiertas.
- Estilos responsive con identidad morado, mostaza, crema y rojo.

## Pendiente
- Completar CRUD de productos e ingredientes, alta de pedidos, recetas, proveedores, compras, RR. HH., CRM, facturación, reportes y administración.
- Ajustar los DTO de lectura de pedidos/alertas a los contratos reales si el backend cambia.
- Añadir pruebas de integración y manejo centralizado de errores.

## Seguridad
El JWT se guarda en Session del servidor MVC, no en localStorage. La API sigue siendo la autoridad para validar roles y permisos. No publiques credenciales de desarrollo.
