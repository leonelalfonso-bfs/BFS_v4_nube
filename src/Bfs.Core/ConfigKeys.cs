namespace Bfs.Core;

public static class ConfigKeys
{
    public const string Aduana = "aduana";
    public const string Contenedores = "contenedores";
    public const string TaraConocida = "tara_conocida";
    public const string TaraPallets = "tara_pallets";
    public const string Termica = "termica";
    public const string TermicaImpresora = "termica_impresora";
    public const string TermicaAncho = "termica_ancho";
    public const string IngresoManual = "ingreso_manual";

    /// <summary>Número de este puesto (1-99). Va adelante del número de ticket.</summary>
    public const string Puesto = "puesto";

    /// <summary>Solo se captura peso de la balanza cuando está estable.</summary>
    public const string ExigirEstable = "exigir_estable";

    /// <summary>Carpeta (otro disco o carpeta de red) donde se mantiene una copia de la base.</summary>
    public const string EspejoCarpeta = "espejo_carpeta";
    public const string EspejoUltimo = "espejo_ultimo";
    public const string EspejoError = "espejo_error";

    /// <summary>Hash de la clave que pide el ingreso manual de peso (la 3.x la guardaba en texto plano).</summary>
    public const string ClaveManualHash = "clave_manual_hash";

    /// <summary>"true": tickets solo con número ("78271") en vez de "01-000001". Pensado para un solo puesto.</summary>
    public const string TicketSoloNumero = "ticket_solo_numero";

    /// <summary>Hash de la clave que pide la corrección de datos de una pesada.</summary>
    public const string ClaveCorreccionHash = "clave_correccion_hash";

    /// <summary>Hash de la clave que pide borrar un registro de un catálogo. Vacía = sin clave.</summary>
    public const string ClaveBorradoHash = "clave_borrado_hash";

    /// <summary>Escenario de aduana fijo para todas las operaciones; vacío = lo elige el operador.</summary>
    public const string AduanaEscenario = "aduana_escenario";

    public const string AduanaCodigo = "aduana_codigo";
    public const string AduanaLot = "aduana_lot";
    public const string AduanaCertificado1 = "aduana_certificado_1";
    public const string AduanaCertificado1Vence = "aduana_certificado_1_vence";
    public const string AduanaCertificado2 = "aduana_certificado_2";
    public const string AduanaCertificado2Vence = "aduana_certificado_2_vence";
    public const string WormsEnabled = "worms_enabled";
    public const string WormsEndpoint = "worms_endpoint";
    public const string WormsApiKey = "worms_api_key";
    public const string WormsBalanza = "worms_balanza";
    public const string WormsTipoDefault = "worms_tipo_default";

    /// <summary>Ids de las empresas que envían a WORMS, separados por coma. Vacío = todas.</summary>
    public const string WormsEmpresas = "worms_empresas";

    /// <summary>"true": al corregir los datos de una pesada se vuelven a mandar sus eventos (WORMS guarda la última versión).</summary>
    public const string WormsReenviarCorrecciones = "worms_reenviar_correcciones";
    public const string ClienteNombre = "cliente_nombre";
    public const string ScaleMode = "scale_mode";
    public const string ScalePort = "scale_port";
    public const string ScaleBaud = "scale_baud";
    public const string ScaleDataBits = "scale_databits";
    public const string ScaleStopBits = "scale_stopbits";
    public const string ScaleParity = "scale_parity";
    public const string ScaleProtocol = "scale_protocol";
    public const string ScaleExtract = "scale_extract";
    public const string FieldsJson = "fields_json";
    public const string TicketTitulo = "ticket_titulo";
    public const string TicketPie = "ticket_pie";
    public const string LogoFile = "logo_file";

    // Edición Inagro.

    /// <summary>Planta de esta instalación ("1: Alvear" / "2: Campana"). Va en cada pesada y a Centum.</summary>
    public const string InagroPlanta = "inagro_planta";

    /// <summary>Qué se imprime al cerrar una pesada: "preguntar", "ticket" o "remito" (remito preimpreso).</summary>
    public const string InagroImpresion = "inagro_impresion";

    /// <summary>Corrimiento del remito preimpreso en mm (positivo = a la derecha / hacia abajo), por si la impresora lo tira corrido.</summary>
    public const string InagroRemitoAjusteX = "inagro_remito_ajuste_x";
    public const string InagroRemitoAjusteY = "inagro_remito_ajuste_y";

    public const string CentumEnabled = "centum_enabled";

    /// <summary>Webhook que recibe las pesadas para Centum (en la 3.x, automate.gauchocode.com).</summary>
    public const string CentumEndpoint = "centum_endpoint";
}
