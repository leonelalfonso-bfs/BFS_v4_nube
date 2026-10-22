using System.Net.NetworkInformation;

namespace Bfs.Core.Licencias;

/// <summary>
/// Id de la PC a partir de la MAC, con el mismo formato que <c>DatabaseManager.getMachineId()</c> de la 3.x.
/// Se aceptan todas las placas físicas: si la PC tiene Ethernet y Wi-Fi, la licencia vale con cualquiera,
/// así no se invalida porque cambió el orden en que Windows las lista.
/// </summary>
public static class MaquinaId
{
    private static readonly string[] Virtuales = ["virtual", "hyper-v", "vmware", "vpn", "tap-", "wan miniport", "bluetooth", "loopback", "pseudo"];

    public static IReadOnlyList<string> Actuales()
    {
        try
        {
            var todas = NetworkInterface.GetAllNetworkInterfaces().Where(TieneMac).ToList();
            // En una máquina virtual la única placa puede figurar como virtual: entonces se usan todas.
            var fisicas = todas.Where(EsFisica).ToList();
            return (fisicas.Count > 0 ? fisicas : todas)
                .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
                .ThenBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => Formatear(n.GetPhysicalAddress().GetAddressBytes()))
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    /// <summary>El id que se muestra para pedir la licencia.</summary>
    public static string Principal() => Actuales().FirstOrDefault() ?? "ID_NO_DISPONIBLE";

    public static string Formatear(byte[] mac) => "MAC-" + Convert.ToHexString(mac);

    private static bool TieneMac(NetworkInterface n)
    {
        if (n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) return false;
        var mac = n.GetPhysicalAddress().GetAddressBytes();
        return mac.Length == 6 && !mac.All(b => b == 0);
    }

    private static bool EsFisica(NetworkInterface n)
    {
        var desc = n.Description.ToLowerInvariant();
        return !Virtuales.Any(desc.Contains);
    }
}
