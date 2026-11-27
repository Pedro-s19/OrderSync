using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public partial class OrderSyncDbContext : DbContext
{
    public OrderSyncDbContext(DbContextOptions<OrderSyncDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AlertaStock> AlertaStocks { get; set; }

    public virtual DbSet<Asistencium> Asistencia { get; set; }

    public virtual DbSet<BitacoraRol> BitacoraRols { get; set; }

    public virtual DbSet<Cliente> Clientes { get; set; }

    public virtual DbSet<DetalleOrdenCompra> DetalleOrdenCompras { get; set; }

    public virtual DbSet<DetallePedido> DetallePedidos { get; set; }

    public virtual DbSet<Empleado> Empleados { get; set; }

    public virtual DbSet<Factura> Facturas { get; set; }

    public virtual DbSet<Ingrediente> Ingredientes { get; set; }

    public virtual DbSet<LiquidacionNomina> LiquidacionNominas { get; set; }

    public virtual DbSet<Modulo> Modulos { get; set; }

    public virtual DbSet<MovimientoInventario> MovimientoInventarios { get; set; }

    public virtual DbSet<MovimientoPunto> MovimientoPuntos { get; set; }

    public virtual DbSet<OrdenCompra> OrdenCompras { get; set; }

    public virtual DbSet<Pago> Pagos { get; set; }

    public virtual DbSet<ParametroNomina> ParametroNominas { get; set; }

    public virtual DbSet<Pedido> Pedidos { get; set; }

    public virtual DbSet<Permiso> Permisos { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    public virtual DbSet<Promocion> Promocions { get; set; }

    public virtual DbSet<Proveedor> Proveedors { get; set; }

    public virtual DbSet<ProveedorIngrediente> ProveedorIngredientes { get; set; }

    public virtual DbSet<RecepcionMercancium> RecepcionMercancia { get; set; }

    public virtual DbSet<Recetum> Receta { get; set; }

    public virtual DbSet<Rol> Rols { get; set; }

    public virtual DbSet<Turno> Turnos { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    public virtual DbSet<RecuperacionContrasena> RecuperacionContrasenas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlertaStock>(entity =>
        {
            entity.HasKey(e => e.AlertaId);

            entity.ToTable("AlertaStock", "inventario");

            entity.HasIndex(e => new { e.Estado, e.IngredienteId }, "IX_Alerta_Estado");

            entity.Property(e => e.Estado)
                .HasMaxLength(15)
                .HasDefaultValue("ABIERTA", "DF_Alerta_Estado");
            entity.Property(e => e.Fecha).HasDefaultValueSql("(sysutcdatetime())", "DF_Alerta_Fecha");
            entity.Property(e => e.StockAlMomento).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.StockMinimo).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Ingrediente).WithMany(p => p.AlertaStocks)
                .HasForeignKey(d => d.IngredienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alerta_Ingrediente");
        });

        modelBuilder.Entity<Asistencium>(entity =>
        {
            entity.HasKey(e => e.AsistenciaId);

            entity.ToTable("Asistencia", "rrhh");

            entity.HasIndex(e => e.TurnoId, "IX_Asistencia_Turno");

            entity.HasOne(d => d.Turno).WithMany(p => p.Asistencia)
                .HasForeignKey(d => d.TurnoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Asistencia_Turno");
        });

        modelBuilder.Entity<BitacoraRol>(entity =>
        {
            entity.HasKey(e => e.BitacoraId);

            entity.ToTable("BitacoraRol", "seguridad");

            entity.Property(e => e.Accion).HasMaxLength(30);
            entity.Property(e => e.Fecha).HasDefaultValueSql("(sysutcdatetime())", "DF_Bitacora_Fecha");

            entity.HasOne(d => d.AdminUsuario).WithMany(p => p.BitacoraRolAdminUsuarios)
                .HasForeignKey(d => d.AdminUsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bitacora_Admin");

            entity.HasOne(d => d.RolAnterior).WithMany(p => p.BitacoraRolRolAnteriors)
                .HasForeignKey(d => d.RolAnteriorId)
                .HasConstraintName("FK_Bitacora_RolAnt");

            entity.HasOne(d => d.RolNuevo).WithMany(p => p.BitacoraRolRolNuevos)
                .HasForeignKey(d => d.RolNuevoId)
                .HasConstraintName("FK_Bitacora_RolNue");

            entity.HasOne(d => d.UsuarioAfectado).WithMany(p => p.BitacoraRolUsuarioAfectados)
                .HasForeignKey(d => d.UsuarioAfectadoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bitacora_Afectado");
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Cliente", "crm");

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Cliente_Activo");
            entity.Property(e => e.Direccion).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(sysutcdatetime())", "DF_Cliente_Fecha");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Telefono).HasMaxLength(20);

            entity.HasMany(d => d.Promocions).WithMany(p => p.Clientes)
                .UsingEntity<Dictionary<string, object>>(
                    "ClientePromocion",
                    r => r.HasOne<Promocion>().WithMany()
                        .HasForeignKey("PromocionId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_CliPromo_Promocion"),
                    l => l.HasOne<Cliente>().WithMany()
                        .HasForeignKey("ClienteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_CliPromo_Cliente"),
                    j =>
                    {
                        j.HasKey("ClienteId", "PromocionId");
                        j.ToTable("ClientePromocion", "crm");
                        j.IndexerProperty<int>("ClienteId");
                        j.IndexerProperty<int>("PromocionId");
                    });
        });

        modelBuilder.Entity<DetalleOrdenCompra>(entity =>
        {
            entity.HasKey(e => e.DetalleId).HasName("PK_DetalleOC");

            entity.ToTable("DetalleOrdenCompra", "compras");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.CantidadRecibida).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Ingrediente).WithMany(p => p.DetalleOrdenCompras)
                .HasForeignKey(d => d.IngredienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetOC_Ingrediente");

            entity.HasOne(d => d.OrdenCompra).WithMany(p => p.DetalleOrdenCompras)
                .HasForeignKey(d => d.OrdenCompraId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetOC_Orden");
        });

        modelBuilder.Entity<DetallePedido>(entity =>
        {
            entity.HasKey(e => e.DetalleId);

            entity.ToTable("DetallePedido", "pedidos");

            entity.HasIndex(e => e.PedidoId, "IX_Detalle_Pedido");

            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Pedido).WithMany(p => p.DetallePedidos)
                .HasForeignKey(d => d.PedidoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Detalle_Pedido");

            entity.HasOne(d => d.Producto).WithMany(p => p.DetallePedidos)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Detalle_Producto");
        });

        modelBuilder.Entity<Empleado>(entity =>
        {
            entity.ToTable("Empleado", "rrhh");

            entity.HasIndex(e => e.Documento, "UQ_Empleado_Documento").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Empleado_Activo");
            entity.Property(e => e.AreaCargo).HasMaxLength(60);
            entity.Property(e => e.Documento).HasMaxLength(20);
            entity.Property(e => e.FechaIngreso).HasDefaultValueSql("(CONVERT([date],sysutcdatetime()))", "DF_Empleado_Ingreso");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.SalarioMensual).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TarifaHora).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TipoContrato)
                .HasMaxLength(30)
                .HasDefaultValue("TERMINO_FIJO", "DF_Empleado_Contrato");
        });

        modelBuilder.Entity<Factura>(entity =>
        {
            entity.ToTable("Factura", "facturacion");

            entity.HasIndex(e => e.Fecha, "IX_Factura_Fecha");

            entity.HasIndex(e => e.PedidoId, "UQ_Factura_Pedido").IsUnique();

            entity.Property(e => e.Fecha).HasDefaultValueSql("(sysutcdatetime())", "DF_Factura_Fecha");
            entity.Property(e => e.Propina).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Pedido).WithOne(p => p.Factura)
                .HasForeignKey<Factura>(d => d.PedidoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Factura_Pedido");
        });

        modelBuilder.Entity<Ingrediente>(entity =>
        {
            entity.ToTable("Ingrediente", "inventario");

            entity.HasIndex(e => e.Nombre, "UQ_Ingrediente_Nombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Ingrediente_Activo");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.StockActual).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.StockMinimo).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnidadMedida).HasMaxLength(10);
        });

        modelBuilder.Entity<LiquidacionNomina>(entity =>
        {
            entity.HasKey(e => e.LiquidacionId).HasName("PK_Liquidacion");

            entity.ToTable("LiquidacionNomina", "rrhh");

            entity.HasIndex(e => new { e.EmpleadoId, e.Anio, e.Mes }, "UQ_Liq_EmpleadoPeriodo").IsUnique();

            entity.Property(e => e.AuxilioTransporte).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FechaCalculo).HasDefaultValueSql("(sysutcdatetime())", "DF_Liq_Fecha");
            entity.Property(e => e.HorasExtra).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.HorasOrdinarias).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Neto).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PagoBruto).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Pension).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Salud).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Solidaridad).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Empleado).WithMany(p => p.LiquidacionNominas)
                .HasForeignKey(d => d.EmpleadoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Liq_Empleado");

            entity.HasOne(d => d.Usuario).WithMany(p => p.LiquidacionNominas)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("FK_Liq_Usuario");
        });

        modelBuilder.Entity<Modulo>(entity =>
        {
            entity.ToTable("Modulo", "seguridad");

            entity.HasIndex(e => e.Codigo, "UQ_Modulo_Codigo").IsUnique();

            entity.Property(e => e.Codigo).HasMaxLength(30);
            entity.Property(e => e.Nombre).HasMaxLength(80);
        });

        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.HasKey(e => e.MovimientoId).HasName("PK_MovInv");

            entity.ToTable("MovimientoInventario", "inventario");

            entity.HasIndex(e => new { e.IngredienteId, e.Fecha }, "IX_MovInv_Ingrediente_Fecha");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Fecha).HasDefaultValueSql("(sysutcdatetime())", "DF_MovInv_Fecha");
            entity.Property(e => e.Motivo).HasMaxLength(200);
            entity.Property(e => e.Tipo).HasMaxLength(20);

            entity.HasOne(d => d.Ingrediente).WithMany(p => p.MovimientoInventarios)
                .HasForeignKey(d => d.IngredienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MovInv_Ingrediente");

            entity.HasOne(d => d.Pedido).WithMany(p => p.MovimientoInventarios)
                .HasForeignKey(d => d.PedidoId)
                .HasConstraintName("FK_MovInv_Pedido");

            entity.HasOne(d => d.Usuario).WithMany(p => p.MovimientoInventarios)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("FK_MovInv_Usuario");
        });

        modelBuilder.Entity<MovimientoPunto>(entity =>
        {
            entity.HasKey(e => e.MovimientoId).HasName("PK_MovPuntos");

            entity.ToTable("MovimientoPuntos", "crm");

            entity.Property(e => e.Fecha).HasDefaultValueSql("(sysutcdatetime())", "DF_MovPuntos_Fecha");

            entity.HasOne(d => d.Cliente).WithMany(p => p.MovimientoPuntos)
                .HasForeignKey(d => d.ClienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MovPuntos_Cliente");

            entity.HasOne(d => d.Pedido).WithMany(p => p.MovimientoPuntos)
                .HasForeignKey(d => d.PedidoId)
                .HasConstraintName("FK_MovPuntos_Pedido");
        });

        modelBuilder.Entity<OrdenCompra>(entity =>
        {
            entity.ToTable("OrdenCompra", "compras");

            entity.Property(e => e.Estado)
                .HasMaxLength(15)
                .HasDefaultValue("BORRADOR", "DF_OC_Estado");
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("(sysutcdatetime())", "DF_OC_Fecha");

            entity.HasOne(d => d.AlertaOrigen).WithMany(p => p.OrdenCompras)
                .HasForeignKey(d => d.AlertaOrigenId)
                .HasConstraintName("FK_OC_Alerta");

            entity.HasOne(d => d.Proveedor).WithMany(p => p.OrdenCompras)
                .HasForeignKey(d => d.ProveedorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OC_Proveedor");

            entity.HasOne(d => d.Usuario).WithMany(p => p.OrdenCompras)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("FK_OC_Usuario");
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("Pago", "facturacion");

            entity.Property(e => e.Fecha).HasDefaultValueSql("(sysutcdatetime())", "DF_Pago_Fecha");
            entity.Property(e => e.Metodo).HasMaxLength(15);
            entity.Property(e => e.Monto).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Factura).WithMany(p => p.Pagos)
                .HasForeignKey(d => d.FacturaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pago_Factura");
        });

        modelBuilder.Entity<ParametroNomina>(entity =>
        {
            entity.HasKey(e => e.Clave);

            entity.ToTable("ParametroNomina", "rrhh");

            entity.Property(e => e.Clave).HasMaxLength(50);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Valor).HasColumnType("decimal(18, 4)");
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("Pedido", "pedidos");

            entity.HasIndex(e => new { e.Estado, e.FechaCreacion }, "IX_Pedido_Estado_Fecha");

            entity.Property(e => e.Canal).HasMaxLength(20);
            entity.Property(e => e.DireccionEntrega).HasMaxLength(200);
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("BORRADOR", "DF_Pedido_Estado");
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("(sysutcdatetime())", "DF_Pedido_Fecha");
            entity.Property(e => e.Mesa).HasMaxLength(10);
            entity.Property(e => e.TelefonoContacto).HasMaxLength(20);
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cliente).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.ClienteId)
                .HasConstraintName("FK_Pedido_Cliente");

            entity.HasOne(d => d.Usuario).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("FK_Pedido_Usuario");
        });

        modelBuilder.Entity<Permiso>(entity =>
        {
            entity.HasKey(e => new { e.RolId, e.ModuloId });

            entity.ToTable("Permiso", "seguridad");

            entity.Property(e => e.Alcance).HasMaxLength(100);

            entity.HasOne(d => d.Modulo).WithMany(p => p.Permisos)
                .HasForeignKey(d => d.ModuloId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Permiso_Modulo");

            entity.HasOne(d => d.Rol).WithMany(p => p.Permisos)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Permiso_Rol");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Producto", "pedidos");

            entity.HasIndex(e => e.Nombre, "UQ_Producto_Nombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Producto_Activo");
            entity.Property(e => e.Categoria).HasMaxLength(50);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Precio).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<Promocion>(entity =>
        {
            entity.ToTable("Promocion", "crm");

            entity.Property(e => e.Activa).HasDefaultValue(true, "DF_Promo_Activa");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.PorcentajeDto).HasColumnType("decimal(5, 2)");
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.ToTable("Proveedor", "compras");

            entity.HasIndex(e => e.Nombre, "UQ_Proveedor_Nombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Proveedor_Activo");
            entity.Property(e => e.Contacto).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Nombre).HasMaxLength(120);
            entity.Property(e => e.Telefono).HasMaxLength(20);
        });

        modelBuilder.Entity<ProveedorIngrediente>(entity =>
        {
            entity.HasKey(e => new { e.ProveedorId, e.IngredienteId }).HasName("PK_ProvIng");

            entity.ToTable("ProveedorIngrediente", "compras");

            entity.Property(e => e.FechaPrecio).HasDefaultValueSql("(CONVERT([date],sysutcdatetime()))", "DF_ProvIng_Fecha");
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Ingrediente).WithMany(p => p.ProveedorIngredientes)
                .HasForeignKey(d => d.IngredienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProvIng_Ingrediente");

            entity.HasOne(d => d.Proveedor).WithMany(p => p.ProveedorIngredientes)
                .HasForeignKey(d => d.ProveedorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProvIng_Proveedor");
        });

        modelBuilder.Entity<RecepcionMercancium>(entity =>
        {
            entity.HasKey(e => e.RecepcionId).HasName("PK_Recepcion");

            entity.ToTable("RecepcionMercancia", "compras");

            entity.Property(e => e.EntregaAtiempo).HasColumnName("EntregaATiempo");
            entity.Property(e => e.Fecha).HasDefaultValueSql("(sysutcdatetime())", "DF_Recepcion_Fecha");
            entity.Property(e => e.Observaciones).HasMaxLength(300);

            entity.HasOne(d => d.OrdenCompra).WithMany(p => p.RecepcionMercancia)
                .HasForeignKey(d => d.OrdenCompraId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Recepcion_Orden");

            entity.HasOne(d => d.Usuario).WithMany(p => p.RecepcionMercancia)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("FK_Recepcion_Usuario");
        });

        modelBuilder.Entity<Recetum>(entity =>
        {
            entity.HasKey(e => new { e.ProductoId, e.IngredienteId });

            entity.ToTable("Receta", "inventario");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Ingrediente).WithMany(p => p.Receta)
                .HasForeignKey(d => d.IngredienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Receta_Ingrediente");

            entity.HasOne(d => d.Producto).WithMany(p => p.Receta)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Receta_Producto");
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("Rol", "seguridad");

            entity.HasIndex(e => e.Nombre, "UQ_Rol_Nombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Rol_Activo");
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<Turno>(entity =>
        {
            entity.ToTable("Turno", "rrhh");

            entity.HasIndex(e => new { e.EmpleadoId, e.Inicio }, "IX_Turno_Empleado_Inicio");

            entity.Property(e => e.Estado)
                .HasMaxLength(15)
                .HasDefaultValue("PROGRAMADO", "DF_Turno_Estado");

            entity.HasOne(d => d.Empleado).WithMany(p => p.Turnos)
                .HasForeignKey(d => d.EmpleadoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Turno_Empleado");



            modelBuilder.Entity<RecuperacionContrasena>(entity =>
            {
                entity.HasKey(e => e.RecuperacionId);

                entity.ToTable("RecuperacionContrasena", "seguridad");

                entity.HasIndex(e => e.UsuarioId, "IX_Recuperacion_Usuario");

                entity.Property(e => e.CodigoHash)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(e => e.FechaCreacion)
                    .HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.FechaExpiracion)
                    .IsRequired();

                entity.Property(e => e.Usado)
                    .HasDefaultValue(false);

                entity.Property(e => e.Intentos)
                    .HasDefaultValue(0);

                entity.HasOne<Usuario>()
                    .WithMany()
                    .HasForeignKey(e => e.UsuarioId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Recuperacion_Usuario");
            });

        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuario", "seguridad");

            entity.HasIndex(e => e.Email, "UQ_Usuario_Email").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true, "DF_Usuario_Activo");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("(sysutcdatetime())", "DF_Usuario_Fecha");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(200);

            entity.HasOne(d => d.Empleado).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.EmpleadoId)
                .HasConstraintName("FK_Usuario_Empleado");

            entity.HasOne(d => d.Rol).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Usuario_Rol");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
