[![License](https://img.shields.io/github/license/luxoft/bss-platform)](LICENSE)
![Nuget](https://img.shields.io/nuget/v/Luxoft.Bss.Platform.RabbitMq.Consumer)
[![Build & Tests](https://github.com/luxoft/bss-platform/actions/workflows/pr.yml/badge.svg)](https://github.com/Luxoft/bss-platform/actions/workflows/pr.yml)

# BSS Platform

This repository offers a wide collection of .NET packages for use in microservices architecture.

# Sections

- [RabbitMQ Consumer](#Consumer)
- [Logging](#Logging)
- [API Middlewares](#Middlewares)
- [API Documentation](#Documentation)
- [Domain Events](#Domain-Events)
- [Integration Events](#Integration-Events)
- [Kubernetes Insights](#Insights)
- [Kubernetes Health Checks](#Health-Checks)
- [NHibernate](#NHibernate)

## RabbitMQ

### Consumer
To use the RabbitMQ consumer, first install the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.RabbitMq.Consumer):
```shell
dotnet add package Luxoft.Bss.Platform.RabbitMq.Consumer
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

Finally, register the RabbitMQ consumer in DI
```C#
services
    .AddPlatformRabbitMqClient(configuration)
    .AddPlatformRabbitMqConsumer<MessageProcessor>(configuration);
```

> [!IMPORTANT]
> If you run more than one consumer, they will consume messages in parallel mode. In order to change it follow the instructions below.

Switch consuming mode in appsettings.json file
```json
{
  "RabbitMQ": {
    "Consumer": {
      "Mode": "SingleActiveConsumer"
    }
  }
}
```

Register the consumer lock service in DI
```C#
services
    .AddPlatformRabbitMqSqlServerConsumerLock(configuration.GetConnectionString("ms sql connection string"));
```

## Logging
To use platform logger, first install the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Logging):
```shell
dotnet add package Luxoft.Bss.Platform.Logging
```

In the second step, you need register the logger in DI
```C#
builder.Host.AddPlatformLogging();
```

Finally, register sinks in appsettings.json file
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "System": "Error",
        "Microsoft": "Error",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ]
  }
}
```
## API

### Middlewares
To use platform API middlewares, first install the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Api.Middlewares):
```shell
dotnet add package Luxoft.Bss.Platform.Api.Middlewares
```

#### Errors
To log exceptions you need to use errors middleware.
```C#
app.UsePlatformErrorsMiddleware(); // This middleware should be the first
```
> [!IMPORTANT]
> If you need to change response status code, then you should register status code resolver.
```C#
services.AddSingleton<IStatusCodeResolver, YourStatusCodeResolver>();
```

### Documentation
To use platform API documentation, first install the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Api.Documentation):
```shell
dotnet add package Luxoft.Bss.Platform.Api.Documentation
```

Finally, you need register it in DI
```C#
services
  .AddPlatformApiDocumentation(builder.Environment);

app
  .UsePlatformApiDocumentation(builder.Environment);
```

## Events
To use events, first install the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Events):
```shell
dotnet add package Luxoft.Bss.Platform.Events
```

### Domain Events

To use domain events, you need register it in DI
```C#
services
  .AddPlatformDomainEvents();
```
Now, you can use it in this way
```C#
public class CommandHandler(IDomainEventPublisher eventPublisher) : IRequestHandler<Command>
{
    public async Task Handle(Command request, CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(new DomainEvent(), cancellationToken);
    }
}
```

### Integration Events
In the first step, you need implement interface IIntegrationEventProcessor
```C#
public class IntegrationEventProcessor : IIntegrationEventProcessor
{
    public Task ProcessAsync(IIntegrationEvent @event, CancellationToken cancellationToken)
    {
        // write your consuming logic here
    }
}
```

Then, register integration events in DI
```C#
services
  .AddPlatformIntegrationEvents<IntegrationEventProcessor>(
      typeof(IntegrationEvents).Assembly,
      x =>
      {
          x.SqlServer.ConnectionString = "ms sql connection string";

          x.MessageQueue.ExchangeName = "integration.events";
          x.MessageQueue.Host = "RabbitMQ host";
          x.MessageQueue.Port = "RabbitMQ port";
          x.MessageQueue.VirtualHost = "RabbitMQ virtual host";
          x.MessageQueue.UserName = "RabbitMQ username";
          x.MessageQueue.Secret = "RabbitMQ secret";
      });
```

Now, you can use it in this way
```C#
public class CommandHandler(IIntegrationEventPublisher eventPublisher) : IRequestHandler<Command>
{
    public async Task Handle(Command request, CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(new IntegrationEvent(), cancellationToken);
    }
}
```

Additional options

| Option                  | Description                                                                           | Type   | Default value |
| ----------------------- | ------------------------------------------------------------------------------------- | ------ | --------------|
| **DashboardPath**       | Dashboard relative path                                                               | string | /admin/events |
| **FailedRetryCount**    | The number of message retries                                                         | int    | 5             |
| **RetentionDays**       | Success message live period                                                           | int    | 15            |
| **SqlServer.Schema**    | Shema name for event tables                                                           | string | events        |
| **MessageQueue.Enable** | For developer purpose only. If false, then switches RabbitMQ queue to queue in memory | string | true          |
|                         |

## Kubernetes

To use kubernetes utils, first install the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Kubernetes):
```shell
dotnet add package Luxoft.Bss.Platform.Kubernetes
```

### Insights

To enable application insights, just register it in DI
```C#
services
  .AddPlatformKubernetesInsights(builder.Configuration);
```

### Health Checks

To add health checks for your service, you need register it in DI
```C#
services
  .AddPlatformKubernetesHealthChecks("your db connection string");

app
  .UsePlatformKubernetesHealthChecks();
```

After that, health checks will be available by URLs
- /health/live
- /health/ready

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
