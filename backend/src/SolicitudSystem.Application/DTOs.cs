using SolicitudSystem.Domain;

namespace SolicitudSystem.Application;

public record CrearSolicitudRequest(
    string Titulo, string Descripcion, string DatosSensibles,
    string NombreBeneficiario, string Nit, string CuentaOrigen, string CuentaDestino,
    string FuenteFinanciamiento, string EstructuraPresupuestaria, decimal Monto, string Moneda);

public record ActualizarSolicitudRequest(
    string Titulo, string Descripcion, string DatosSensibles,
    string NombreBeneficiario, string Nit, string CuentaOrigen, string CuentaDestino,
    string FuenteFinanciamiento, string EstructuraPresupuestaria, decimal Monto, string Moneda);

public record CambiarEstadoRequest(EstadoSolicitud Estado);

public record DocumentoDto(Guid Id, string NombreArchivo, string ContentType, long Tamano, DateTime FechaCreacionUtc);

public record SolicitudDto(
    Guid Id, string Titulo, string Descripcion, string DatosSensibles,
    string NombreBeneficiario, string Nit, string CuentaOrigen, string CuentaDestino,
    string FuenteFinanciamiento, string EstructuraPresupuestaria, decimal Monto, string Moneda,
    EstadoSolicitud Estado, DateTime FechaCreacionUtc, DateTime FechaActualizacionUtc, string CreadoPor,
    List<DocumentoDto> Documentos);

public record LoginRequest(string Usuario, string Password);
public record LoginResponse(string Token);
public record SolicitudEvent(string MessageId, string Tipo, Guid SolicitudId, DateTime FechaUtc);
