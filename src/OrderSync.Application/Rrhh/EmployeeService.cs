using OrderSync.Application.Common;

namespace OrderSync.Application.Rrhh;

/// <summary>CRUD de empleados. Un empleado inactivo no entra en la nómina (CP-05).</summary>
public sealed class EmployeeService
{
    public const string DefaultContract = "TERMINO_FIJO";
    private const decimal MaxRate = 10_000_000m;

    private readonly IEmployeeStore _store;
    private readonly IEmployeeLinkStore _links;
    private readonly IClock _clock;

    public EmployeeService(IEmployeeStore store, IEmployeeLinkStore links, IClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<IReadOnlyList<EmployeeDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => _store.ListAsync(incluirInactivos, ct);

    public async Task<EmployeeDto> GetAsync(int empleadoId, CancellationToken ct = default)
        => await _store.GetAsync(empleadoId, ct)
           ?? throw new ResourceNotFoundException($"No existe el empleado {empleadoId}.");

    public async Task<EmployeeDto> CreateAsync(EmployeeRequest request, CancellationToken ct = default)
    {
        var data = Validate(request);
        if (await _store.DocumentExistsAsync(data.Documento, null, ct))
            throw new ConflictException($"Ya existe un empleado con el documento {data.Documento}.");
        return await _store.CreateAsync(data, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(int empleadoId, EmployeeRequest request, CancellationToken ct = default)
    {
        var data = Validate(request);
        _ = await GetAsync(empleadoId, ct);
        if (await _store.DocumentExistsAsync(data.Documento, empleadoId, ct))
            throw new ConflictException($"Ya existe un empleado con el documento {data.Documento}.");

        await _store.UpdateAsync(empleadoId, data, ct);
        return await GetAsync(empleadoId, ct);
    }

    public async Task<EmployeeDto> SetActiveAsync(int empleadoId, bool activo, CancellationToken ct = default)
    {
        _ = await GetAsync(empleadoId, ct);
        await _store.SetActiveAsync(empleadoId, activo, ct);
        return await GetAsync(empleadoId, ct);
    }

    /// <summary>Vincula (o desvincula, con null) la cuenta de un usuario a un empleado activo.</summary>
    public async Task LinkUserAsync(int usuarioId, int? empleadoId, CancellationToken ct = default)
    {
        if (!await _links.UserExistsAsync(usuarioId, ct))
            throw new ResourceNotFoundException($"No existe el usuario {usuarioId}.");

        if (empleadoId is int id)
        {
            var employee = await GetAsync(id, ct);
            if (!employee.Activo)
                throw new BusinessRuleException($"El empleado {employee.Nombre} está desactivado.");
        }

        await _links.LinkAsync(usuarioId, empleadoId, ct);
    }

    private EmployeeData Validate(EmployeeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var documento = InputRules.Required(request.Documento, 20, "El documento");
        var nombre = InputRules.Required(request.Nombre, 100, "El nombre");
        var area = InputRules.Required(request.AreaCargo, 60, "El área o cargo");
        var contrato = InputRules.Optional(request.TipoContrato, 30, "El tipo de contrato") ?? DefaultContract;

        if (request.TarifaHora <= 0 || request.TarifaHora > MaxRate)
            throw new RequestValidationException("La tarifa por hora debe ser mayor que cero.");
        if (request.SalarioMensual is <= 0)
            throw new RequestValidationException("El salario mensual, si se indica, debe ser mayor que cero.");

        var hoy = DateOnly.FromDateTime(_clock.LocalNow);
        var ingreso = request.FechaIngreso ?? hoy;
        if (ingreso > hoy)
            throw new RequestValidationException("La fecha de ingreso no puede ser futura.");

        return new EmployeeData(documento, nombre, area, contrato.ToUpperInvariant(),
            request.TarifaHora, request.SalarioMensual, ingreso);
    }
}
