/*
    OrderSync - Actualización de permisos del Administrador del Sistema
    Ejecutar una sola vez sobre OrderSyncDB. Es idempotente: puede repetirse.
    No elimina ni modifica usuarios, pedidos ni datos operativos.
*/
USE OrderSyncDB;
GO

UPDATE seguridad.Rol
SET Descripcion = N'Acceso total y administración de todos los módulos del ERP'
WHERE Nombre = N'Administrador del Sistema';
GO

/* Crea cualquier permiso del administrador que todavía no exista. */
INSERT INTO seguridad.Permiso (RolId, ModuloId, Nivel, Alcance)
SELECT
    rol.RolId,
    modulo.ModuloId,
    3,
    NULL
FROM seguridad.Rol AS rol
CROSS JOIN seguridad.Modulo AS modulo
WHERE rol.Nombre = N'Administrador del Sistema'
  AND NOT EXISTS
  (
      SELECT 1
      FROM seguridad.Permiso AS existente
      WHERE existente.RolId = rol.RolId
        AND existente.ModuloId = modulo.ModuloId
  );
GO

UPDATE permiso
SET permiso.Nivel = 3,
    permiso.Alcance = NULL
FROM seguridad.Permiso AS permiso
INNER JOIN seguridad.Rol AS rol
    ON rol.RolId = permiso.RolId
WHERE rol.Nombre = N'Administrador del Sistema';
GO

/* Verificación: deben aparecer los siete módulos con Nivel = 3. */
SELECT
    rol.Nombre AS Rol,
    modulo.Codigo AS Modulo,
    permiso.Nivel,
    permiso.Alcance
FROM seguridad.Permiso AS permiso
INNER JOIN seguridad.Rol AS rol
    ON rol.RolId = permiso.RolId
INNER JOIN seguridad.Modulo AS modulo
    ON modulo.ModuloId = permiso.ModuloId
WHERE rol.Nombre = N'Administrador del Sistema'
ORDER BY modulo.Codigo;
GO
