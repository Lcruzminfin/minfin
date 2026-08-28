using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using SolicitudSystem.Application;
using SolicitudSystem.Domain;

namespace SolicitudSystem.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<MensajeProcesado> MensajesProcesados => Set<MensajeProcesado>();
    public DbSet<DocumentoSolicitud> DocumentosSolicitudes => Set<DocumentoSolicitud>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Solicitud>().HasKey(x => x.Id);
        b.Entity<Usuario>().HasIndex(x => x.UsuarioLogin).IsUnique();
        b.Entity<MensajeProcesado>().HasIndex(x => x.MessageId).IsUnique();
        b.Entity<Solicitud>().Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
        b.Entity<Solicitud>().Property(x => x.Monto).HasPrecision(18, 2);
        b.Entity<DocumentoSolicitud>().HasKey(x => x.Id);
        b.Entity<DocumentoSolicitud>().Property(x => x.Contenido).HasColumnType("varbinary(max)");
        b.Entity<DocumentoSolicitud>().HasOne(x => x.Solicitud).WithMany(x => x.Documentos).HasForeignKey(x => x.SolicitudId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SolicitudRepository(AppDbContext db) : ISolicitudRepository
{
    public Task<List<Solicitud>> GetAllAsync(CancellationToken ct) => db.Solicitudes.AsNoTracking().Include(x => x.Documentos).OrderByDescending(x => x.FechaCreacionUtc).ToListAsync(ct);
    public Task<Solicitud?> GetAsync(Guid id, CancellationToken ct) => db.Solicitudes.Include(x => x.Documentos).FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddAsync(Solicitud entity, CancellationToken ct) => await db.Solicitudes.AddAsync(entity, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class EncryptionService(IConfiguration config) : IEncryptionService
{
    private readonly byte[] _key = Convert.FromBase64String(config["Security:EncryptionKey"] ?? throw new InvalidOperationException("Falta Security:EncryptionKey."));
    public string Encrypt(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var plain = Encoding.UTF8.GetBytes(value); var cipher = new byte[plain.Length];
        using var aes = new AesGcm(_key, 16); aes.Encrypt(nonce, plain, cipher, tag);
        return $"{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(cipher)}";
    }
    public string Decrypt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var p = value.Split('.'); if (p.Length != 3) return "";
        var nonce = Convert.FromBase64String(p[0]); var tag = Convert.FromBase64String(p[1]); var cipher = Convert.FromBase64String(p[2]); var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16); aes.Decrypt(nonce, cipher, tag, plain); return Encoding.UTF8.GetString(plain);
    }
}

public sealed class PasswordService : IPasswordService
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

public sealed class JwtService(IConfiguration config) : IJwtService
{
    public string CreateToken(string username)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key.");
        var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username) };
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: creds);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class AuthService(AppDbContext db, IPasswordService passwords, IJwtService jwt) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Usuarios.SingleOrDefaultAsync(x => x.UsuarioLogin == request.Usuario, ct);
        return user is not null && passwords.Verify(request.Password, user.PasswordHash) ? new LoginResponse(jwt.CreateToken(user.UsuarioLogin)) : null;
    }
}

public sealed class RabbitMqEventPublisher(IConfiguration config) : IEventPublisher
{
    private readonly string _host = config["RabbitMQ:Host"] ?? "localhost";
    private readonly string _user = config["RabbitMQ:User"] ?? "guest";
    private readonly string _pass = config["RabbitMQ:Password"] ?? "guest";
    private const string Exchange = "solicitudes.events";
    private const string Queue = "solicitudes.processor";
    public async Task PublishAsync(SolicitudEvent evt, CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = _host, UserName = _user, Password = _pass, ClientProvidedName = "solicitudes-api" };
        await using var conn = await factory.CreateConnectionAsync(ct); await using var ch = await conn.CreateChannelAsync(cancellationToken: ct);
        await ch.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await ch.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await ch.QueueBindAsync(Queue, Exchange, "solicitud.*", cancellationToken: ct);
        var body = JsonSerializer.SerializeToUtf8Bytes(evt);
        await ch.BasicPublishAsync(Exchange, evt.Tipo, false, new BasicProperties { MessageId = evt.MessageId, Persistent = true, ContentType = "application/json" }, body, ct);
    }
}

public sealed class DocumentoService(AppDbContext db) : IDocumentoService
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    { "application/pdf", "image/png", "image/jpeg", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/msword", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/vnd.ms-excel" };

    public async Task<List<DocumentoDto>> GetAllAsync(Guid solicitudId, CancellationToken ct)
    {
        if (!await db.Solicitudes.AnyAsync(x => x.Id == solicitudId, ct)) throw new KeyNotFoundException("Solicitud no encontrada.");
        return await db.DocumentosSolicitudes.AsNoTracking().Where(x => x.SolicitudId == solicitudId)
            .OrderBy(x => x.FechaCreacionUtc).Select(x => new DocumentoDto(x.Id, x.NombreArchivo, x.ContentType, x.Tamano, x.FechaCreacionUtc)).ToListAsync(ct);
    }

    public async Task<DocumentoDto> UploadAsync(Guid solicitudId, string fileName, string contentType, Stream content, CancellationToken ct)
    {
        if (!await db.Solicitudes.AnyAsync(x => x.Id == solicitudId, ct)) throw new KeyNotFoundException("Solicitud no encontrada.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("El nombre del archivo es obligatorio.");
        if (!AllowedTypes.Contains(contentType)) throw new ArgumentException("Tipo de archivo no permitido. Use PDF, Word, Excel, PNG o JPG.");
        if (content.Length > MaxFileSize) throw new ArgumentException("El archivo supera el límite de 10 MB.");
        using var ms = new MemoryStream(); await content.CopyToAsync(ms, ct);
        var entity = new DocumentoSolicitud { Id = Guid.NewGuid(), SolicitudId = solicitudId, NombreArchivo = Path.GetFileName(fileName), ContentType = contentType, Tamano = ms.Length, Contenido = ms.ToArray() };
        db.DocumentosSolicitudes.Add(entity); await db.SaveChangesAsync(ct);
        return new DocumentoDto(entity.Id, entity.NombreArchivo, entity.ContentType, entity.Tamano, entity.FechaCreacionUtc);
    }

    public async Task<(string FileName, string ContentType, byte[] Content)?> DownloadAsync(Guid solicitudId, Guid documentId, CancellationToken ct)
    {
        var d = await db.DocumentosSolicitudes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId && x.SolicitudId == solicitudId, ct);
        return d is null ? null : (d.NombreArchivo, d.ContentType, d.Contenido);
    }

    public async Task DeleteAsync(Guid solicitudId, Guid documentId, CancellationToken ct)
    {
        var d = await db.DocumentosSolicitudes.FirstOrDefaultAsync(x => x.Id == documentId && x.SolicitudId == solicitudId, ct) ?? throw new KeyNotFoundException("Documento no encontrado.");
        db.DocumentosSolicitudes.Remove(d); await db.SaveChangesAsync(ct);
    }
}

public sealed class MessageIdempotency(AppDbContext db) : IMessageIdempotency
{
    public async Task<bool> TryRegisterAsync(string messageId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(messageId)) return false;
        if (await db.MensajesProcesados.AnyAsync(x => x.MessageId == messageId, ct)) return false;
        db.MensajesProcesados.Add(new MensajeProcesado { MessageId = messageId });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
    }
}

public static class SeedData
{
    public static async Task EnsureAsync(AppDbContext db, IPasswordService passwords)
    {
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('Solicitudes','NombreBeneficiario') IS NULL ALTER TABLE Solicitudes ADD NombreBeneficiario nvarchar(200) NOT NULL CONSTRAINT DF_Solicitudes_NombreBeneficiario DEFAULT '';
IF COL_LENGTH('Solicitudes','Nit') IS NULL ALTER TABLE Solicitudes ADD Nit nvarchar(50) NOT NULL CONSTRAINT DF_Solicitudes_Nit DEFAULT '';
IF COL_LENGTH('Solicitudes','CuentaOrigen') IS NULL ALTER TABLE Solicitudes ADD CuentaOrigen nvarchar(100) NOT NULL CONSTRAINT DF_Solicitudes_CuentaOrigen DEFAULT '';
IF COL_LENGTH('Solicitudes','CuentaDestino') IS NULL ALTER TABLE Solicitudes ADD CuentaDestino nvarchar(100) NOT NULL CONSTRAINT DF_Solicitudes_CuentaDestino DEFAULT '';
IF COL_LENGTH('Solicitudes','FuenteFinanciamiento') IS NULL ALTER TABLE Solicitudes ADD FuenteFinanciamiento nvarchar(100) NOT NULL CONSTRAINT DF_Solicitudes_FuenteFinanciamiento DEFAULT '';
IF COL_LENGTH('Solicitudes','EstructuraPresupuestaria') IS NULL ALTER TABLE Solicitudes ADD EstructuraPresupuestaria nvarchar(200) NOT NULL CONSTRAINT DF_Solicitudes_EstructuraPresupuestaria DEFAULT '';
IF COL_LENGTH('Solicitudes','Monto') IS NULL ALTER TABLE Solicitudes ADD Monto decimal(18,2) NOT NULL CONSTRAINT DF_Solicitudes_Monto DEFAULT 0;
IF COL_LENGTH('Solicitudes','Moneda') IS NULL ALTER TABLE Solicitudes ADD Moneda nvarchar(3) NOT NULL CONSTRAINT DF_Solicitudes_Moneda DEFAULT 'Q';
IF OBJECT_ID('DocumentosSolicitudes','U') IS NULL
BEGIN
CREATE TABLE DocumentosSolicitudes (Id uniqueidentifier NOT NULL PRIMARY KEY, SolicitudId uniqueidentifier NOT NULL, NombreArchivo nvarchar(255) NOT NULL, ContentType nvarchar(150) NOT NULL, Tamano bigint NOT NULL, Contenido varbinary(max) NOT NULL, FechaCreacionUtc datetime2 NOT NULL, CONSTRAINT FK_DocumentosSolicitudes_Solicitudes FOREIGN KEY (SolicitudId) REFERENCES Solicitudes(Id) ON DELETE CASCADE);
CREATE INDEX IX_DocumentosSolicitudes_SolicitudId ON DocumentosSolicitudes(SolicitudId);
END", cancellationToken: default);
        if (!await db.Usuarios.AnyAsync()) { db.Usuarios.Add(new Usuario { UsuarioLogin = "admin", PasswordHash = passwords.Hash("Admin123!") }); await db.SaveChangesAsync(); }
    }
}
