# OrderSync - Política de acceso por roles

## Decisión aplicada

El rol **Administrador del Sistema** tiene acceso total (nivel 3) a los siete módulos del ERP. Esta decisión se aplica tanto a los permisos almacenados en `seguridad.Permiso` como a las comprobaciones de acceso del frontend MVC.

Los permisos de Cajero, Cocina, Supervisor y Gerencia se conservan según la matriz existente del proyecto. No se amplían sus facultades en este cambio.

## Matriz de permisos

| Módulo | Cajero | Cocina | Supervisor | Gerencia | Administrador del Sistema |
|---|---:|---:|---:|---:|---:|
| Pedidos | 3 | 2 | 2 | 1 | 3 |
| Inventario | 0 | 0 | 2 | 1 | 3 |
| Proveedores | 0 | 0 | 1 | 1 | 3 |
| RR. HH. y nómina | 2 | 2 | 2 | 1 | 3 |
| CRM | 2 | 0 | 1 | 2 | 3 |
| Facturación y ventas | 2 | 0 | 1 | 2 | 3 |
| Usuarios y roles | 0 | 0 | 0 | 0 | 3 |

## Niveles

- `0`: sin acceso.
- `1`: lectura.
- `2`: acceso parcial, limitado por el campo `Alcance`.
- `3`: acceso total al módulo.

## Cambios incluidos

- El script principal `infra/sql/OrderSyncDB_azure.sql` actualiza los permisos del Administrador del Sistema a nivel 3 incluso si las filas ya existían.
- `infra/sql/ActualizarPermisosAdministrador.sql` permite aplicar la actualización sobre una base `OrderSyncDB` ya creada, sin borrar datos.
- La clase `OrderSync.Web.Security.RoleAccess` centraliza la excepción del Administrador del Sistema en las validaciones MVC.
- La barra lateral muestra al Administrador los accesos a todos los módulos disponibles en la aplicación.
- Se conserva la autorización de la API: ocultar o mostrar enlaces en MVC no sustituye la validación del token y de `seguridad.Permiso` en el backend.

## Aplicar en una base local ya existente

1. Abrir SQL Server Management Studio o el explorador de SQL Server.
2. Seleccionar la base `OrderSyncDB`.
3. Ejecutar `infra/sql/ActualizarPermisosAdministrador.sql`.
4. Verificar que la consulta final devuelva los siete módulos con `Nivel = 3` y `Alcance = NULL`.
5. Cerrar sesión e iniciar sesión nuevamente en OrderSync para probar el menú y los endpoints con el rol Administrador del Sistema.

## Alcance y precauciones

Este cambio no convierte al administrador en el usuario de asistencia personal de Cajero o Cocina. Las pantallas `MiAsistencia` y `MarcarAsistencia` siguen siendo operaciones personales de esos roles. La administración general de asistencia está disponible mediante el módulo de RR. HH.

Las reglas especiales de negocio (por ejemplo, impedir que un administrador cambie su propio rol) se mantienen y no se eliminan por conceder acceso total a los módulos.
