namespace SolicitudSystem.Domain;

public enum EstadoSolicitud { Pendiente, EnProceso, Resuelta, Cancelada }

public sealed class Solicitud
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string DatosSensiblesCifrados { get; set; } = "";
    public string NombreBeneficiario { get; set; } = "";
    public string Nit { get; set; } = "";
    public string CuentaOrigen { get; set; } = "";
    public string CuentaDestino { get; set; } = "";
    public string FuenteFinanciamiento { get; set; } = "";
    public string EstructuraPresupuestaria { get; set; } = "";
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "Q";
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacionUtc { get; set; } = DateTime.UtcNow;
    public string CreadoPor { get; set; } = "";
    public ICollection<DocumentoSolicitud> Documentos { get; set; } = new List<DocumentoSolicitud>();
}

public sealed class DocumentoSolicitud
{
    public Guid Id { get; set; }
    public Guid SolicitudId { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Tamano { get; set; }
    public byte[] Contenido { get; set; } = Array.Empty<byte>();
    public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;
    public Solicitud Solicitud { get; set; } = null!;
}

public sealed class Usuario
{
    public int Id { get; set; }
    public string UsuarioLogin { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public sealed class MensajeProcesado
{
    public long Id { get; set; }
    public string MessageId { get; set; } = "";
    public DateTime ProcesadoUtc { get; set; } = DateTime.UtcNow;
}
