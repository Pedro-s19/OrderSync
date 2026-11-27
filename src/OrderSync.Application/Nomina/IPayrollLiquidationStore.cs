namespace OrderSync.Application.Nomina;

/// <summary>Puerto de escritura: guarda (o actualiza) la liquidación del empleado en el periodo.</summary>
public interface IPayrollLiquidationStore
{
    Task SaveAsync(PayrollLiquidation liquidation, CancellationToken ct = default);
}