using System.Data;
using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Common;

namespace OrderSync.Infrastructure.Persistence;

public sealed class EfTransactionRunner : ITransactionRunner
{
    private readonly OrderSyncDbContext _db;

    public EfTransactionRunner(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Si la acción lanza una excepción, Dispose sin Commit deshace todo.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var result = await action(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}