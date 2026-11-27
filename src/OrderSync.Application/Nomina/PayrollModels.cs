using System.Text.Json.Serialization;
using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

/// <summary>Solicitud de liquidación de un mes. EmpleadoId nulo = todos los empleados.</summary>
public sealed record PayrollRequest(int Anio, int Mes, int? EmpleadoId = null, int? UsuarioId = null);

/// <summary>Un empleado con sus turnos trabajados del periodo, ya listos para el motor.</summary>
public sealed record EmployeePayrollData(
    int EmpleadoId,
    string Nombre,
    decimal TarifaHora,
    bool Activo,
    IReadOnlyList<WorkedShift> Turnos);

/// <summary>Liquidación lista para guardar en rrhh.LiquidacionNomina.</summary>
public sealed record PayrollLiquidation(
    int EmpleadoId,
    int? UsuarioId,
    int Anio,
    int Mes,
    PayrollResult Resultado);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PayrollItemStatus
{
    Liquidado,
    Excluido,
    Rechazado
}

public sealed record EmployeePayrollOutcome(
    int EmpleadoId,
    string Nombre,
    PayrollItemStatus Estado,
    string? Mensaje,
    PayrollResult? Resultado);

public sealed record PayrollRunResult(
    int Anio,
    int Mes,
    IReadOnlyList<EmployeePayrollOutcome> Empleados);