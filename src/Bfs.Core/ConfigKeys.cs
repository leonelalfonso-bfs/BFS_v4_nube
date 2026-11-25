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

    /// <summary>"true": en Pesaje se puede pesar en partes (balanza corta: chasis y acoplado por separado).</summary>
    public const string PesajePartes = "pesaje_partes";

    /// <summary>"true": en Pesaje se puede pesar por ejes, con control de los límites de Vialidad.</summary>
    public const string PesajeEjes = "pesaje_ejes";

    /// <summary>"dos": entrada y salida (cada una puede ir por ejes, hay neto). "una": una sola pasada por ejes (control de carga).</summary>
    public const string EjesPasadas = "ejes_pasadas";

    /// <summary>"aviso": el exceso se avisa en pantalla. "ticket": además se marca en el ticket.</summary>
    public const string EjesExceso = "ejes_exceso";

    /// <summary>Límites por tipo de grupo de ejes, JSON {tipo: kg}; lo que falte usa <see cref="TiposEje"/>.</summary>
    public const string EjesLimites = "ejes_limites";

    /// <summary>Peso total máximo del vehículo, en kg.</summary>
    public const string EjesLimiteTotal = "ejes_limite_total";

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

    /// <summary>Automatización (placa Modbus, semáforos, barreras, sensores y cámaras): <see cref="ConfigAutomatizacion"/> en JSON.</summary>
    public const string AutomatizacionJson = "automatizacion_json";

    /// <summary>Marca que ya se ocultó una vez el selector de tipo (ingreso / egreso) en esta base.</summary>
    public const string TipoOcultoAplicado = "campos_tipo_oculto";
}
