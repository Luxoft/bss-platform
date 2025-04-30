namespace Bss.Platform.RabbitMq.Consumer.Internal;

internal sealed record MessageLink(Type MessageType, string RoutingKey);
