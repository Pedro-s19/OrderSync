# Cambios de esta entrega

## 1. Permisos del Administrador del Sistema

- Se asignó nivel `3` (Total) al Administrador del Sistema en Pedidos, Inventario, Proveedores, RR. HH., CRM, Facturación y Usuarios.
- Se actualizó la descripción del rol.
- El script principal de base de datos actualiza permisos existentes y no se limita a insertar filas faltantes.
- Se agregó `infra/sql/ActualizarPermisosAdministrador.sql` para aplicar el cambio en una base local ya creada.
- Los permisos de Cajero, Cocina, Supervisor y Gerencia se conservaron.

## 2. Frontend MVC

- Se agregó `Security/RoleAccess.cs` para centralizar la regla de acceso total del Administrador del Sistema.
- Se actualizaron las validaciones MVC de pedidos, cocina, gestión, operaciones y panel para que el administrador pueda acceder a las operaciones de los módulos.
- Se mantuvieron restringidas las funciones personales de asistencia (`MiAsistencia` y `MarcarAsistencia`) a Cajero y Cocina.
- Se alinearon las opciones del menú lateral con los permisos de lectura y operación de cada rol.
- Se ajustaron las vistas de pedidos, productos e inventario para no mostrar acciones que el rol no puede ejecutar.

## 3. Formato y distribución visual

- Se reorganizó la sangría de `_Layout.cshtml` y de los controladores/vistas MVC intervenidos.
- Se dio formato legible a las hojas CSS.
- Se agregó `wwwroot/css/layout-fixes.css` para que el menú lateral pueda desplazarse cuando contiene muchos módulos, evitar desbordamientos horizontales y adaptar tablas, tarjetas y formularios a pantallas pequeñas.
- Se agregó `.editorconfig` para mantener un formato consistente en futuras modificaciones.

## 4. Verificación pendiente en el equipo de desarrollo

Este paquete fue revisado estáticamente, pero en el entorno donde se preparó no está instalado el SDK de .NET; por eso no fue posible ejecutar `dotnet build` ni `dotnet test` aquí.

Después de descomprimir el proyecto:

1. Abrir `OrderSync.slnx` en Visual Studio.
2. Ejecutar `infra/sql/ActualizarPermisosAdministrador.sql` sobre `OrderSyncDB`.
3. Compilar la solución en Visual Studio.
4. Probar el inicio de sesión con una cuenta de cada rol.
5. Verificar que el Administrador del Sistema vea todos los módulos y que las acciones restringidas sigan bloqueadas para los demás roles.
6. Revisar el diseño en una ventana de escritorio y otra de ancho móvil.

No se agregaron datos de prueba ni se modificó la recuperación de contraseña o el envío de correos.
