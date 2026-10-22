using Bfs.Core;
using Bfs.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bfs.Infrastructure.Data;

/// <summary>
/// Completa las columnas de sincronización al guardar y convierte los borrados en borrado lógico,
/// para que la eliminación también pueda viajar entre nodo y nube.
/// </summary>
public class SyncStampInterceptor : SaveChangesInterceptor
{
    private readonly NodoInfo _nodo;
    private readonly TimeProvider _clock;

    public SyncStampInterceptor(NodoInfo nodo, TimeProvider? clock = null)
    {
        _nodo = nodo;
        _clock = clock ?? TimeProvider.System;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context == null) return;
        var now = _clock.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<EntidadSync>())
        {
            var e = entry.Entity;
            switch (entry.State)
            {
                case EntityState.Added:
                    if (e.Id == Guid.Empty) e.Id = Guid.NewGuid();
                    if (e.CreadoEn == default) e.CreadoEn = now;
                    e.ModificadoEn = now;
                    e.Version = 1;
                    e.OrigenNodoId ??= _nodo.Id;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    e.Eliminado = true;
                    e.ModificadoEn = now;
                    e.Version++;
                    break;

                case EntityState.Modified:
                    e.ModificadoEn = now;
                    e.Version++;
                    break;
            }
        }
    }
}
