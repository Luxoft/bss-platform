[![License](https://img.shields.io/github/license/luxoft/bss-platform)](LICENSE)

# BSS Platform

This repository offers a wide collection of .NET packages for use in microservices architecture.

## RabbitMQ

### Consumer
To use the RabbitMQ consumer, first install the [NuGet package](https://www.nuget.org/packages/Bss.Platform.RabbitMq.Consumer):
```shell
dotnet add package Bss.Platform.RabbitMq.Consumer
```
In the second step, you need implement interface IRabbitMqMessageProcessor
```C#
public class MessageProcessor : IRabbitMqMessageProcessor
{
    public Task ProcessAsync(IBasicProperties properties, string routingKey, string message, CancellationToken token)
    {
        // write your consuming logic here
    }
}
```

Finally, register the RabbitMq consumer in DI
```C#
services
    .AddPlatformRabbitMqConsumer<MessageProcessor>(configuration);
```

> [!IMPORTANT]
> If you run more than one consumer, they will consume messages in parallel mode. In order to change it you need

## NHibernate

### Unit Testing
To use the IQueryable mock, first install the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.NHibernate.UnitTesting):
```shell
dotnet add package Luxoft.Bss.Platform.NHibernate.UnitTesting
```

To mock IQueryable, in your code, call:
```C#
var entity = new Entity();
repository.GetQueryable().Returns(new TestQueryable<Entity>(entity));
```
