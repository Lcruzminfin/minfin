using Moq;
using SolicitudSystem.Application;
using SolicitudSystem.Domain;

namespace SolicitudSystem.Tests;

public class SolicitudServiceTests
{
    [Fact]
    public async Task Create_DebeCrearSolicitudEnPendiente()
    {
        var repo = new Mock<ISolicitudRepository>(); var crypto = new Mock<IEncryptionService>(); var events = new Mock<IEventPublisher>();
        crypto.Setup(x => x.Encrypt(It.IsAny<string>())).Returns("cipher");
        var service = new SolicitudService(repo.Object, crypto.Object, events.Object);
        var result = await service.CreateAsync(new("Problema acceso", "No puedo iniciar sesión", "secreto", "Juan Perez", "1234567-8", "001", "002", "Fuente 11", "Estructura 1", 100m, "Q"), "admin", default);
        Assert.Equal(EstadoSolicitud.Pendiente, result.Estado); repo.Verify(x => x.AddAsync(It.IsAny<Solicitud>(), It.IsAny<CancellationToken>()), Times.Once); events.Verify(x => x.PublishAsync(It.IsAny<SolicitudEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Status_NoDebePermitirPendienteAResuelta()
    {
        var entity = new Solicitud { Id = Guid.NewGuid() }; var repo = new Mock<ISolicitudRepository>(); repo.Setup(x => x.GetAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var service = new SolicitudService(repo.Object, Mock.Of<IEncryptionService>(), Mock.Of<IEventPublisher>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeStatusAsync(entity.Id, new(EstadoSolicitud.Resuelta), default));
    }
}
