using SolicitudSystem.Domain;

namespace SolicitudSystem.Application;

public interface ISolicitudRepository
{
    Task<List<Solicitud>> GetAllAsync(CancellationToken ct);
    Task<Solicitud?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(Solicitud entity, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public interface IEncryptionService
{
    string Encrypt(string value);
    string Decrypt(string value);
}

public interface IJwtService { string CreateToken(string username); }
public interface IEventPublisher { Task PublishAsync(SolicitudEvent evt, CancellationToken ct); }
public interface IMessageIdempotency { Task<bool> TryRegisterAsync(string messageId, CancellationToken ct); }
public interface IPasswordService { string Hash(string password); bool Verify(string password, string hash); }

public interface ISolicitudService
{
    Task<List<SolicitudDto>> GetAllAsync(CancellationToken ct);
    Task<SolicitudDto> CreateAsync(CrearSolicitudRequest request, string user, CancellationToken ct);
    Task<SolicitudDto> UpdateAsync(Guid id, ActualizarSolicitudRequest request, string user, CancellationToken ct);
    Task<SolicitudDto> ChangeStatusAsync(Guid id, CambiarEstadoRequest request, CancellationToken ct);
}

public interface IDocumentoService
{
    Task<List<DocumentoDto>> GetAllAsync(Guid solicitudId, CancellationToken ct);
    Task<DocumentoDto> UploadAsync(Guid solicitudId, string fileName, string contentType, Stream content, CancellationToken ct);
    Task<(string FileName, string ContentType, byte[] Content)?> DownloadAsync(Guid solicitudId, Guid documentId, CancellationToken ct);
    Task DeleteAsync(Guid solicitudId, Guid documentId, CancellationToken ct);
}

public interface IAuthService { Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct); }
