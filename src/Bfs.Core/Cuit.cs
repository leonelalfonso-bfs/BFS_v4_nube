namespace Bfs.Core;

public static class Cuit
{
    private static readonly int[] Pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    /// <summary>
    /// Acepta el CUIT con o sin guiones y lo devuelve como <c>XX-XXXXXXXX-X</c>.
    /// Vacío es válido (el CUIT es opcional); un CUIT con dígito verificador incorrecto no.
    /// </summary>
    public static (string? Valor, string? Error) Normalizar(string? cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit)) return (null, null);
        var digitos = new string(cuit.Where(char.IsDigit).ToArray());
        if (digitos.Length != 11 || cuit.Any(c => !char.IsDigit(c) && c is not ('-' or ' ' or '.')))
        {
            return (null, "El CUIT debe tener 11 dígitos");
        }
        if (!DigitoVerificadorOk(digitos))
        {
            return (null, "El CUIT no es válido (dígito verificador)");
        }
        return ($"{digitos[..2]}-{digitos[2..10]}-{digitos[10]}", null);
    }

    private static bool DigitoVerificadorOk(string d)
    {
        var suma = 0;
        for (var i = 0; i < 10; i++) suma += (d[i] - '0') * Pesos[i];
        var resto = 11 - suma % 11;
        var esperado = resto switch { 11 => 0, 10 => 9, _ => resto };
        return d[10] - '0' == esperado;
    }
}
