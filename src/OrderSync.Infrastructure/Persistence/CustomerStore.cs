using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Crm;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class CustomerStore : ICustomerStore
{
    private readonly OrderSyncDbContext _db;

    public CustomerStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(CustomerFilter filter, CancellationToken ct = default)
    {
        var query = _db.Clientes.AsNoTracking().Where(c => filter.IncluirInactivos || c.Activo);

        if (filter.Texto is { } texto)
            query = query.Where(c => c.Nombre.Contains(texto)
                                     || (c.Telefono != null && c.Telefono.Contains(texto))
                                     || (c.Email != null && c.Email.Contains(texto)));

        return await query
            .OrderBy(c => c.Nombre)
            .Skip((filter.Pagina - 1) * filter.TamanoPagina)
            .Take(filter.TamanoPagina)
            .Select(c => new CustomerDto(c.ClienteId, c.Nombre, c.Telefono, c.Email, c.Direccion,
                c.PuntosFidelidad, c.FechaRegistro, c.Activo))
            .ToListAsync(ct);
    }

    public Task<CustomerDto?> GetAsync(int clienteId, CancellationToken ct = default)
        => _db.Clientes.AsNoTracking()
            .Where(c => c.ClienteId == clienteId)
            .Select(c => new CustomerDto(c.ClienteId, c.Nombre, c.Telefono, c.Email, c.Direccion,
                c.PuntosFidelidad, c.FechaRegistro, c.Activo))
            .FirstOrDefaultAsync(ct);

    public async Task<CustomerDto> CreateAsync(CustomerRequest customer, CancellationToken ct = default)
    {
        var entity = new Cliente
        {
            Nombre = customer.Nombre, Telefono = customer.Telefono, Email = customer.Email,
            Direccion = customer.Direccion, PuntosFidelidad = 0, FechaRegistro = DateTime.UtcNow, Activo = true
        };
        _db.Clientes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new CustomerDto(entity.ClienteId, entity.Nombre, entity.Telefono, entity.Email, entity.Direccion,
            entity.PuntosFidelidad, entity.FechaRegistro, entity.Activo);
    }

    public async Task UpdateAsync(int clienteId, CustomerRequest customer, CancellationToken ct = default)
    {
        var entity = await _db.Clientes.FirstAsync(c => c.ClienteId == clienteId, ct);
        entity.Nombre = customer.Nombre;
        entity.Telefono = customer.Telefono;
        entity.Email = customer.Email;
        entity.Direccion = customer.Direccion;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int clienteId, bool activo, CancellationToken ct = default)
    {
        var entity = await _db.Clientes.FirstAsync(c => c.ClienteId == clienteId, ct);
        entity.Activo = activo;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CustomerDto?> LockAsync(int clienteId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlAsync(
            $"SELECT ClienteId FROM crm.Cliente WITH (UPDLOCK, ROWLOCK) WHERE ClienteId = {clienteId}", ct);
        return await GetAsync(clienteId, ct);
    }

    public async Task AddPointsAsync(int clienteId, int? pedidoId, int puntos, CancellationToken ct = default)
    {
        var entity = await _db.Clientes.FirstAsync(c => c.ClienteId == clienteId, ct);
        entity.PuntosFidelidad += puntos;

        _db.MovimientoPuntos.Add(new MovimientoPunto
        {
            ClienteId = clienteId, PedidoId = pedidoId, Puntos = puntos, Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerOrderDto>> GetOrdersAsync(int clienteId, int top, CancellationToken ct = default)
        => await _db.Pedidos.AsNoTracking()
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.FechaCreacion)
            .Take(top)
            .Select(p => new CustomerOrderDto(p.PedidoId, p.Canal, p.Estado, p.Total, p.FechaCreacion))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PointsMovementDto>> GetPointsAsync(int clienteId, int top, CancellationToken ct = default)
        => await _db.MovimientoPuntos.AsNoTracking()
            .Where(m => m.ClienteId == clienteId)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.MovimientoId)
            .Take(top)
            .Select(m => new PointsMovementDto(m.MovimientoId, m.PedidoId, m.Puntos, m.Fecha))
            .ToListAsync(ct);
}
