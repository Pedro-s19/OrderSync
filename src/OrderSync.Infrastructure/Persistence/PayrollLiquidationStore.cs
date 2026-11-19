using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Nomina;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

/// <summary>Guarda en rrhh.LiquidacionNomina; si ya existe (empleado, año, mes) la actualiza.</summary>
public sealed class PayrollLiquidationStore : IPayrollLiquidationStore
{
    private readonly OrderSyncDbContext _db;

    public PayrollLiquidationStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task SaveAsync(PayrollLiquidation liquidation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(liquidation);

        var entity = await _db.LiquidacionNominas.FirstOrDefaultAsync(l =>
            l.EmpleadoId == liquidation.EmpleadoId &&
            l.Anio == liquidation.Anio &&
            l.Mes == (byte)liquidation.Mes, ct);

        if (entity is null)
        {
            entity = new LiquidacionNomina
            {
                EmpleadoId = liquidation.EmpleadoId,
                Anio = liquidation.Anio,
                Mes = (byte)liquidation.Mes
            };
            _db.LiquidacionNominas.Add(entity);
        }

        var r = liquidation.Resultado;
        entity.UsuarioId = liquidation.UsuarioId;
        entity.HorasOrdinarias = r.HorasOrdinarias;
        entity.HorasExtra = r.HorasExtra;
        entity.PagoBruto = r.PagoBruto;
        entity.Salud = r.Deducciones.Salud;
        entity.Pension = r.Deducciones.Pension;
        entity.Solidaridad = r.Deducciones.Solidaridad;
        entity.AuxilioTransporte = r.Deducciones.AuxilioTransporte;
        entity.Neto = r.Neto;
        entity.FechaCalculo = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}