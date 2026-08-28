using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolicitudSystem.Application;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) => (await auth.LoginAsync(request, ct)) is { } result ? Ok(result) : Unauthorized(new { error = "Usuario o contraseña inválidos." });
}

[ApiController, Route("api/solicitudes"), Authorize]
public sealed class SolicitudesController(ISolicitudService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SolicitudDto>>> Get(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<SolicitudDto>> Create(CrearSolicitudRequest request, CancellationToken ct) => Ok(await service.CreateAsync(request, User.Identity?.Name ?? "unknown", ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SolicitudDto>> Update(Guid id, ActualizarSolicitudRequest request, CancellationToken ct) => Ok(await service.UpdateAsync(id, request, User.Identity?.Name ?? "unknown", ct));

    [HttpPatch("{id:guid}/estado")]
    public async Task<ActionResult<SolicitudDto>> Status(Guid id, CambiarEstadoRequest request, CancellationToken ct) => Ok(await service.ChangeStatusAsync(id, request, ct));
}


[ApiController, Route("api/solicitudes/{solicitudId:guid}/documentos"), Authorize]
public sealed class DocumentosController(IDocumentoService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DocumentoDto>>> Get(Guid solicitudId, CancellationToken ct) => Ok(await service.GetAllAsync(solicitudId, ct));

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DocumentoDto>> Upload(Guid solicitudId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Debe seleccionar un archivo." });
        return Ok(await service.UploadAsync(solicitudId, file.FileName, file.ContentType, file.OpenReadStream(), ct));
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid solicitudId, Guid documentId, CancellationToken ct)
    {
        var result = await service.DownloadAsync(solicitudId, documentId, ct);
        return result is null ? NotFound(new { error = "Documento no encontrado." }) : File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid solicitudId, Guid documentId, CancellationToken ct)
    {
        await service.DeleteAsync(solicitudId, documentId, ct);
        return NoContent();
    }
}
