/* Version para Azure SQL Database: sin CREATE DATABASE ni USE (la base la crea Terraform).
   Conectese con SSMS directamente a la base OrderSyncDB del servidor de Azure y ejecute este archivo. */
/* Parametros de nomina adicionales que usa el PayrollEngine (HU-04).
   Ejecutar una vez sobre OrderSyncDB. Es idempotente. */
INSERT INTO rrhh.ParametroNomina (Clave, Valor, Descripcion, Vigencia)
SELECT v.Clave, v.Valor, v.Descripcion, 2026
FROM (VALUES
    (N'HORAS_JORNADA',            8.0000, N'Horas ordinarias por turno; lo que exceda es hora extra'),
    (N'PENSION_EMPLEADOR',        0.1200, N'Aporte de pension a cargo del empleador'),
    (N'CAJA_COMPENSACION',        0.0400, N'Aporte a caja de compensacion (empleador)'),
    (N'SALUD_EMPLEADOR',          0.0850, N'Aporte de salud del empleador (exonerado bajo 10 SMMLV)'),
    (N'SENA',                     0.0200, N'Aporte SENA (exonerado bajo 10 SMMLV)'),
    (N'ICBF',                     0.0300, N'Aporte ICBF (exonerado bajo 10 SMMLV)'),
    (N'LIMITE_EXONERACION_SMMLV', 10.0000, N'Salario bajo este numero de SMMLV exonera salud, SENA e ICBF')
) AS v(Clave, Valor, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM rrhh.ParametroNomina p WHERE p.Clave = v.Clave);
GO
