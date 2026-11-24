namespace Bss.Platform.RabbitMq.Services;

internal sealed class RabbitMqStartupConnectionException()
    : InvalidOperationException("RabbitMQ initialization could not establish a connection.");
