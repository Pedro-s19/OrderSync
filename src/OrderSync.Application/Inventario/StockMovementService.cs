using OrderSync.Application.Common;

namespace OrderSync.Application.Inventario;

/// <summary>
/// Mermas (FR-22), ajustes por conteo físico, atención de alertas y consulta de movimientos.
/// Cada cambio de stock ocurre en una transacción con el ingrediente bloqueado.
/// La alerta se abre si el stock queda por debajo del mínimo y se cierra si lo supera.
/// </summary>
public sealed class StockMovementService
{
    private readonly ITransactionRunner _tx;
    private readonly IIngredientStore _ingredients;
    private readonly IStockMovementStore _movements;
    private readonly IStockAlertStore _alerts;
    private readonly IMovementReader _reader;

    public StockMovementService(
        ITransactionRunner tx, IIngredientStore ingredients, IStockMovementStore movements,
        IStockAlertStore alerts, IMovementReader reader)
    {
        _tx = tx ?? throw new ArgumentNullException(nameof(tx));
        _ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
        _movements = movements ?? throw new ArgumentNullException(nameof(movements));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public Task<StockChangeResult> RegisterWasteAsync(int? usuarioId, WasteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Cantidad <= 0)
            throw new RequestValidationException("La cantidad de la merma debe ser mayor que cero.");
        var motivo = RequireReason(request.Motivo);

        return ApplyAsync(usuarioId, request.IngredienteId, MovementTypes.Merma, motivo, stock =>
        {
            if (request.Cantidad > stock.StockActual)
                throw new BusinessRuleException(
                    $"La merma ({request.Cantidad}) supera el stock disponible de {stock.Nombre} ({stock.StockActual}).");
            return stock.StockActual - request.Cantidad;
        }, ct);
    }

    public Task<StockChangeResult> RegisterAdjustmentAsync(int? usuarioId, AdjustmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.StockReal < 0)
            throw new RequestValidationException("El stock real no puede ser negativo.");
        var motivo = RequireReason(request.Motivo);

        return ApplyAsync(usuarioId, request.IngredienteId, MovementTypes.Ajuste, motivo, stock =>
        {
            if (request.StockReal == stock.StockActual)
                throw new BusinessRuleException(
                    "El stock real coincide con el stock registrado; no hay nada que ajustar.");
            return request.StockReal;
        }, ct);
    }

    public async Task AttendAlertAsync(int alertaId, CancellationToken ct = default)
    {
        var estado = await _alerts.GetStateAsync(alertaId, ct)
                     ?? throw new ResourceNotFoundException($"No existe la alerta {alertaId}.");
        if (estado == AlertStates.Atendida)
            throw new ConflictException($"La alerta {alertaId} ya fue atendida.");

        await _alerts.MarkAttendedAsync(alertaId, ct);
    }

    public Task<IReadOnlyList<MovementDto>> ListMovementsAsync(MovementFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var tipo = string.IsNullOrWhiteSpace(filter.Tipo) ? null : filter.Tipo.Trim().ToUpperInvariant();
        if (tipo is not null && !MovementTypes.All.Contains(tipo))
            throw new RequestValidationException($"Tipo no válido. Use: {string.Join(", ", MovementTypes.All)}.");
        if (filter.Pagina < 1 || filter.TamanoPagina < 1 || filter.TamanoPagina > 200)
            throw new RequestValidationException("La página debe ser 1 o más y el tamaño entre 1 y 200.");
        if (filter.Desde > filter.Hasta)
            throw new RequestValidationException("La fecha 'desde' no puede ser posterior a 'hasta'.");

        return _reader.ListAsync(filter with { Tipo = tipo }, ct);
    }

    private Task<StockChangeResult> ApplyAsync(
        int? usuarioId, int ingredienteId, string tipo, string motivo,
        Func<IngredientStock, decimal> computeNewStock, CancellationToken ct)
        => _tx.ExecuteAsync(async token =>
        {
            var stock = await _ingredients.LockAsync(ingredienteId, token)
                        ?? throw new ResourceNotFoundException($"No existe el ingrediente {ingredienteId}.");

            var nuevo = computeNewStock(stock);
            var delta = nuevo - stock.StockActual;
            var bajoMinimo = nuevo < stock.StockMinimo;

            await _movements.ApplyAsync(new StockChange(
                ingredienteId, tipo, delta, nuevo, stock.StockMinimo, bajoMinimo, usuarioId, motivo), token);

            return new StockChangeResult(
                ingredienteId, stock.Nombre, tipo, delta, stock.StockActual, nuevo, stock.StockMinimo, bajoMinimo);
        }, ct);

    private static string RequireReason(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new RequestValidationException("El motivo es obligatorio.");
        var trimmed = motivo.Trim();
        if (trimmed.Length > 200)
            throw new RequestValidationException("El motivo admite hasta 200 caracteres.");
        return trimmed;
    }
}
