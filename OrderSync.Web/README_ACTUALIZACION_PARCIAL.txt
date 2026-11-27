ACTUALIZACIÓN PARCIAL DE FRONTEND - ORDERSYNC
============================================

Este paquete contiene únicamente archivos nuevos o reemplazados. No contiene la solución completa ni modifica la API o la base de datos.

INSTALACIÓN
1. Haz una copia de seguridad de tu carpeta actual:
   OrderSync-MVC-Razor/OrderSync.Web
2. Descomprime el ZIP en la carpeta raíz del proyecto OrderSync, conservando la estructura de carpetas.
3. Cuando Windows pregunte si deseas reemplazar archivos, acepta únicamente los archivos incluidos en este paquete.
4. Abre la solución y compila el proyecto MVC.

ARCHIVOS NUEVOS
- OrderSync-MVC-Razor/OrderSync.Web/Views/Operaciones/DetalleCompra.cshtml
- OrderSync-MVC-Razor/OrderSync.Web/Views/Gestion/Factura.cshtml
- OrderSync-MVC-Razor/OrderSync.Web/wwwroot/css/layout-fixes.css

ARCHIVOS REEMPLAZADOS
- Controllers/OperacionesController.cs: consulta de detalle de compra y registro de recepción.
- Controllers/GestionController.cs: detalle de factura y vinculación usuario-empleado.
- Views/Operaciones/Compras.cshtml: acceso al detalle de cada orden.
- Views/Gestion/Index.cshtml: acceso al detalle de factura y vinculación de empleado.
- Views/Shared/_Layout.cshtml: carga de la hoja de estilos correctiva.

QUÉ SE COMPLETA
- Compras: detalle de orden, cantidades solicitadas/recibidas, historial y registro de recepción parcial o total. La recepción utiliza POST api/ordenes-compra/{id}/recepciones.
- Facturación: detalle de factura y desglose de pagos. Utiliza GET api/facturacion/facturas/{id}.
- Usuarios: vinculación/desvinculación de empleado desde la tabla de usuarios. Utiliza PUT api/usuarios/{id}/empleado.
- Diseño: tablas con desplazamiento horizontal, formularios flexibles y ajustes para evitar desbordamientos en pantallas pequeñas.

PERMISOS
Las nuevas acciones respetan los roles ya definidos en el frontend y los endpoints existentes de la API. La recepción y vinculación de empleados quedan reservadas al Administrador del Sistema.

IMPORTANTE
- No se modifican los modelos, endpoints ni reglas de negocio del backend.
- No se agregan pantallas de procesos que no tengan un endpoint existente.
- La compilación debe realizarse en tu entorno con el SDK .NET usado por el proyecto y la API debe estar disponible para probar las operaciones.
- Antes de probar recepción, utiliza una orden en estado ENVIADA; el backend actualiza el inventario cuando acepta la recepción.
