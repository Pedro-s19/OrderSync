namespace OrderSync.Application.Crm;

// ---- Clientes ----
public sealed record CustomerDto(
    int ClienteId, string Nombre, string? Telefono, string? Email, string? Direccion,
    int PuntosFidelidad, DateTime FechaRegistro, bool Activo);

public sealed record CustomerRequest(string Nombre, string? Telefono, string? Email, string? Direccion);

public sealed record CustomerFilter(string? Texto, bool IncluirInactivos = false, int Pagina = 1, int TamanoPagina = 50);

public sealed record PointsRequest(int Puntos);

public sealed record PointsMovementDto(long MovimientoId, int? PedidoId, int Puntos, DateTime Fecha);

public sealed record CustomerOrderDto(int PedidoId, string Canal, string Estado, decimal Total, DateTime FechaCreacion);

public sealed record CustomerHistory(
    CustomerDto Cliente, IReadOnlyList<CustomerOrderDto> Pedidos, IReadOnlyList<PointsMovementDto> Puntos);

// ---- Promociones ----
public sealed record PromotionDto(
    int PromocionId, string Nombre, decimal PorcentajeDto, int? PuntosMinimos,
    DateOnly VigenteDesde, DateOnly? VigenteHasta, bool Activa);

public sealed record PromotionRequest(
    string Nombre, decimal PorcentajeDto, int? PuntosMinimos, DateOnly VigenteDesde, DateOnly? VigenteHasta);

// ---- Puertos ----
public interface ICustomerStore
{
    Task<IReadOnlyList<CustomerDto>> ListAsync(CustomerFilter filter, CancellationToken ct = default);
    Task<CustomerDto?> GetAsync(int clienteId, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CustomerRequest customer, CancellationToken ct = default);
    Task UpdateAsync(int clienteId, CustomerRequest customer, CancellationToken ct = default);
    Task SetActiveAsync(int clienteId, bool activo, CancellationToken ct = default);

    /// <summary>Bloquea al cliente (dentro de una transacción) y devuelve su ficha con el saldo de puntos.</summary>
    Task<CustomerDto?> LockAsync(int clienteId, CancellationToken ct = default);

    /// <summary>Suma (o resta, si es negativo) puntos: actualiza el saldo y deja un movimiento.</summary>
    Task AddPointsAsync(int clienteId, int? pedidoId, int puntos, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerOrderDto>> GetOrdersAsync(int clienteId, int top, CancellationToken ct = default);
    Task<IReadOnlyList<PointsMovementDto>> GetPointsAsync(int clienteId, int top, CancellationToken ct = default);
}

public interface IPromotionStore
{
    Task<IReadOnlyList<PromotionDto>> ListAsync(bool incluirInactivas, CancellationToken ct = default);
    Task<PromotionDto?> GetAsync(int promocionId, CancellationToken ct = default);
    Task<PromotionDto> CreateAsync(PromotionRequest promotion, CancellationToken ct = default);
    Task UpdateAsync(int promocionId, PromotionRequest promotion, CancellationToken ct = default);
    Task SetActiveAsync(int promocionId, bool activa, CancellationToken ct = default);

    Task<IReadOnlyList<PromotionDto>> GetAssignedAsync(int clienteId, CancellationToken ct = default);
    Task<bool> IsAssignedAsync(int clienteId, int promocionId, CancellationToken ct = default);
    Task AssignAsync(int clienteId, int promocionId, CancellationToken ct = default);
    Task<bool> UnassignAsync(int clienteId, int promocionId, CancellationToken ct = default);
}
