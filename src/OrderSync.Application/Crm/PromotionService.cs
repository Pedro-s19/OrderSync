using OrderSync.Application.Common;

namespace OrderSync.Application.Crm;

/// <summary>Promociones y su asignación a clientes (FR-17). "Desactivar" no borra la promoción.</summary>
public sealed class PromotionService
{
    private readonly IPromotionStore _store;
    private readonly ICustomerStore _customers;

    public PromotionService(IPromotionStore store, ICustomerStore customers)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
    }

    public Task<IReadOnlyList<PromotionDto>> ListAsync(bool incluirInactivas, CancellationToken ct = default)
        => _store.ListAsync(incluirInactivas, ct);

    public async Task<PromotionDto> GetAsync(int promocionId, CancellationToken ct = default)
        => await _store.GetAsync(promocionId, ct)
           ?? throw new ResourceNotFoundException($"No existe la promoción {promocionId}.");

    public Task<PromotionDto> CreateAsync(PromotionRequest request, CancellationToken ct = default)
        => _store.CreateAsync(Validate(request), ct);

    public async Task<PromotionDto> UpdateAsync(int promocionId, PromotionRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        _ = await GetAsync(promocionId, ct);

        await _store.UpdateAsync(promocionId, clean, ct);
        return await GetAsync(promocionId, ct);
    }

    public async Task<PromotionDto> SetActiveAsync(int promocionId, bool activa, CancellationToken ct = default)
    {
        _ = await GetAsync(promocionId, ct);
        await _store.SetActiveAsync(promocionId, activa, ct);
        return await GetAsync(promocionId, ct);
    }

    public async Task<IReadOnlyList<PromotionDto>> ListForCustomerAsync(int clienteId, CancellationToken ct = default)
    {
        await EnsureCustomerAsync(clienteId, ct);
        return await _store.GetAssignedAsync(clienteId, ct);
    }

    public async Task<IReadOnlyList<PromotionDto>> AssignAsync(int clienteId, int promocionId, CancellationToken ct = default)
    {
        var cliente = await EnsureCustomerAsync(clienteId, ct);
        if (!cliente.Activo)
            throw new BusinessRuleException($"El cliente {cliente.Nombre} está desactivado.");

        var promocion = await GetAsync(promocionId, ct);
        if (!promocion.Activa)
            throw new BusinessRuleException($"La promoción {promocion.Nombre} está desactivada.");
        if (await _store.IsAssignedAsync(clienteId, promocionId, ct))
            throw new ConflictException($"El cliente ya tiene asignada la promoción {promocion.Nombre}.");

        await _store.AssignAsync(clienteId, promocionId, ct);
        return await _store.GetAssignedAsync(clienteId, ct);
    }

    public async Task UnassignAsync(int clienteId, int promocionId, CancellationToken ct = default)
    {
        await EnsureCustomerAsync(clienteId, ct);
        if (!await _store.UnassignAsync(clienteId, promocionId, ct))
            throw new ResourceNotFoundException($"El cliente {clienteId} no tiene asignada la promoción {promocionId}.");
    }

    private async Task<CustomerDto> EnsureCustomerAsync(int clienteId, CancellationToken ct)
        => await _customers.GetAsync(clienteId, ct)
           ?? throw new ResourceNotFoundException($"No existe el cliente {clienteId}.");

    private static PromotionRequest Validate(PromotionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nombre = InputRules.Required(request.Nombre, 100, "El nombre");
        if (request.PorcentajeDto <= 0 || request.PorcentajeDto > 100)
            throw new RequestValidationException("El porcentaje de descuento debe ser mayor que 0 y hasta 100.");
        if (request.PuntosMinimos is < 0)
            throw new RequestValidationException("Los puntos mínimos no pueden ser negativos.");
        if (request.VigenteHasta is DateOnly hasta && hasta < request.VigenteDesde)
            throw new RequestValidationException("La vigencia 'hasta' no puede ser anterior a 'desde'.");

        return request with { Nombre = nombre };
    }
}
