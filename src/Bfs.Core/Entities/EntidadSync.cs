namespace Bfs.Core.Entities;

/// <summary>
/// Columnas comunes para sincronizar nodo ↔ nube. Las completa el interceptor de guardado:
/// no se asignan a mano salvo <see cref="Id"/>, que puede venir de otro nodo.
/// </summary>
public abstract class EntidadSync
{
    public Guid Id { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ModificadoEn { get; set; }
    public long Version { get; set; }
    public bool Eliminado { get; set; }
    public Guid? OrigenNodoId { get; set; }
}
