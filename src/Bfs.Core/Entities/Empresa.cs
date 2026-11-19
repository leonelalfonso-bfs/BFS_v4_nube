namespace Bfs.Core.Entities;

public class Empresa : EntidadSync
{
    /// <summary>Razón social. Sale en el ticket.</summary>
    public string Nombre { get; set; } = "";
    public string? Cuit { get; set; }
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    public string? Provincia { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool Activa { get; set; } = true;
    public int ProximoTicket { get; set; } = 1;

    /// <summary>
    /// Edición Inagro: código de sociedad de Centum (tipo_sociedad_id: 1 Inagro S.A., 2 Inagro S.R.L., 3 Otros).
    /// Vacío o "4" (No Vincula Centum) = las pesadas de esta empresa no se envían.
    /// </summary>
    public string? CodigoSociedad { get; set; }
}
