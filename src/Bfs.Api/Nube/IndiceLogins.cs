using Bfs.Infrastructure.Central;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Nube;

/// <summary>
/// En la nube el email es único entre todos los clientes (el login lo usa para saber a qué cliente va).
/// En el nodo hay una sola base y alcanza con el índice único de la tabla de usuarios.
/// </summary>
public interface IIndiceLogins
{
    Task<bool> DisponibleAsync(string email);
    Task RegistrarAsync(string email);
}

public sealed class IndiceLoginsNodo : IIndiceLogins
{
    public Task<bool> DisponibleAsync(string email) => Task.FromResult(true);
    public Task RegistrarAsync(string email) => Task.CompletedTask;
}

public sealed class IndiceLoginsNube(BasesNube bases, TenantActual tenant) : IIndiceLogins
{
    public async Task<bool> DisponibleAsync(string email)
    {
        await using var central = bases.Central();
        return !await central.Logins.AnyAsync(l => l.Email == email) && !await central.Superadmins.AnyAsync(s => s.Email == email);
    }

    public async Task RegistrarAsync(string email)
    {
        var id = tenant.Id ?? throw new SinTenantException();
        await using var central = bases.Central();
        central.Logins.Add(new LoginTenant { Email = email, TenantId = id });
        await central.SaveChangesAsync();
    }
}
