[![License](https://img.shields.io/github/license/luxoft/bss-platform)](LICENSE)
![Nuget](https://img.shields.io/nuget/v/Luxoft.Bss.Platform.RabbitMq.Consumer)
[![Build & Tests](https://github.com/luxoft/bss-platform/actions/workflows/pr.yml/badge.svg)](https://github.com/Luxoft/bss-platform/actions/workflows/pr.yml)

# BSS Platform

This repository offers a wide collection of .NET packages for use in microservices architecture.

# Sections

- [RabbitMQ Consumer](#Consumer)
- [JsonSchema generator for Rabbit events](#json-schema-generator-for-rabbit-events)
- [Logging](#Logging)
- [API Middlewares](#Middlewares)
- [API Documentation](#Documentation)
- [Domain Events](#Domain-Events)
- [Integration Events](#Integration-Events)
- [Kubernetes Insights](#Insights)
- [Kubernetes Health Checks](#Health-Checks)
- [NHibernate](#NHibernate)
- [Notifications](#Notifications)
- [Notifications Audit](#Notifications-Audit)

## RabbitMQ

### Consumer

To use the RabbitMQ consumer, first install
the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.RabbitMq.Consumer):

```shell
dotnet add package Luxoft.Bss.Platform.RabbitMq.Consumer
```

then you can choose one of three ways to add the consumer:

- [with event marked by attribute](#Register-event-with-attribute)
- [with manual listed events (builder)](#Register-event-with-builder)
- [Old way, implement IRabbitMqMessageProcessor](#Old-way-IRabbitMqMessageProcessor)

#### Register event with attribute

> **!IMPORTANT:**
> `RoutingKeys` section in `RabbitMqConsumerSettings` (config) should be empty (removed), because it fills that section
> automatically (and throw error if config has some data)

this way requires implementing `IRabbitMqEventProcessor<TEvent>` interface, where `TEvent` - base type of messages

```C#
public interface IRabbitMqEventProcessor<in TEvent>
{
    Task ProcessAsync(TEvent @event, CancellationToken token);
}
```

and then add:

```C#
services
    .AddPlatformRabbitMqClient(configuration)
    .AddPlatformRabbitMqConsumerWithMessages<RabbitMqEventProcessorImplementation, IRequest, RabbitEventAttribute>(
            configuration,
            x => x.RoutingKey);
```

where

```C#
public class RabbitEventAttribute(string routingKey) : Attribute
{
    public string RoutingKey { get; } = routingKey;
}

[RabbitEvent("RoutingKey1")]
public record MessageOne(string Name, Guid Id): IRequest;
```

This method will find all types that has attribute `RabbitEvent` in the attribute's assembly and try to add them to
consumer for handling corresponded routing key.

> If type is not implement / inherit type `IRequest` (second generic argument of
`AddPlatformRabbitMqConsumerWithMessages`) -
> will throw exception (on startup)

#### Register event with builder

> **!IMPORTANT:**
> `RoutingKeys` section in `RabbitMqConsumerSettings` (config) should be empty (removed), because it fills that section
> automatically (and throw error if config has some data)

this way requires implementing `IRabbitMqEventProcessor<TEvent>` interface, where `TEvent` - base type of messages

```C#
public interface IRabbitMqEventProcessor<in TEvent>
{
    Task ProcessAsync(TEvent @event, CancellationToken token);
}
```

and then add:

```C#
services
    .AddPlatformRabbitMqClient(configuration)
    .AddPlatformRabbitMqConsumerWithMessages<RabbitMqEventProcessorImplementation, IRequest>(
        configuration,
        x => x
            .Add<MessageOne>("RoutingKey1")
            .Add<MessageTwo>("RoutingKey2"));
```

This way allows to explicitly add all required messages with routing keys to consumer.

#### Old way (IRabbitMqMessageProcessor)

You need implement interface IRabbitMqMessageProcessor

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

> **!IMPORTANT:**
> `RoutingKeys` section in `RabbitMqConsumerSettings` (config) should contain all routing keys that you want to consume
> and it will be added to queue's bindings (if `RoutingKeys` is empty - adds `#` binding)

> **!IMPORTANT:**
> If you run more than one consumer, they will consume messages in parallel mode. In order to change it follow the
> instructions below.

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

### JsonSchema Generator for Rabbit events

Allow to generate json schema for consuming and producing types,
based on [NJsonSchema](https://github.com/RicoSuter/NJsonSchema)

To use just add a new middleware via the extension:
```C#
if (app.Environment.IsDevelopment())
{
       app.UseRabbitJsonSchemaGenerator(opt =>
       {
              opt.Path = "/api/rabbit-json-schema";
              opt.TypePrefix = "TSS";
              opt.ProducedEventTypes = typeof(IEvent).Assembly.GetTypes()
                     .Where(x => x.IsPublic && !x.IsAbstract && !x.IsInterface)
                     .Where(x => x.GetInterfaces().Contains(typeof(IEvent)));
       });
}
```
`TypePrefix` and `ProducedEventTypes` are required only for produced events<br>
Consumed events are automatically added if you use the new way to register consumer with
method [AddPlatformRabbitMqConsumerWithMessages](#Register-event-with-builder)

For more complex cases please use middleware without extensions:
```C#
app.UseMiddleware<GenerateSchemaMiddleware>("/api/rabbit-json-schema", allEventsDict);
```
where `Dictionary<string, Type> allEventsDict` contains the mapping between routing keys and types


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

To use platform API middlewares, first install
the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Api.Middlewares):

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

To use platform API documentation, first install
the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Api.Documentation):

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

Now, you can publish it in this way

```C#
public class CommandHandler(IDomainEventPublisher eventPublisher) : IRequestHandler<Command>
{
    public async Task Handle(Command request, CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(new DomainEvent(), cancellationToken);
    }
}
```

And process it in this way

```C#
public class EventHandler : INotificationHandler<DomainEvent>
{
    public async Task Handle(DomainEvent notification, CancellationToken cancellationToken)
    {
        // your logic
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

Now, you can publish it in this way

```C#
public class CommandHandler(IIntegrationEventPublisher eventPublisher) : IRequestHandler<Command>
{
    public async Task Handle(Command request, CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(new IntegrationEvent(), cancellationToken);
    }
}
```

And process it in this way

```C#
public class EventHandler : INotificationHandler<IntegrationEvent>
{
    public async Task Handle(IntegrationEvent notification, CancellationToken cancellationToken)
    {
        // your logic
    }
}
```

Options

| Option                  | Description                                                                           | Type   | Default value |
|-------------------------|---------------------------------------------------------------------------------------|--------|---------------|
| **DashboardPath**       | Dashboard relative path                                                               | string | /admin/events |
| **FailedRetryCount**    | The number of message retries                                                         | int    | 5             |
| **RetentionDays**       | Success message live period                                                           | int    | 15            |
| **SqlServer.Schema**    | Shema name for event tables                                                           | string | events        |
| **MessageQueue.Enable** | For developer purpose only. If false, then switches RabbitMQ queue to queue in memory | string | true          |

## Kubernetes

To use kubernetes utils, first install
the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Kubernetes):

```shell
dotnet add package Luxoft.Bss.Platform.Kubernetes
```

### Insights

To enable application insights, just register it in DI

```C#
services
  .AddPlatformKubernetesInsights(
      builder.Configuration,
      opt =>
      {
          opt.SkipSuccessfulDependency = true;
          opt.RoleName = Environment.GetEnvironmentVariable("APP_NAME");
          opt.AdditionalHealthCheckPathToSkip = ["/DoHealthCheck"];
      });
```

All settings have default values and optional (as well as `opt` argument in `AddPlatformKubernetesInsights`):

| Option                                  | Description                                                             | Type     | Default value     |
|-----------------------------------------|-------------------------------------------------------------------------|----------|-------------------|
| **SkipSuccessfulDependency**            | Skip dependency without error                                           | bool     | false             |
| **SkipDefaultHealthChecks**             | Skip requests to default health checks (`/health/ready`,`/health/live`) | bool     | true              |
| **AdditionalHealthCheckPathToSkip**     | Additional paths to skip their requests as healthchecks                 | string[] | []                |
| **SetAuthenticatedUserFromHttpContext** | Fill AuthenticatedUserId from HttpContext.User.Identity.Name            | bool     | true              |
| **RoleName**                            | Value to set as role name for AppInsight requests                       | string   | `<assembly name>` |

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

To use the IQueryable mock, first install
the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.NHibernate.UnitTesting):

```shell
dotnet add package Luxoft.Bss.Platform.NHibernate.UnitTesting
```

To mock IQueryable, in your code, call:

```C#
var entity = new Entity();
repository.GetQueryable().Returns(new TestQueryable<Entity>(entity));
```

## Notifications

To use platform senders for notifications, first install
the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Notifications):

```shell
dotnet add package Luxoft.Bss.Platform.Notifications
```

Then register notifications service in DI

```C#
services
    .AddPlatformNotifications(builder.Environment, builder.Configuration)
```

Then fill configuration settings:

```json
{
  "NotificationSender": {
    "Server": "smtp.server.com", // smtp server host
    "RedirectTo": ["test@email.com"], // all messages on non-prod environments will be sent to these addresses, recipients will be listed in message body
    "DefaultRecipients": ["your_support@email.com"] // if no recipients are provided for a message then these emails will become recipients
  }
}
```

Now you can send messages to smtp server:

```C#
public class YourNotificationRequestHandler(IEmailSender sender) : IRequestHandler<YourNotificationRequest>
{
    public async Task Handle(YourNotificationRequest request, CancellationToken cancellationToken)
    {
        var attachment = new Attachment(new MemoryStream(), request.AttachmentName);
        attachment.ContentDisposition!.Inline = true;
    
        var message = new EmailModel(
            request.Subject,
            request.Body,
            new MailAddress(request.From),
            new[] { new MailAddress(request.To) },
            Attachments: new[] { attachment });
    
        await sender.SendAsync(message, token);
    }
}
```

> [!NOTE]
> Attachment will be added to notification only if:
> - it is not inlined
> - it is inlined and referred by name as image source in notification text

### Notifications Audit

To audit sent notifications, first install
the [NuGet package](https://www.nuget.org/packages/Luxoft.Bss.Platform.Notifications.Audit):

```shell
dotnet add package Luxoft.Bss.Platform.Notifications.Audit
```

Then register notifications service in DI with provided sql connection

```C#
services
    .AddPlatformNotificationsAudit(o => o.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!);
```

Thats all - db schema and tables will be generated on application start (you can customize schema and table names on DI
step).
