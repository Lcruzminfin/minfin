using SolicitudSystem.Domain;

namespace SolicitudSystem.Application;

public sealed class SolicitudService : ISolicitudService
{
    private readonly ISolicitudRepository _repo;
    private readonly IEncryptionService _crypto;
    private readonly IEventPublisher _events;

    public SolicitudService(ISolicitudRepository repo, IEncryptionService crypto, IEventPublisher events) => (_repo, _crypto, _events) = (repo, crypto, events);

    public async Task<List<SolicitudDto>> GetAllAsync(CancellationToken ct) =>
        (await _repo.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<SolicitudDto> CreateAsync(CrearSolicitudRequest request, string user, CancellationToken ct)
    {
        Validate(request.Titulo, request.Descripcion);
        ValidatePayment(request);
        var entity = new Solicitud
        {
            Id = Guid.NewGuid(), Titulo = request.Titulo.Trim(), Descripcion = request.Descripcion.Trim(),
            DatosSensiblesCifrados = _crypto.Encrypt(request.DatosSensibles ?? ""),
            NombreBeneficiario = request.NombreBeneficiario.Trim(), Nit = request.Nit.Trim(),
            CuentaOrigen = request.CuentaOrigen.Trim(), CuentaDestino = request.CuentaDestino.Trim(),
            FuenteFinanciamiento = request.FuenteFinanciamiento.Trim(), EstructuraPresupuestaria = request.EstructuraPresupuestaria.Trim(),
            Monto = request.Monto, Moneda = request.Moneda.Trim(), CreadoPor = user
        };
        await _repo.AddAsync(entity, ct); await _repo.SaveAsync(ct);
        await _events.PublishAsync(new(Guid.NewGuid().ToString(), "solicitud.creada", entity.Id, DateTime.UtcNow), ct);
        return ToDto(entity);
    }

    public async Task<SolicitudDto> UpdateAsync(Guid id, ActualizarSolicitudRequest request, string user, CancellationToken ct)
    {
        Validate(request.Titulo, request.Descripcion);
        ValidatePayment(request);
        var entity = await _repo.GetAsync(id, ct) ?? throw new KeyNotFoundException("Solicitud no encontrada.");
        if (entity.Estado is EstadoSolicitud.Resuelta or EstadoSolicitud.Cancelada) throw new InvalidOperationException("No se puede actualizar una solicitud finalizada.");
        entity.Titulo = request.Titulo.Trim(); entity.Descripcion = request.Descripcion.Trim();
        entity.DatosSensiblesCifrados = _crypto.Encrypt(request.DatosSensibles ?? "");
        entity.NombreBeneficiario = request.NombreBeneficiario.Trim(); entity.Nit = request.Nit.Trim();
        entity.CuentaOrigen = request.CuentaOrigen.Trim(); entity.CuentaDestino = request.CuentaDestino.Trim();
        entity.FuenteFinanciamiento = request.FuenteFinanciamiento.Trim(); entity.EstructuraPresupuestaria = request.EstructuraPresupuestaria.Trim();
        entity.Monto = request.Monto; entity.Moneda = request.Moneda.Trim(); entity.FechaActualizacionUtc = DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        await _events.PublishAsync(new(Guid.NewGuid().ToString(), "solicitud.actualizada", entity.Id, DateTime.UtcNow), ct);
        return ToDto(entity);
    }

    public async Task<SolicitudDto> ChangeStatusAsync(Guid id, CambiarEstadoRequest request, CancellationToken ct)
    {
        var entity = await _repo.GetAsync(id, ct) ?? throw new KeyNotFoundException("Solicitud no encontrada.");
        if (!IsValidTransition(entity.Estado, request.Estado)) throw new InvalidOperationException($"Transición inválida: {entity.Estado} -> {request.Estado}.");
        entity.Estado = request.Estado; entity.FechaActualizacionUtc = DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        await _events.PublishAsync(new(Guid.NewGuid().ToString(), "solicitud.actualizada", entity.Id, DateTime.UtcNow), ct);
        return ToDto(entity);
    }

    private static void Validate(string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 5 || title.Trim().Length > 100) throw new ArgumentException("El título debe tener entre 5 y 100 caracteres.");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 10 || description.Trim().Length > 1000) throw new ArgumentException("La descripción debe tener entre 10 y 1000 caracteres.");
    }

    private static void ValidatePayment(CrearSolicitudRequest request) => ValidatePayment(request.NombreBeneficiario, request.Nit, request.CuentaOrigen, request.CuentaDestino, request.FuenteFinanciamiento, request.EstructuraPresupuestaria, request.Monto, request.Moneda);
    private static void ValidatePayment(ActualizarSolicitudRequest request) => ValidatePayment(request.NombreBeneficiario, request.Nit, request.CuentaOrigen, request.CuentaDestino, request.FuenteFinanciamiento, request.EstructuraPresupuestaria, request.Monto, request.Moneda);
    private static void ValidatePayment(string beneficiary, string nit, string origin, string destination, string source, string structure, decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(beneficiary)) throw new ArgumentException("El nombre del beneficiario es obligatorio.");
        if (string.IsNullOrWhiteSpace(nit)) throw new ArgumentException("El NIT es obligatorio.");
        if (string.IsNullOrWhiteSpace(origin)) throw new ArgumentException("La cuenta origen es obligatoria.");
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("La cuenta destino es obligatoria.");
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("La fuente de financiamiento es obligatoria.");
        if (string.IsNullOrWhiteSpace(structure)) throw new ArgumentException("La estructura presupuestaria es obligatoria.");
        if (amount <= 0) throw new ArgumentException("El monto debe ser mayor que cero.");
        if (currency is not ("Q" or "$")) throw new ArgumentException("La moneda debe ser Q o $.");
    }

    private static bool IsValidTransition(EstadoSolicitud from, EstadoSolicitud to) =>
        from switch { EstadoSolicitud.Pendiente => to is EstadoSolicitud.EnProceso or EstadoSolicitud.Cancelada,
                       EstadoSolicitud.EnProceso => to is EstadoSolicitud.Resuelta or EstadoSolicitud.Cancelada,
                       _ => false };

    private SolicitudDto ToDto(Solicitud x) => new(
        x.Id, x.Titulo, x.Descripcion, _crypto.Decrypt(x.DatosSensiblesCifrados), x.NombreBeneficiario, x.Nit,
        x.CuentaOrigen, x.CuentaDestino, x.FuenteFinanciamiento, x.EstructuraPresupuestaria, x.Monto, x.Moneda,
        x.Estado, x.FechaCreacionUtc, x.FechaActualizacionUtc, x.CreadoPor,
        x.Documentos.Select(d => new DocumentoDto(d.Id, d.NombreArchivo, d.ContentType, d.Tamano, d.FechaCreacionUtc)).ToList());
}
