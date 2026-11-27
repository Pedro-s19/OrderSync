using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Nomina;
using OrderSync.Domain.Nomina;

namespace OrderSync.Infrastructure.Persistence;

/// <summary>Implementa el puerto leyendo rrhh.ParametroNomina.</summary>
public sealed class PayrollParametersProvider : IPayrollParametersProvider
{
    private readonly OrderSyncDbContext _db;

    public PayrollParametersProvider(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public PayrollParameters Get(int year)
    {
        var values = _db.ParametroNominas
            .AsNoTracking()
            .Where(p => p.Vigencia == year)
            .ToDictionary(p => p.Clave, p => p.Valor, StringComparer.OrdinalIgnoreCase);

        return PayrollParametersFactory.FromValues(values, year);
    }
}