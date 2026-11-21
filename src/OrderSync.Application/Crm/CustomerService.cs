using OrderSync.Application.Common;

namespace OrderSync.Application.Crm;

/// <summary>Perfil del cliente (FR-18), historial y ajuste manual de puntos de fidelidad (FR-16).</summary>
public sealed class CustomerService
{
    private const int HistoryTop = 20;

    private readonly ICustomerStore _store;
    private readonly ITransactionRunner _tx;

    public CustomerService(ICustomerStore store, ITransactionRunner tx)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tx = tx ?? throw new ArgumentNullException(nameof(tx));
    }

    public Task<IReadOnlyList<CustomerDto>> ListAsync(CustomerFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        InputRules.Paging(filter.Pagina, filter.TamanoPagina);

        var texto = string.IsNullOrWhiteSpace(filter.Texto) ? null : filter.Texto.Trim();
        return _store.ListAsync(filter with { Texto = texto }, ct);
    }

    public async Task<CustomerDto> GetAsync(int clienteId, CancellationToken ct = default)
        => await _store.GetAsync(clienteId, ct)
           ?? throw new ResourceNotFoundException($"No existe el cliente {clienteId}.");

    public async Task<CustomerHistory> GetHistoryAsync(int clienteId, CancellationToken ct = default)
    {
        var cliente = await GetAsync(clienteId, ct);
        var pedidos = await _store.GetOrdersAsync(clienteId, HistoryTop, ct);
        var puntos = await _store.GetPointsAsync(clienteId, HistoryTop, ct);
        return new CustomerHistory(cliente, pedidos, puntos);
    }

    public Task<CustomerDto> CreateAsync(CustomerRequest request, CancellationToken ct = default)
        => _store.CreateAsync(Validate(request), ct);

    public async Task<CustomerDto> UpdateAsync(int clienteId, CustomerRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        _ = await GetAsync(clienteId, ct);

        await _store.UpdateAsync(clienteId, clean, ct);
        return await GetAsync(clienteId, ct);
    }

    public async Task<CustomerDto> SetActiveAsync(int clienteId, bool activo, CancellationToken ct = default)
    {
        _ = await GetAsync(clienteId, ct);
        await _store.SetActiveAsync(clienteId, activo, ct);
        return await GetAsync(clienteId, ct);
    }

    /// <summary>Suma o resta puntos a mano. El saldo nunca puede quedar negativo.</summary>
    public Task<CustomerDto> AdjustPointsAsync(int clienteId, PointsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Puntos == 0)
            throw new RequestValidationException("Los puntos deben ser distintos de cero.");

        return _tx.ExecuteAsync(async token =>
        {
            var cliente = await _store.LockAsync(clienteId, token)
                          ?? throw new ResourceNotFoundException($"No existe el cliente {clienteId}.");
            if (!cliente.Activo)
                throw new BusinessRuleException($"El cliente {cliente.Nombre} está desactivado.");
            if (cliente.PuntosFidelidad + request.Puntos < 0)
                throw new BusinessRuleException(
                    $"Saldo insuficiente: el cliente tiene {cliente.PuntosFidelidad} puntos y se intentan restar {-request.Puntos}.");

            await _store.AddPointsAsync(clienteId, null, request.Puntos, token);
            return await GetAsync(clienteId, token);
        }, ct);
    }

    private static CustomerRequest Validate(CustomerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CustomerRequest(
            InputRules.Required(request.Nombre, 100, "El nombre"),
            InputRules.Optional(request.Telefono, 20, "El teléfono"),
            InputRules.OptionalEmail(request.Email),
            InputRules.Optional(request.Direccion, 200, "La dirección"));
    }
}
