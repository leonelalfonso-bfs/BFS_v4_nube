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
    private static long _ultimaSecuencia;

    private readonly NodoInfo _nodo;
    private readonly TimeProvider _clock;
    private readonly bool _marcarSecuencia;

    /// <param name="marcarSecuencia">
    /// Solo en la nube: cada cambio guardado lleva un <see cref="EntidadSync.SyncSeq"/> creciente, que es lo que
    /// usan los nodos para pedir "lo que cambió desde la última vez" sin depender del reloj de cada PC.
    /// </param>
    public SyncStampInterceptor(NodoInfo nodo, TimeProvider? clock = null, bool marcarSecuencia = false)
    {
        _nodo = nodo;
        _clock = clock ?? TimeProvider.System;
        _marcarSecuencia = marcarSecuencia;
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
        // Lo que llega de otro lado ya trae sus columnas de sync: se guarda tal cual (si no, volvería a viajar como cambio nuevo).
        var aplicandoSync = context is BfsDbContext { AplicandoSincronizacion: true };

        foreach (var entry in context.ChangeTracker.Entries<EntidadSync>())
        {
            var e = entry.Entity;
            if (_marcarSecuencia && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                e.SyncSeq = ProximaSecuencia(now);
            }
            if (aplicandoSync) continue;

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

    /// <summary>Ticks del reloj del servidor, pero estrictamente creciente aunque dos cambios caigan en el mismo tick.</summary>
    private static long ProximaSecuencia(DateTimeOffset now)
    {
        while (true)
        {
            var anterior = Interlocked.Read(ref _ultimaSecuencia);
            var siguiente = Math.Max(now.UtcTicks, anterior + 1);
            if (Interlocked.CompareExchange(ref _ultimaSecuencia, siguiente, anterior) == anterior) return siguiente;
        }
    }
}
