using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

/// <summary>
/// Puerto para obtener los parámetros de nómina. Infrastructure lo implementa
/// leyendo rrhh.ParametroNomina (inversión de dependencias).
/// </summary>
public interface IPayrollParametersProvider
{
    PayrollParameters Get(int year);
}
