namespace OrderSync.Application.Common;

/// <summary>Ejecuta una operación completa en una sola transacción (todo o nada).</summary>
public interface ITransactionRunner
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
}