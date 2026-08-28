using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SolicitudSystem.Application;
using SolicitudSystem.Infrastructure;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IMessageIdempotency, MessageIdempotency>();
builder.Services.AddHostedService<EventConsumer>();
await builder.Build().RunAsync();

public sealed class EventConsumer(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<EventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = config["RabbitMQ:Host"] ?? "localhost", UserName = config["RabbitMQ:User"] ?? "guest", Password = config["RabbitMQ:Password"] ?? "guest", ClientProvidedName = "solicitudes-worker" };
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.ExchangeDeclareAsync("solicitudes.events", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync("solicitudes.processor", durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                await channel.QueueBindAsync("solicitudes.processor", "solicitudes.events", "solicitud.*", cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        var messageId = ea.BasicProperties.MessageId ?? "";
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var idem = scope.ServiceProvider.GetRequiredService<IMessageIdempotency>();
                        if (!await idem.TryRegisterAsync(messageId, stoppingToken)) { await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken); return; }
                        var evt = JsonSerializer.Deserialize<SolicitudEvent>(ea.Body.Span);
                        logger.LogInformation("Evento procesado: {Type}, Solicitud: {Id}", evt?.Tipo, evt?.SolicitudId);
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex) { logger.LogError(ex, "Error procesando evento"); await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken); }
                };
                await channel.BasicConsumeAsync("solicitudes.processor", false, consumer, stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "RabbitMQ no disponible; reintentando en 5 segundos."); await Task.Delay(5000, stoppingToken); }
        }
    }
}
