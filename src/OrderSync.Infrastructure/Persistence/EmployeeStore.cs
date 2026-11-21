using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Rrhh;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class EmployeeStore : IEmployeeStore
{
    private readonly OrderSyncDbContext _db;

    public EmployeeStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => await _db.Empleados.AsNoTracking()
            .Where(e => incluirInactivos || e.Activo)
            .OrderBy(e => e.Nombre)
            .Select(e => new EmployeeDto(e.EmpleadoId, e.Documento, e.Nombre, e.AreaCargo, e.TipoContrato,
                e.TarifaHora, e.SalarioMensual, e.FechaIngreso, e.Activo))
            .ToListAsync(ct);

    public Task<EmployeeDto?> GetAsync(int empleadoId, CancellationToken ct = default)
        => _db.Empleados.AsNoTracking()
            .Where(e => e.EmpleadoId == empleadoId)
            .Select(e => new EmployeeDto(e.EmpleadoId, e.Documento, e.Nombre, e.AreaCargo, e.TipoContrato,
                e.TarifaHora, e.SalarioMensual, e.FechaIngreso, e.Activo))
            .FirstOrDefaultAsync(ct);

    public Task<bool> DocumentExistsAsync(string documento, int? excludeId, CancellationToken ct = default)
        => _db.Empleados.AnyAsync(e => e.Documento == documento && (excludeId == null || e.EmpleadoId != excludeId), ct);

    public async Task<EmployeeDto> CreateAsync(EmployeeData employee, CancellationToken ct = default)
    {
        var entity = new Empleado
        {
            Documento = employee.Documento, Nombre = employee.Nombre, AreaCargo = employee.AreaCargo,
            TipoContrato = employee.TipoContrato, TarifaHora = employee.TarifaHora,
            SalarioMensual = employee.SalarioMensual, FechaIngreso = employee.FechaIngreso, Activo = true
        };
        _db.Empleados.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new EmployeeDto(entity.EmpleadoId, entity.Documento, entity.Nombre, entity.AreaCargo,
            entity.TipoContrato, entity.TarifaHora, entity.SalarioMensual, entity.FechaIngreso, entity.Activo);
    }

    public async Task UpdateAsync(int empleadoId, EmployeeData employee, CancellationToken ct = default)
    {
        var entity = await _db.Empleados.FirstAsync(e => e.EmpleadoId == empleadoId, ct);
        entity.Documento = employee.Documento;
        entity.Nombre = employee.Nombre;
        entity.AreaCargo = employee.AreaCargo;
        entity.TipoContrato = employee.TipoContrato;
        entity.TarifaHora = employee.TarifaHora;
        entity.SalarioMensual = employee.SalarioMensual;
        entity.FechaIngreso = employee.FechaIngreso;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int empleadoId, bool activo, CancellationToken ct = default)
    {
        var entity = await _db.Empleados.FirstAsync(e => e.EmpleadoId == empleadoId, ct);
        entity.Activo = activo;
        await _db.SaveChangesAsync(ct);
    }
}
