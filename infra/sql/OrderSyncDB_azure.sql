/* Version para Azure SQL Database: sin CREATE DATABASE ni USE (la base la crea Terraform).
   Conectese con SSMS directamente a la base OrderSyncDB del servidor de Azure y ejecute este archivo. */
/* =====================================================================
   OrderSync ERP - Base de datos (SQL Server)
   Sprint 2 - Ingenieria de Software II - Universidad de Cundinamarca
   Idempotente: se puede ejecutar varias veces sin duplicar nada.
   Ejecutar en SSMS conectado a (localdb)\MSSQLLocalDB o a SQLEXPRESS.
   ===================================================================== */
/* ---------- Esquemas (uno por modulo) ---------- */
IF SCHEMA_ID(N'seguridad')   IS NULL EXEC(N'CREATE SCHEMA seguridad');
IF SCHEMA_ID(N'pedidos')     IS NULL EXEC(N'CREATE SCHEMA pedidos');
IF SCHEMA_ID(N'inventario')  IS NULL EXEC(N'CREATE SCHEMA inventario');
IF SCHEMA_ID(N'compras')     IS NULL EXEC(N'CREATE SCHEMA compras');
IF SCHEMA_ID(N'rrhh')        IS NULL EXEC(N'CREATE SCHEMA rrhh');
IF SCHEMA_ID(N'crm')         IS NULL EXEC(N'CREATE SCHEMA crm');
IF SCHEMA_ID(N'facturacion') IS NULL EXEC(N'CREATE SCHEMA facturacion');
GO

/* =====================================================================
   SEGURIDAD: roles y permisos por modulo (HU-06, NFR-06)
   Nivel de permiso: 0 = sin acceso, 1 = lectura, 2 = parcial (ver Alcance), 3 = total
   ===================================================================== */
IF OBJECT_ID(N'seguridad.Rol', N'U') IS NULL
CREATE TABLE seguridad.Rol (
    RolId        INT IDENTITY(1,1) CONSTRAINT PK_Rol PRIMARY KEY,
    Nombre       NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Rol_Nombre UNIQUE,
    Descripcion  NVARCHAR(200) NULL,
    Activo       BIT NOT NULL CONSTRAINT DF_Rol_Activo DEFAULT 1
);
GO
IF OBJECT_ID(N'seguridad.Modulo', N'U') IS NULL
CREATE TABLE seguridad.Modulo (
    ModuloId INT IDENTITY(1,1) CONSTRAINT PK_Modulo PRIMARY KEY,
    Codigo   NVARCHAR(30) NOT NULL CONSTRAINT UQ_Modulo_Codigo UNIQUE,
    Nombre   NVARCHAR(80) NOT NULL
);
GO
IF OBJECT_ID(N'seguridad.Permiso', N'U') IS NULL
CREATE TABLE seguridad.Permiso (
    RolId    INT NOT NULL CONSTRAINT FK_Permiso_Rol    REFERENCES seguridad.Rol(RolId),
    ModuloId INT NOT NULL CONSTRAINT FK_Permiso_Modulo REFERENCES seguridad.Modulo(ModuloId),
    Nivel    TINYINT NOT NULL CONSTRAINT CK_Permiso_Nivel CHECK (Nivel BETWEEN 0 AND 3),
    Alcance  NVARCHAR(100) NULL,
    CONSTRAINT PK_Permiso PRIMARY KEY (RolId, ModuloId)
);
GO

/* =====================================================================
   RRHH: empleados, turnos, asistencia, nomina (HU-04, FR-13..15, FR-24)
   ===================================================================== */
IF OBJECT_ID(N'rrhh.Empleado', N'U') IS NULL
CREATE TABLE rrhh.Empleado (
    EmpleadoId      INT IDENTITY(1,1) CONSTRAINT PK_Empleado PRIMARY KEY,
    Documento       NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Empleado_Documento UNIQUE,
    Nombre          NVARCHAR(100) NOT NULL,
    AreaCargo       NVARCHAR(60)  NOT NULL,
    TipoContrato    NVARCHAR(30)  NOT NULL CONSTRAINT DF_Empleado_Contrato DEFAULT N'TERMINO_FIJO',
    TarifaHora      DECIMAL(18,2) NOT NULL CONSTRAINT CK_Empleado_Tarifa CHECK (TarifaHora > 0),
    SalarioMensual  DECIMAL(18,2) NULL CONSTRAINT CK_Empleado_Salario CHECK (SalarioMensual IS NULL OR SalarioMensual > 0),
    FechaIngreso    DATE NOT NULL CONSTRAINT DF_Empleado_Ingreso DEFAULT CAST(SYSUTCDATETIME() AS DATE),
    Activo          BIT  NOT NULL CONSTRAINT DF_Empleado_Activo DEFAULT 1
);
GO

/* ---------- Usuarios (despues de Empleado para poder enlazarlos) ---------- */
IF OBJECT_ID(N'seguridad.Usuario', N'U') IS NULL
CREATE TABLE seguridad.Usuario (
    UsuarioId     INT IDENTITY(1,1) CONSTRAINT PK_Usuario PRIMARY KEY,
    Nombre        NVARCHAR(100) NOT NULL,
    Email         NVARCHAR(150) NOT NULL CONSTRAINT UQ_Usuario_Email UNIQUE,
    PasswordHash  NVARCHAR(200) NOT NULL,   -- hash BCrypt, nunca la clave en texto
    RolId         INT NOT NULL CONSTRAINT FK_Usuario_Rol REFERENCES seguridad.Rol(RolId),
    EmpleadoId    INT NULL CONSTRAINT FK_Usuario_Empleado REFERENCES rrhh.Empleado(EmpleadoId),
    Activo        BIT NOT NULL CONSTRAINT DF_Usuario_Activo DEFAULT 1,
    FechaCreacion DATETIME2 NOT NULL CONSTRAINT DF_Usuario_Fecha DEFAULT SYSUTCDATETIME()
);
GO
/* Bitacora de cambios de rol (NFR-10): usuario, fecha, accion y rol afectado */
IF OBJECT_ID(N'seguridad.BitacoraRol', N'U') IS NULL
CREATE TABLE seguridad.BitacoraRol (
    BitacoraId       BIGINT IDENTITY(1,1) CONSTRAINT PK_BitacoraRol PRIMARY KEY,
    AdminUsuarioId   INT NOT NULL CONSTRAINT FK_Bitacora_Admin    REFERENCES seguridad.Usuario(UsuarioId),
    UsuarioAfectadoId INT NOT NULL CONSTRAINT FK_Bitacora_Afectado REFERENCES seguridad.Usuario(UsuarioId),
    RolAnteriorId    INT NULL CONSTRAINT FK_Bitacora_RolAnt REFERENCES seguridad.Rol(RolId),
    RolNuevoId       INT NULL CONSTRAINT FK_Bitacora_RolNue REFERENCES seguridad.Rol(RolId),
    Accion           NVARCHAR(30) NOT NULL CONSTRAINT CK_Bitacora_Accion CHECK (Accion IN (N'ASIGNAR', N'CAMBIAR', N'REVOCAR')),
    Fecha            DATETIME2 NOT NULL CONSTRAINT DF_Bitacora_Fecha DEFAULT SYSUTCDATETIME()
);
GO

/* =====================================================================
   CRM: clientes, promociones, puntos (FR-16..18, FR-25)
   ===================================================================== */
IF OBJECT_ID(N'crm.Cliente', N'U') IS NULL
CREATE TABLE crm.Cliente (
    ClienteId        INT IDENTITY(1,1) CONSTRAINT PK_Cliente PRIMARY KEY,
    Nombre           NVARCHAR(100) NOT NULL,
    Telefono         NVARCHAR(20)  NULL,
    Email            NVARCHAR(150) NULL,
    Direccion        NVARCHAR(200) NULL,
    PuntosFidelidad  INT NOT NULL CONSTRAINT DF_Cliente_Puntos DEFAULT 0 CONSTRAINT CK_Cliente_Puntos CHECK (PuntosFidelidad >= 0),
    FechaRegistro    DATETIME2 NOT NULL CONSTRAINT DF_Cliente_Fecha DEFAULT SYSUTCDATETIME(),
    Activo           BIT NOT NULL CONSTRAINT DF_Cliente_Activo DEFAULT 1
);
GO

/* =====================================================================
   PEDIDOS: productos del menu, pedidos y detalle (FR-01..05, FR-21)
   ===================================================================== */
IF OBJECT_ID(N'pedidos.Producto', N'U') IS NULL
CREATE TABLE pedidos.Producto (
    ProductoId INT IDENTITY(1,1) CONSTRAINT PK_Producto PRIMARY KEY,
    Nombre     NVARCHAR(100) NOT NULL CONSTRAINT UQ_Producto_Nombre UNIQUE,
    Categoria  NVARCHAR(50)  NULL,
    Precio     DECIMAL(18,2) NOT NULL CONSTRAINT CK_Producto_Precio CHECK (Precio >= 0),
    Activo     BIT NOT NULL CONSTRAINT DF_Producto_Activo DEFAULT 1
);
GO
IF OBJECT_ID(N'pedidos.Pedido', N'U') IS NULL
CREATE TABLE pedidos.Pedido (
    PedidoId          INT IDENTITY(1,1) CONSTRAINT PK_Pedido PRIMARY KEY,
    ClienteId         INT NULL CONSTRAINT FK_Pedido_Cliente REFERENCES crm.Cliente(ClienteId),
    UsuarioId         INT NULL CONSTRAINT FK_Pedido_Usuario REFERENCES seguridad.Usuario(UsuarioId),
    Canal             NVARCHAR(20) NOT NULL CONSTRAINT CK_Pedido_Canal CHECK (Canal IN (N'MOSTRADOR', N'DOMICILIO', N'QR')),
    Estado            NVARCHAR(20) NOT NULL CONSTRAINT DF_Pedido_Estado DEFAULT N'BORRADOR'
                      CONSTRAINT CK_Pedido_Estado CHECK (Estado IN (N'BORRADOR', N'CONFIRMADO', N'EN_COCINA', N'LISTO', N'ENTREGADO', N'CANCELADO')),
    Mesa              NVARCHAR(10)  NULL,
    DireccionEntrega  NVARCHAR(200) NULL,
    TelefonoContacto  NVARCHAR(20)  NULL,
    Total             DECIMAL(18,2) NOT NULL CONSTRAINT DF_Pedido_Total DEFAULT 0,
    FechaCreacion     DATETIME2 NOT NULL CONSTRAINT DF_Pedido_Fecha DEFAULT SYSUTCDATETIME(),
    FechaEnvioCocina  DATETIME2 NULL,
    FechaListo        DATETIME2 NULL,
    -- FR-02: un pedido a domicilio exige direccion y contacto
    CONSTRAINT CK_Pedido_Domicilio CHECK (Canal <> N'DOMICILIO' OR (DireccionEntrega IS NOT NULL AND TelefonoContacto IS NOT NULL))
);
GO
IF OBJECT_ID(N'pedidos.DetallePedido', N'U') IS NULL
CREATE TABLE pedidos.DetallePedido (
    DetalleId      INT IDENTITY(1,1) CONSTRAINT PK_DetallePedido PRIMARY KEY,
    PedidoId       INT NOT NULL CONSTRAINT FK_Detalle_Pedido   REFERENCES pedidos.Pedido(PedidoId),
    ProductoId     INT NOT NULL CONSTRAINT FK_Detalle_Producto REFERENCES pedidos.Producto(ProductoId),
    Cantidad       INT NOT NULL CONSTRAINT CK_Detalle_Cantidad CHECK (Cantidad > 0),
    PrecioUnitario DECIMAL(18,2) NOT NULL CONSTRAINT CK_Detalle_Precio CHECK (PrecioUnitario >= 0)
);
GO

/* =====================================================================
   INVENTARIO: ingredientes, recetas, movimientos, alertas (HU-05, FR-06,07,09,22)
   ===================================================================== */
IF OBJECT_ID(N'inventario.Ingrediente', N'U') IS NULL
CREATE TABLE inventario.Ingrediente (
    IngredienteId INT IDENTITY(1,1) CONSTRAINT PK_Ingrediente PRIMARY KEY,
    Nombre        NVARCHAR(100) NOT NULL CONSTRAINT UQ_Ingrediente_Nombre UNIQUE,
    UnidadMedida  NVARCHAR(10)  NOT NULL,
    StockActual   DECIMAL(18,3) NOT NULL CONSTRAINT DF_Ingrediente_Stock DEFAULT 0 CONSTRAINT CK_Ingrediente_Stock CHECK (StockActual >= 0),
    StockMinimo   DECIMAL(18,3) NOT NULL CONSTRAINT DF_Ingrediente_Min   DEFAULT 0 CONSTRAINT CK_Ingrediente_Min   CHECK (StockMinimo >= 0),
    Activo        BIT NOT NULL CONSTRAINT DF_Ingrediente_Activo DEFAULT 1
);
GO
IF OBJECT_ID(N'inventario.Receta', N'U') IS NULL
CREATE TABLE inventario.Receta (
    ProductoId    INT NOT NULL CONSTRAINT FK_Receta_Producto    REFERENCES pedidos.Producto(ProductoId),
    IngredienteId INT NOT NULL CONSTRAINT FK_Receta_Ingrediente REFERENCES inventario.Ingrediente(IngredienteId),
    Cantidad      DECIMAL(18,3) NOT NULL CONSTRAINT CK_Receta_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT PK_Receta PRIMARY KEY (ProductoId, IngredienteId)
);
GO
IF OBJECT_ID(N'inventario.MovimientoInventario', N'U') IS NULL
CREATE TABLE inventario.MovimientoInventario (
    MovimientoId  BIGINT IDENTITY(1,1) CONSTRAINT PK_MovInv PRIMARY KEY,
    IngredienteId INT NOT NULL CONSTRAINT FK_MovInv_Ingrediente REFERENCES inventario.Ingrediente(IngredienteId),
    Tipo          NVARCHAR(20) NOT NULL CONSTRAINT CK_MovInv_Tipo CHECK (Tipo IN (N'VENTA', N'RECEPCION', N'MERMA', N'AJUSTE', N'CANCELACION')),
    Cantidad      DECIMAL(18,3) NOT NULL,          -- con signo: negativo descuenta, positivo suma
    PedidoId      INT NULL CONSTRAINT FK_MovInv_Pedido  REFERENCES pedidos.Pedido(PedidoId),
    UsuarioId     INT NULL CONSTRAINT FK_MovInv_Usuario REFERENCES seguridad.Usuario(UsuarioId),
    Motivo        NVARCHAR(200) NULL,
    Fecha         DATETIME2 NOT NULL CONSTRAINT DF_MovInv_Fecha DEFAULT SYSUTCDATETIME()
);
GO
IF OBJECT_ID(N'inventario.AlertaStock', N'U') IS NULL
CREATE TABLE inventario.AlertaStock (
    AlertaId       INT IDENTITY(1,1) CONSTRAINT PK_AlertaStock PRIMARY KEY,
    IngredienteId  INT NOT NULL CONSTRAINT FK_Alerta_Ingrediente REFERENCES inventario.Ingrediente(IngredienteId),
    StockAlMomento DECIMAL(18,3) NOT NULL,
    StockMinimo    DECIMAL(18,3) NOT NULL,
    Estado         NVARCHAR(15) NOT NULL CONSTRAINT DF_Alerta_Estado DEFAULT N'ABIERTA'
                   CONSTRAINT CK_Alerta_Estado CHECK (Estado IN (N'ABIERTA', N'ATENDIDA')),
    Fecha          DATETIME2 NOT NULL CONSTRAINT DF_Alerta_Fecha DEFAULT SYSUTCDATETIME(),
    FechaAtencion  DATETIME2 NULL
);
GO

/* =====================================================================
   COMPRAS / PROVEEDORES (FR-08, FR-19, FR-20, FR-23)
   ===================================================================== */
IF OBJECT_ID(N'compras.Proveedor', N'U') IS NULL
CREATE TABLE compras.Proveedor (
    ProveedorId INT IDENTITY(1,1) CONSTRAINT PK_Proveedor PRIMARY KEY,
    Nombre      NVARCHAR(120) NOT NULL CONSTRAINT UQ_Proveedor_Nombre UNIQUE,
    Contacto    NVARCHAR(100) NOT NULL,
    Telefono    NVARCHAR(20)  NULL,
    Email       NVARCHAR(150) NULL,
    Activo      BIT NOT NULL CONSTRAINT DF_Proveedor_Activo DEFAULT 1
);
GO
/* Lista de precios: base de la comparacion de precios (FR-20) */
IF OBJECT_ID(N'compras.ProveedorIngrediente', N'U') IS NULL
CREATE TABLE compras.ProveedorIngrediente (
    ProveedorId    INT NOT NULL CONSTRAINT FK_ProvIng_Proveedor   REFERENCES compras.Proveedor(ProveedorId),
    IngredienteId  INT NOT NULL CONSTRAINT FK_ProvIng_Ingrediente REFERENCES inventario.Ingrediente(IngredienteId),
    PrecioUnitario DECIMAL(18,2) NOT NULL CONSTRAINT CK_ProvIng_Precio CHECK (PrecioUnitario > 0),
    FechaPrecio    DATE NOT NULL CONSTRAINT DF_ProvIng_Fecha DEFAULT CAST(SYSUTCDATETIME() AS DATE),
    CONSTRAINT PK_ProvIng PRIMARY KEY (ProveedorId, IngredienteId)
);
GO
IF OBJECT_ID(N'compras.OrdenCompra', N'U') IS NULL
CREATE TABLE compras.OrdenCompra (
    OrdenCompraId  INT IDENTITY(1,1) CONSTRAINT PK_OrdenCompra PRIMARY KEY,
    ProveedorId    INT NOT NULL CONSTRAINT FK_OC_Proveedor REFERENCES compras.Proveedor(ProveedorId),
    UsuarioId      INT NULL CONSTRAINT FK_OC_Usuario REFERENCES seguridad.Usuario(UsuarioId),
    AlertaOrigenId INT NULL CONSTRAINT FK_OC_Alerta  REFERENCES inventario.AlertaStock(AlertaId), -- Sprint 3 (US-INV-04)
    Estado         NVARCHAR(15) NOT NULL CONSTRAINT DF_OC_Estado DEFAULT N'BORRADOR'
                   CONSTRAINT CK_OC_Estado CHECK (Estado IN (N'BORRADOR', N'ENVIADA', N'RECIBIDA', N'CANCELADA')),
    FechaCreacion  DATETIME2 NOT NULL CONSTRAINT DF_OC_Fecha DEFAULT SYSUTCDATETIME(),
    FechaEsperada  DATE NULL
);
GO
IF OBJECT_ID(N'compras.DetalleOrdenCompra', N'U') IS NULL
CREATE TABLE compras.DetalleOrdenCompra (
    DetalleId        INT IDENTITY(1,1) CONSTRAINT PK_DetalleOC PRIMARY KEY,
    OrdenCompraId    INT NOT NULL CONSTRAINT FK_DetOC_Orden       REFERENCES compras.OrdenCompra(OrdenCompraId),
    IngredienteId    INT NOT NULL CONSTRAINT FK_DetOC_Ingrediente REFERENCES inventario.Ingrediente(IngredienteId),
    Cantidad         DECIMAL(18,3) NOT NULL CONSTRAINT CK_DetOC_Cant CHECK (Cantidad > 0),
    PrecioUnitario   DECIMAL(18,2) NOT NULL CONSTRAINT CK_DetOC_Precio CHECK (PrecioUnitario > 0),
    CantidadRecibida DECIMAL(18,3) NOT NULL CONSTRAINT DF_DetOC_Rec DEFAULT 0
);
GO
IF OBJECT_ID(N'compras.RecepcionMercancia', N'U') IS NULL
CREATE TABLE compras.RecepcionMercancia (
    RecepcionId   INT IDENTITY(1,1) CONSTRAINT PK_Recepcion PRIMARY KEY,
    OrdenCompraId INT NOT NULL CONSTRAINT FK_Recepcion_Orden   REFERENCES compras.OrdenCompra(OrdenCompraId),
    UsuarioId     INT NULL CONSTRAINT FK_Recepcion_Usuario REFERENCES seguridad.Usuario(UsuarioId),
    Fecha         DATETIME2 NOT NULL CONSTRAINT DF_Recepcion_Fecha DEFAULT SYSUTCDATETIME(),
    EntregaATiempo BIT NULL,                -- insumo del ranking de proveedores (Sprint 3)
    Observaciones NVARCHAR(300) NULL
);
GO

/* =====================================================================
   RRHH (continuacion): turnos, asistencia y nomina
   ===================================================================== */
IF OBJECT_ID(N'rrhh.Turno', N'U') IS NULL
CREATE TABLE rrhh.Turno (
    TurnoId    INT IDENTITY(1,1) CONSTRAINT PK_Turno PRIMARY KEY,
    EmpleadoId INT NOT NULL CONSTRAINT FK_Turno_Empleado REFERENCES rrhh.Empleado(EmpleadoId),
    Inicio     DATETIME2 NOT NULL,
    Fin        DATETIME2 NOT NULL,
    Estado     NVARCHAR(15) NOT NULL CONSTRAINT DF_Turno_Estado DEFAULT N'PROGRAMADO'
               CONSTRAINT CK_Turno_Estado CHECK (Estado IN (N'PROGRAMADO', N'CUMPLIDO', N'CANCELADO')),
    CONSTRAINT CK_Turno_Rango CHECK (Fin > Inicio)   -- el cruce de horarios se valida en el servicio (FR-13)
);
GO
/* FR-14: la asistencia SIEMPRE pertenece a un turno programado (TurnoId NOT NULL).
   Salida NULL = turno sin cierre: el PayrollEngine lo rechaza con error de validacion. */
IF OBJECT_ID(N'rrhh.Asistencia', N'U') IS NULL
CREATE TABLE rrhh.Asistencia (
    AsistenciaId INT IDENTITY(1,1) CONSTRAINT PK_Asistencia PRIMARY KEY,
    TurnoId      INT NOT NULL CONSTRAINT FK_Asistencia_Turno REFERENCES rrhh.Turno(TurnoId),
    Entrada      DATETIME2 NOT NULL,
    Salida       DATETIME2 NULL,
    CONSTRAINT CK_Asistencia_Rango CHECK (Salida IS NULL OR Salida > Entrada)
);
GO
/* Parametros de nomina editables sin tocar codigo (CP-08) */
IF OBJECT_ID(N'rrhh.ParametroNomina', N'U') IS NULL
CREATE TABLE rrhh.ParametroNomina (
    Clave       NVARCHAR(50)  NOT NULL CONSTRAINT PK_ParametroNomina PRIMARY KEY,
    Valor       DECIMAL(18,4) NOT NULL,
    Descripcion NVARCHAR(200) NULL,
    Vigencia    INT NOT NULL
);
GO
IF OBJECT_ID(N'rrhh.LiquidacionNomina', N'U') IS NULL
CREATE TABLE rrhh.LiquidacionNomina (
    LiquidacionId    INT IDENTITY(1,1) CONSTRAINT PK_Liquidacion PRIMARY KEY,
    EmpleadoId       INT NOT NULL CONSTRAINT FK_Liq_Empleado REFERENCES rrhh.Empleado(EmpleadoId),
    UsuarioId        INT NULL CONSTRAINT FK_Liq_Usuario REFERENCES seguridad.Usuario(UsuarioId),
    Anio             INT NOT NULL,
    Mes              TINYINT NOT NULL CONSTRAINT CK_Liq_Mes CHECK (Mes BETWEEN 1 AND 12),
    HorasOrdinarias  DECIMAL(10,2) NOT NULL,
    HorasExtra       DECIMAL(10,2) NOT NULL CONSTRAINT DF_Liq_Extra DEFAULT 0,
    PagoBruto        DECIMAL(18,2) NOT NULL,
    Salud            DECIMAL(18,2) NOT NULL,
    Pension          DECIMAL(18,2) NOT NULL,
    Solidaridad      DECIMAL(18,2) NOT NULL CONSTRAINT DF_Liq_Solid DEFAULT 0,
    AuxilioTransporte DECIMAL(18,2) NOT NULL CONSTRAINT DF_Liq_Aux DEFAULT 0,
    Neto             DECIMAL(18,2) NOT NULL,
    FechaCalculo     DATETIME2 NOT NULL CONSTRAINT DF_Liq_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Liq_EmpleadoPeriodo UNIQUE (EmpleadoId, Anio, Mes)
);
GO

/* =====================================================================
   FACTURACION Y VENTAS (FR-10..12, FR-26)
   La factura existe solo cuando el pago se confirma (FR-10).
   ===================================================================== */
IF OBJECT_ID(N'facturacion.Factura', N'U') IS NULL
CREATE TABLE facturacion.Factura (
    FacturaId INT IDENTITY(1,1) CONSTRAINT PK_Factura PRIMARY KEY,
    PedidoId  INT NOT NULL CONSTRAINT UQ_Factura_Pedido UNIQUE   -- relacion 1 a 1 con el pedido
              CONSTRAINT FK_Factura_Pedido REFERENCES pedidos.Pedido(PedidoId),
    Fecha     DATETIME2 NOT NULL CONSTRAINT DF_Factura_Fecha DEFAULT SYSUTCDATETIME(),
    Subtotal  DECIMAL(18,2) NOT NULL CONSTRAINT CK_Factura_Sub CHECK (Subtotal >= 0),
    Propina   DECIMAL(18,2) NOT NULL CONSTRAINT DF_Factura_Prop DEFAULT 0 CONSTRAINT CK_Factura_Prop CHECK (Propina >= 0),
    Total     DECIMAL(18,2) NOT NULL
);
GO
IF OBJECT_ID(N'facturacion.Pago', N'U') IS NULL
CREATE TABLE facturacion.Pago (
    PagoId    INT IDENTITY(1,1) CONSTRAINT PK_Pago PRIMARY KEY,
    FacturaId INT NOT NULL CONSTRAINT FK_Pago_Factura REFERENCES facturacion.Factura(FacturaId),
    Metodo    NVARCHAR(15) NOT NULL CONSTRAINT CK_Pago_Metodo CHECK (Metodo IN (N'EFECTIVO', N'TARJETA', N'DIGITAL')),
    Monto     DECIMAL(18,2) NOT NULL CONSTRAINT CK_Pago_Monto CHECK (Monto > 0),
    Fecha     DATETIME2 NOT NULL CONSTRAINT DF_Pago_Fecha DEFAULT SYSUTCDATETIME()
);
GO

/* =====================================================================
   CRM (continuacion): promociones y puntos
   ===================================================================== */
IF OBJECT_ID(N'crm.Promocion', N'U') IS NULL
CREATE TABLE crm.Promocion (
    PromocionId   INT IDENTITY(1,1) CONSTRAINT PK_Promocion PRIMARY KEY,
    Nombre        NVARCHAR(100) NOT NULL,
    PorcentajeDto DECIMAL(5,2)  NOT NULL CONSTRAINT CK_Promo_Dto CHECK (PorcentajeDto > 0 AND PorcentajeDto <= 100),
    PuntosMinimos INT NULL,
    VigenteDesde  DATE NOT NULL,
    VigenteHasta  DATE NULL,
    Activa        BIT NOT NULL CONSTRAINT DF_Promo_Activa DEFAULT 1
);
GO
IF OBJECT_ID(N'crm.ClientePromocion', N'U') IS NULL
CREATE TABLE crm.ClientePromocion (
    ClienteId   INT NOT NULL CONSTRAINT FK_CliPromo_Cliente   REFERENCES crm.Cliente(ClienteId),
    PromocionId INT NOT NULL CONSTRAINT FK_CliPromo_Promocion REFERENCES crm.Promocion(PromocionId),
    CONSTRAINT PK_ClientePromocion PRIMARY KEY (ClienteId, PromocionId)
);
GO
IF OBJECT_ID(N'crm.MovimientoPuntos', N'U') IS NULL
CREATE TABLE crm.MovimientoPuntos (
    MovimientoId BIGINT IDENTITY(1,1) CONSTRAINT PK_MovPuntos PRIMARY KEY,
    ClienteId    INT NOT NULL CONSTRAINT FK_MovPuntos_Cliente REFERENCES crm.Cliente(ClienteId),
    PedidoId     INT NULL CONSTRAINT FK_MovPuntos_Pedido REFERENCES pedidos.Pedido(PedidoId),
    Puntos       INT NOT NULL,
    Fecha        DATETIME2 NOT NULL CONSTRAINT DF_MovPuntos_Fecha DEFAULT SYSUTCDATETIME()
);
GO

/* ---------- Indices para los reportes y consultas del panel ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedido_Estado_Fecha')
    CREATE INDEX IX_Pedido_Estado_Fecha ON pedidos.Pedido (Estado, FechaCreacion);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Detalle_Pedido')
    CREATE INDEX IX_Detalle_Pedido ON pedidos.DetallePedido (PedidoId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MovInv_Ingrediente_Fecha')
    CREATE INDEX IX_MovInv_Ingrediente_Fecha ON inventario.MovimientoInventario (IngredienteId, Fecha);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Alerta_Estado')
    CREATE INDEX IX_Alerta_Estado ON inventario.AlertaStock (Estado, IngredienteId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Turno_Empleado_Inicio')
    CREATE INDEX IX_Turno_Empleado_Inicio ON rrhh.Turno (EmpleadoId, Inicio);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asistencia_Turno')
    CREATE INDEX IX_Asistencia_Turno ON rrhh.Asistencia (TurnoId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_Fecha')
    CREATE INDEX IX_Factura_Fecha ON facturacion.Factura (Fecha);
GO

/* =====================================================================
   DATOS INICIALES
   ===================================================================== */

/* ---------- Roles ---------- */
INSERT INTO seguridad.Rol (Nombre, Descripcion)
SELECT v.Nombre, v.Descripcion
FROM (VALUES
    (N'Cajero',                 N'Toma pedidos, cobra y gestiona puntos del cliente'),
    (N'Cocina',                 N'Ve los pedidos y los marca como listos'),
    (N'Supervisor',             N'Turnos, nomina, alertas, mermas y ajustes de inventario'),
    (N'Gerencia',               N'Consulta y reportes; promociones'),
    (N'Administrador del Sistema', N'Acceso total y administración de todos los módulos del ERP')
) AS v(Nombre, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM seguridad.Rol r WHERE r.Nombre = v.Nombre);
GO

UPDATE seguridad.Rol
SET Descripcion = N'Acceso total y administración de todos los módulos del ERP'
WHERE Nombre = N'Administrador del Sistema';
GO

/* ---------- Modulos ---------- */
INSERT INTO seguridad.Modulo (Codigo, Nombre)
SELECT v.Codigo, v.Nombre
FROM (VALUES
    (N'PEDIDOS',     N'Pedidos'),
    (N'INVENTARIO',  N'Inventario'),
    (N'PROVEEDORES', N'Proveedores'),
    (N'RRHH',        N'Recursos Humanos y Nomina'),
    (N'CRM',         N'CRM (Clientes)'),
    (N'FACTURACION', N'Facturacion y Ventas'),
    (N'USUARIOS',    N'Usuarios y roles')
) AS v(Codigo, Nombre)
WHERE NOT EXISTS (SELECT 1 FROM seguridad.Modulo m WHERE m.Codigo = v.Codigo);
GO

/* ---------- Matriz de roles y permisos (Tabla 10 del informe del Sprint 2) ---------- */
;WITH M(Rol, Modulo, Nivel, Alcance) AS (
    SELECT * FROM (VALUES
    (N'Cajero', N'PEDIDOS', 3, NULL),
    (N'Cajero', N'INVENTARIO', 0, NULL),
    (N'Cajero', N'PROVEEDORES', 0, NULL),
    (N'Cajero', N'RRHH', 2, N'Solo su asistencia'),
    (N'Cajero', N'CRM', 2, N'Puntos e historial'),
    (N'Cajero', N'FACTURACION', 2, N'Cobro'),
    (N'Cajero', N'USUARIOS', 0, NULL),

    (N'Cocina', N'PEDIDOS', 2, N'Ver y marcar listo'),
    (N'Cocina', N'INVENTARIO', 0, NULL),
    (N'Cocina', N'PROVEEDORES', 0, NULL),
    (N'Cocina', N'RRHH', 2, N'Solo su asistencia'),
    (N'Cocina', N'CRM', 0, NULL),
    (N'Cocina', N'FACTURACION', 0, NULL),
    (N'Cocina', N'USUARIOS', 0, NULL),

    (N'Supervisor', N'PEDIDOS', 2, N'Cancelar y consultar'),
    (N'Supervisor', N'INVENTARIO', 2, N'Alertas, mermas y ajustes'),
    (N'Supervisor', N'PROVEEDORES', 1, NULL),
    (N'Supervisor', N'RRHH', 2, N'Turnos y pago'),
    (N'Supervisor', N'CRM', 1, NULL),
    (N'Supervisor', N'FACTURACION', 1, NULL),
    (N'Supervisor', N'USUARIOS', 0, NULL),

    (N'Gerencia', N'PEDIDOS', 1, NULL),
    (N'Gerencia', N'INVENTARIO', 1, NULL),
    (N'Gerencia', N'PROVEEDORES', 1, NULL),
    (N'Gerencia', N'RRHH', 1, NULL),
    (N'Gerencia', N'CRM', 2, N'Promociones'),
    (N'Gerencia', N'FACTURACION', 2, N'Reportes'),
    (N'Gerencia', N'USUARIOS', 0, NULL),

    (N'Administrador del Sistema', N'PEDIDOS', 3, NULL),
    (N'Administrador del Sistema', N'INVENTARIO', 3, NULL),
    (N'Administrador del Sistema', N'PROVEEDORES', 3, NULL),
    (N'Administrador del Sistema', N'RRHH', 3, NULL),
    (N'Administrador del Sistema', N'CRM', 3, NULL),
    (N'Administrador del Sistema', N'FACTURACION', 3, NULL),
    (N'Administrador del Sistema', N'USUARIOS', 3, NULL)
    ) AS x(Rol, Modulo, Nivel, Alcance)
)
INSERT INTO seguridad.Permiso (RolId, ModuloId, Nivel, Alcance)
SELECT r.RolId, mo.ModuloId, M.Nivel, M.Alcance
FROM M
JOIN seguridad.Rol    r  ON r.Nombre  = M.Rol
JOIN seguridad.Modulo mo ON mo.Codigo = M.Modulo
WHERE NOT EXISTS (SELECT 1 FROM seguridad.Permiso p WHERE p.RolId = r.RolId AND p.ModuloId = mo.ModuloId);
GO

/* El Administrador del Sistema conserva acceso total también en bases ya existentes. */
UPDATE p
SET p.Nivel = 3,
    p.Alcance = NULL
FROM seguridad.Permiso p
INNER JOIN seguridad.Rol r ON r.RolId = p.RolId
WHERE r.Nombre = N'Administrador del Sistema';
GO

/* ---------- Parametros de nomina 2026 (valores del informe; confirmar en el spike) ---------- */
INSERT INTO rrhh.ParametroNomina (Clave, Valor, Descripcion, Vigencia)
SELECT v.Clave, v.Valor, v.Descripcion, 2026
FROM (VALUES
    (N'SMMLV',                  1750905.0000, N'Salario minimo mensual legal vigente'),
    (N'AUXILIO_TRANSPORTE',      249095.0000, N'Auxilio de transporte mensual'),
    (N'SALUD_EMPLEADO',               0.0400, N'Aporte de salud a cargo del empleado'),
    (N'PENSION_EMPLEADO',             0.0400, N'Aporte de pension a cargo del empleado'),
    (N'SOLIDARIDAD_PENSIONAL',        0.0100, N'Fondo de solidaridad desde 4 SMMLV (confirmar escalas)'),
    (N'RECARGO_EXTRA_DIURNA',         0.2500, N'Recargo de la hora extra diurna'),
    (N'LIMITE_AUXILIO_SMMLV',         2.0000, N'Auxilio aplica hasta este numero de SMMLV'),
    (N'LIMITE_SOLIDARIDAD_SMMLV',     4.0000, N'Solidaridad aplica desde este numero de SMMLV')
) AS v(Clave, Valor, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM rrhh.ParametroNomina p WHERE p.Clave = v.Clave);
GO

/* ---------- Datos de demostracion (borrar en produccion) ---------- */
INSERT INTO inventario.Ingrediente (Nombre, UnidadMedida, StockActual, StockMinimo)
SELECT v.Nombre, v.Unidad, v.Stock, v.Minimo
FROM (VALUES
    (N'Pan de hamburguesa', N'und', 100.0, 20.0),
    (N'Carne 125 g',        N'und',  80.0, 15.0),
    (N'Queso cheddar',      N'lon',  90.0, 20.0),
    (N'Lechuga',            N'g',  3000.0, 500.0),
    (N'Tomate',             N'g',  3000.0, 500.0)
) AS v(Nombre, Unidad, Stock, Minimo)
WHERE NOT EXISTS (SELECT 1 FROM inventario.Ingrediente i WHERE i.Nombre = v.Nombre);

INSERT INTO pedidos.Producto (Nombre, Categoria, Precio)
SELECT v.Nombre, v.Categoria, v.Precio
FROM (VALUES (N'Hamburguesa clasica', N'Hamburguesas', 18000.00)) AS v(Nombre, Categoria, Precio)
WHERE NOT EXISTS (SELECT 1 FROM pedidos.Producto p WHERE p.Nombre = v.Nombre);

INSERT INTO inventario.Receta (ProductoId, IngredienteId, Cantidad)
SELECT p.ProductoId, i.IngredienteId, r.Cantidad
FROM (VALUES
    (N'Hamburguesa clasica', N'Pan de hamburguesa', 1.0),
    (N'Hamburguesa clasica', N'Carne 125 g',        1.0),
    (N'Hamburguesa clasica', N'Queso cheddar',      1.0),
    (N'Hamburguesa clasica', N'Lechuga',           30.0),
    (N'Hamburguesa clasica', N'Tomate',            40.0)
) AS r(Producto, Ingrediente, Cantidad)
JOIN pedidos.Producto p ON p.Nombre = r.Producto
JOIN inventario.Ingrediente i ON i.Nombre = r.Ingrediente
WHERE NOT EXISTS (SELECT 1 FROM inventario.Receta x WHERE x.ProductoId = p.ProductoId AND x.IngredienteId = i.IngredienteId);
GO

PRINT N'OrderSyncDB lista.';
GO
