# Luxoft.Bss.Platform.Events

Integration & domain events for microservices, built on top of [DotNetCore.CAP](https://github.com/dotnetcore/CAP)
(outbox pattern over RabbitMQ + SQL Server) and integrated with
[`Bss.Platform.Mediation`](../../README.md#custom-mediation).

```shell
dotnet add package Luxoft.Bss.Platform.Events
```

> This document describes the changes introduced **after** the base version `1.6.8` (package `1.6.9`).
> For the general "getting started" guide see the [Events section](../../README.md#events) of the root README.

## Table of contents

- [What changed (overview)](#what-changed-overview)
- [Registration methods (
  `AddPlatformIntegrationEvents` overloads)](#registration-methods-addplatformintegrationevents-overloads)
    - [1. Legacy — assembly scan](#1-legacy--assembly-scan)
    - [2. Setup-based with `IIntegrationEvent` base](#2-setup-based-with-iintegrationevent-base)
    - [3. Setup-based with a custom base type](#3-setup-based-with-a-custom-base-type)
    - [4. Setup-based with separate input/output base types](#4-setup-based-with-separate-inputoutput-base-types)
    - [Which overload to use](#which-overload-to-use)
- [Input vs output events](#input-vs-output-events)
- [Publishing events](#publishing-events)
- [Failed event processing](#failed-event-processing)
- [External system queue bindings](#external-system-queue-bindings)
- [Options](#options)
- [Breaking changes](#breaking-changes)
- [Switching to the new registration (queue name & `.v1`)](#switching-to-the-new-registration-queue-name--v1)
- [Other notes / things to review](#other-notes--things-to-review)

## What changed (overview)

The integration-events module was reworked to support explicit, per-type event registration and to produce
"external" events for other systems in addition to consuming its own.

Highlights:

- **New setup-based registration** via `Action<IIntegrationEventSetup<...>>` — you now list the event types
  (and their routing keys) explicitly instead of relying only on an assembly scan.
- **Input vs output events** — a distinction between events that are *consumed & handled* inside the service
  (`Input`) and events that are only *produced* for other systems (`Output`). See
  [`IEventTypeProvider`](../Bss.Platform.Events.Abstractions/IEventTypeProvider.cs).
- **Generic publisher / processor** — `IIntegrationEventPublisher<T>` and `IIntegrationEventProcessor<T>`;
  the old non-generic interfaces now inherit from the `IIntegrationEvent` closed versions.
- **Failed event processing** — opt-in CAP filter (`UseFailedEventProcessor`) that invokes any number of
  `IFailedEventProcessor` implementations on the last retry.
- **Custom RabbitMQ headers** — messages produced by the new selector carry `MessageId` (snowflake),
  `MessageName` (routing key) and `Type` headers so consumers built outside of CAP can read them.
- **Options cleanup & new options** — `IntegrationEventsOptions.Default` removed in favour of inline defaults;
  new `GatewayPrefix`, `QueueName`, `OverrideCapOptions`, `UseFailedEventProcessor`; `SqlServer.ConnectionString`
  and RabbitMQ are now optional (fallback to no-DB / in-memory queue), default RabbitMQ `Port` is `5672`.
- **Queue-name behaviour change** — the new (multi-generic) overloads no longer append `.v1` to the queue name.
  This is the most important behavioural change — see
  [Switching to the new registration](#switching-to-the-new-registration-queue-name--v1).

## Registration methods (`AddPlatformIntegrationEvents` overloads)

There are now four overloads. All of them ultimately register CAP (SQL Server outbox + RabbitMQ or in-memory),
the dashboard and a publisher; they differ in **how event types are discovered** and **which base type / publisher**
is used.

### 1. Legacy — assembly scan

```C#
services.AddPlatformIntegrationEvents<TEventProcessor>(
    Assembly eventsAssembly,
    Action<IntegrationEventsOptions>? setup = null);
// where TEventProcessor : class, IIntegrationEventProcessor
```

- Scans `eventsAssembly` for every non-abstract type assignable to `IIntegrationEvent` and subscribes to each
  using the **type name** as the routing key.
- `TEventProcessor` is the non-generic `IIntegrationEventProcessor` (handles `IIntegrationEvent`).
- Registers the non-generic legacy `IIntegrationEventPublisher`.
- **Appends `.v1` to the queue name** to preserve the historical CAP group name.

Use this when upgrading an existing service and you don't want to change anything else.

### 2. Setup-based with `IIntegrationEvent` base

```C#
services.AddPlatformIntegrationEvents<TEventProcessor>(
    Action<IIntegrationEventSetup<IIntegrationEvent, IIntegrationEvent>> setupEvents,
    Action<IntegrationEventsOptions> setupOptions);
// where TEventProcessor : class, IIntegrationEventProcessor<IIntegrationEvent>
```

- Same base type (`IIntegrationEvent`) as the legacy method, but you register the events **explicitly** via the
  setup action (see [Input vs output events](#input-vs-output-events)).
- Registers the legacy `IIntegrationEventPublisher` for backward compatibility.

Use this when you want the new explicit registration / output events but keep `IIntegrationEvent` as your base.

### 3. Setup-based with a custom base type

```C#
services.AddPlatformIntegrationEvents<TEventProcessor, TEvent>(
    Action<IIntegrationEventSetup<TEvent, TEvent>> setupEvents,
    Action<IntegrationEventsOptions> setupOptions);
// where TEventProcessor : class, IIntegrationEventProcessor<TEvent>
// where TEvent : notnull
```

- Your own base type `TEvent` for both consumed and produced events (input == output).
- Registers `IIntegrationEventPublisher<TEvent>`.
- **Does not append `.v1`** to the queue name.

Use this when your events do not implement `IIntegrationEvent` and input/output share one base type.

### 4. Setup-based with separate input/output base types

```C#
services.AddPlatformIntegrationEvents<TEventProcessor, TInputEvent, TOutputEvent>(
    Action<IIntegrationEventSetup<TInputEvent, TOutputEvent>> setupEvents,
    Action<IntegrationEventsOptions> setupOptions);
// where TEventProcessor : class, IIntegrationEventProcessor<TInputEvent>
// where TInputEvent : notnull
// where TOutputEvent : notnull
```

- The most flexible overload. `TInputEvent` is the base of events **consumed & handled** by this service;
  `TOutputEvent` is the base of events **only produced** for other systems.
- Registers `IIntegrationEventPublisher<TOutputEvent>` (wrap it if you need a narrower contract).
- **Does not append `.v1`** to the queue name.
- Overloads (3) and (2) both delegate to this one.

Use this when produced and consumed events have different base contracts.

### Which overload to use

| Situation                                                            | Overload                     |
|----------------------------------------------------------------------|------------------------------|
| Just upgrading the package, no code changes wanted                   | **(1)** Legacy assembly scan |
| Want explicit registration / output events, keep `IIntegrationEvent` | **(2)**                      |
| Custom base type, input == output                                    | **(3)**                      |
| Different base types for consumed vs produced events                 | **(4)**                      |

## Input vs output events

The setup action exposes [`IIntegrationEventSetup<TIn, TOut>`](Interfaces/IIntegrationEventSetup.cs):

```C#
services.AddPlatformIntegrationEvents<MyProcessor, IMyInputEvent, IMyOutputEvent>(
    events => events
        // consumed & handled inside this service (Rabbit -> CAP -> processor):
        .AddInputEvents<IMyInputEvent>("INT.")            // scan assembly of IMyInputEvent, routing key "INT." + TypeName
        .AddInputEvent<SpecificEvent>("custom.routing.key") // single type, explicit routing key (overrides scan)
        // only produced for other systems (published to the Rabbit exchange, no local handler):
        .AddOutputEvents<IMyOutputEvent>("EXT.", typeof(SomeOtherEvent).Assembly)
        .AddOutputEvent<AnotherOutgoingEvent>("some.routing.key"),
    options =>
    {
        options.SqlServer.ConnectionString = "...";
        options.MessageQueue.ExchangeName = "integration.events";
        options.MessageQueue.Host = "...";
        // ...
    });
```

- **Input events** are subscribed in CAP and dispatched to `IIntegrationEventProcessor<TInputEvent>`
  (a single processor instance is reused for every input type).
- **Output events** are not subscribed; they only get a routing key so the publisher can emit them.
- `AddInputEvents` / `AddOutputEvents` scan the assembly containing the base type (or the assemblies you pass)
  and build the routing key as `prefix + TypeName`. `AddInputEvent` / `AddOutputEvent` register a single type
  with an explicit routing key and **override** any entry added by the scan.

## Publishing events

```C#
public class Handler(IIntegrationEventPublisher<IMyOutputEvent> publisher) : IRequestHandler<Command>
{
    public Task Handle(Command request, CancellationToken ct) =>
        publisher.PublishAsync(new MyEvent(), ct);
}
```

- The new publisher (`IntegrationEventPublisherNew<T>`) resolves the routing key from the type provider:
  if the type is registered as input it is published to the internal routing key, and if it is also registered
  as output (with a different key) it is additionally published to the external routing key. If the type is
  registered in neither, `PublishAsync` throws.
- The legacy publisher (`IntegrationEventPublisherLegacy`, exposed as `IIntegrationEventPublisher`) keeps the old
  behaviour of publishing with the routing key equal to the event's type name.

## Failed event processing

Enable it via options and register one or more `IFailedEventProcessor` implementations:

```C#
options.UseFailedEventProcessor = true;

services.AddScoped<IFailedEventProcessor, MyFailedEventProcessor>();
```

```C#
public interface IFailedEventProcessor
{
    Task HandleAsync(object? value, Exception ex);
}
```

- When enabled, a CAP `ISubscribeFilter` (`CapExceptionFilter`) is registered.
- It fires **only on the last retry** (accounting for the `0`-retries case) and invokes **all** registered
  `IFailedEventProcessor` instances.
- The failed payload is deserialized to the handler's parameter type when the CAP value is JSON; otherwise the raw
  value is passed. Failure details (`ExceptionType`, `Message`, `StackTrace`) are also written to the
  `x-cap-failure-details` message header.

## External system queue bindings

Output events (see [Input vs output events](#input-vs-output-events)) are published to the exchange, but by default
nothing declares queues/bindings for external subscribers — someone has to create the queue and bind it to the
routing keys they care about. `ExternalSystemBindingsOptions` lets you declare N queues (one per external system)
and have them bound automatically at startup, with per-queue overrides and mass exclusion.

The feature is **opt-in**: it does nothing unless enabled via `MessageQueue.EnableExternalSystemBindings(...)`. It
also requires an `IEventTypeProvider` (i.e. one of the setup-based registration overloads (2)-(4), not the legacy
assembly-scan one) — `ExternalSystemQueueBindingsInitializer` takes it as a mandatory dependency, so enabling the
feature on the legacy overload fails fast at startup with a standard DI resolution error instead of doing nothing silently.

```C#
options.MessageQueue.EnableExternalSystemBindings(); // binds from the default section "RabbitCap:ExternalSystemBindings"
// or bind from a different section:
options.MessageQueue.EnableExternalSystemBindings("MyApp:ExternalSystemBindings");
```

Pass an empty string explicitly (`EnableExternalSystemBindings("")`) to register the pipeline **without** binding
`ExternalSystemBindingsOptions` from configuration at all — use this if you configure `SystemBindings` entirely in
code (see [Overriding a specific system in code](#overriding-a-specific-system-in-code-with-di)).

```json
{
  "RabbitCap": {
    "ExternalSystemBindings": {
      "ExcludeOutputEvents": ["EXT.Debug*", "EXT.Internal.SomeEvent"],
      "SystemBindings": {
        "crm-queue":       null,
        "billing-queue":   [],
        "analytics-queue": ["EXT.OrderCreated", "EXT.OrderCancelled"]
      }
    }
  }
}
```

- `SystemBindings` — key is the queue name to declare, value is the list of output-event routing keys to bind it to.
- `null` or `[]` → the queue gets the **default set**: every registered output event, except those matching
  `ExcludeOutputEvents` (exact match or `*`-mask, e.g. `"EXT.Debug*"`).
- A non-empty array is an **explicit override** — bound exactly as listed, ignoring `ExcludeOutputEvents` entirely.
  It is not validated against the registered output events (you can list keys that don't exist in `OutputEvents`,
  e.g. ones published from another service into the same exchange).

### Overriding a specific system in code (with DI)

`ExternalSystemBindingsOptions` is its own `IOptions<T>`, so you can layer a code-based, DI-aware override on top of
whatever came from configuration — useful when one system's binding list depends on a service you already registered:

```C#
services.AddOptions<ExternalSystemBindingsOptions>()
    .PostConfigure<IMySubscribersCatalog>((options, catalog) =>
    {
        options.SystemBindings["billing-queue"] = catalog.GetBillingRoutingKeys();
    });
```

`PostConfigure` always runs after configuration-based binding, regardless of registration order, and supports
injecting up to five dependencies via the `PostConfigure<T1..T5>` overloads.

### How the queues get declared

At startup, `RabbitInitializersHostedService` rents a single RabbitMQ channel (the same `IConnectionChannelPool`
`DeadLetterProcessor` uses) and runs every registered `IRabbitInitializer.InitializeAsync(IModel, CancellationToken)` against it —
`ExternalSystemQueueBindingsInitializer` is the one that declares queues from `SystemBindings` and binds them.
Each initializer runs in isolation: if `InitializeAsync` itself throws (e.g. a `QueueDeclare` conflict with an
existing queue), it is logged as an `ERROR` with the initializer's type name, but it does **not** stop the other
initializers or fail application startup. This is different from a missing `IEventTypeProvider` (see above), which
fails during DI graph construction — before any initializer runs — and is not caught by this try/catch. You can
register your own `IRabbitInitializer` implementations the same way
(e.g. `services.AddSingleton<IRabbitInitializer, MyInitializer>()`) for other one-time RabbitMQ topology setup.

## Rabbit event schema export

Enable via `MessageQueue.EnableSchemaExport = true`.

At startup, `RabbitEventSchemaExportInitializer` publishes a single JSON payload to
`MessageQueue.SchemaExportExchangeName` (fanout) / `MessageQueue.SchemaExportQueueName`:

```json
{
  "output": { "...": "json schema object" },
  "input": { "...": "json schema object" },
  "systemName": "string",
  "exchange": "string",
  "queue": "string"
}
```

- `input` is generated from `IEventTypeProvider.InputEvents` as-is.
- `output` is generated from `IEventTypeProvider.OutputEvents`, filtered by
  `ExternalSystemBindingsOptions.ExcludeOutputEvents` using the same wildcard matching rules as
  `ExternalSystemQueueBindingsInitializer`.
- `systemName` uses `MessageQueue.SchemaExportSystemName`, or falls back to the effective consumer queue
  (`MessageQueue.QueueName` if set, otherwise `MessageQueue.ExchangeName`).

## Options

[`IntegrationEventsOptions`](Models/IntegrationEventsOptions.cs):

| Option                         | Description                                                                                                         | Type                             | Default         |
|--------------------------------|---------------------------------------------------------------------------------------------------------------------|----------------------------------|-----------------|
| **DashboardPath**              | Dashboard relative path                                                                                             | string                           | `/admin/events` |
| **GatewayPrefix**              | `PathBase` for the dashboard (when hosted behind a gateway prefix)                                                  | string                           | `""`            |
| **FailedRetryCount**           | Number of message retries                                                                                           | int                              | `5`             |
| **RetentionDays**              | Successful message retention period                                                                                 | int                              | `15`            |
| **SqlServer.ConnectionString** | MS SQL connection string. **When empty, SQL Server storage is not configured**, but you can provide EF.Core storage | string                           | *(empty)*       |
| **SqlServer.Schema**           | Schema for event tables                                                                                             | string                           | `events`        |
| **MessageQueue.Enable**        | Dev only. When `false`, uses the in-memory queue instead of RabbitMQ                                                | bool                             | `true`          |
| **MessageQueue.EnableSchemaExport** | Publishes one startup schema payload (`input`/`output` JSON schemas + `systemName`/`exchange`/`queue`) to RabbitMQ | bool | `false` |
| **MessageQueue.Port**          | RabbitMQ port                                                                                                       | int                              | `5672`          |
| **MessageQueue.QueueName**     | Explicit CAP group / queue name (overrides the exchange-name default)                                               | string                           | *(unset)*       |
| **MessageQueue.SchemaExportExchangeName** | Fanout exchange used for schema export payload publication | string | `events.schema.export` |
| **MessageQueue.SchemaExportQueueName** | Queue declared and bound to `SchemaExportExchangeName` for schema export payloads | string | `events.schema.export` |
| **MessageQueue.SchemaExportSystemName** | `systemName` field in schema payload; when empty, effective queue name is used | string | `""` |
| **MessageQueue.EnableExternalSystemBindings(sectionPath)** | Opts into external system queue bindings; binds `ExternalSystemBindingsOptions` from `sectionPath` (pass `""` to configure purely in code). See [External system queue bindings](#external-system-queue-bindings) | method | not called (feature disabled); `sectionPath` defaults to `"RabbitCap:ExternalSystemBindings"` |
| **UseFailedEventProcessor**    | Register the CAP filter that calls `IFailedEventProcessor` on the last retry                                        | bool                             | `false`         |
| **OverrideCapOptions**         | Escape hatch to mutate `CapOptions` directly (applied last)                                                         | `Action<CapOptions>?`            | `null`          |
| **AuthorizationPredicate**     | Predicate controlling access to the events dashboard                                                                | `Func<HttpContext, Task<bool>>?` | `null`          |

## Breaking changes

1. **`IntegrationEventsOptions.Default` removed.** Defaults are now applied directly on the properties. If you
   referenced `IntegrationEventsOptions.Default`, drop it — a plain `new IntegrationEventsOptions()` is already
   populated.
2. **`IIntegrationEventPublisher` is now `IIntegrationEventPublisher<IIntegrationEvent>`.** The non-generic
   interface still exists (as a derived marker), so injecting `IIntegrationEventPublisher` keeps working. Publishing
   custom base types uses `IIntegrationEventPublisher<T>`.
3. **`IIntegrationEventProcessor` is now `IIntegrationEventProcessor<IIntegrationEvent>`.** Existing non-generic
   implementations still compile.
4. **`IntegrationEventPublisher` renamed** to `IntegrationEventPublisherLegacy` (+ new `IntegrationEventPublisherNew<T>`
   and `IntegrationEventPublisherBase<T>`). Breaking only if you referenced the concrete class.
5. **`CapConsumerServiceSelector` renamed & moved.** It is now `CapConsumerServiceSelectorLegacy` /
   `CapConsumerServiceSelectorNew` in the `Bss.Platform.Events.Internal` namespace. Breaking only if you referenced it.
6. **Queue name no longer gets `.v1`** with the new (multi-generic) overloads — see the next section. This changes
   the RabbitMQ queue a service binds to, so it is a runtime/behavioural breaking change even though it compiles.
7. **`IEventTypeProvider` moved** to `Bss.Platform.Events.Abstractions` (it was briefly under
   `Bss.Platform.Events.Interfaces`).
8. **New dependency:** the package now references `Bss.Platform.Mediation.Abstractions`
   (`IIntegrationEvent : INotification`).
9. **`LangVersion` raised to `14`** in `Directory.Build.props`.

## Switching to the new registration (queue name & `.v1`)

> **Read this before moving from the legacy overload to the new setup-based overloads.**

Historically the CAP consumer group / RabbitMQ queue name was `"{ExchangeName}.v1"`. To preserve that,
the **legacy** overload automatically appends `.v1` to the queue name (via `SetLegacyQueueNameWithVersion`):

- **Legacy overload (assembly scan):** queue name = `MessageQueue.QueueName` if set, otherwise `"{ExchangeName}.v1"`.
- **New overloads (custom / input-output generics):** queue name = `MessageQueue.QueueName` if set, otherwise
  `"{ExchangeName}"` — **no `.v1` suffix**.

> Note: currently the `.v1` behaviour is applied by two overloads (the legacy assembly-scan one **and** the
> setup-based `IIntegrationEvent` overload). This is being consolidated so that only the **legacy** overload — the
> one that does **not** take a `setupEvents` action — keeps the `.v1` behaviour, purely for drop-in upgrade
> compatibility.

### If you want to keep the old queue name

When you move to a new overload but need to keep binding to the existing `"{ExchangeName}.v1"` queue, set the queue
name explicitly:

```C#
services.AddPlatformIntegrationEvents<MyProcessor, IMyEvent>(
    events => events.AddInputEvents<IMyEvent>(),
    options =>
    {
        options.MessageQueue.ExchangeName = "integration.events";
        // keep the historical queue name so we don't create/bind a new queue:
        options.MessageQueue.QueueName = $"{options.MessageQueue.ExchangeName}.v1";
        // ...
    });
```

If you intentionally want a fresh queue (new naming), simply leave `QueueName` unset and the queue will be
`"{ExchangeName}"`.

## Other notes / things to review

- `IIntegrationEventSetup.AddOutputEvents` has no default value for `prefix` while `AddInputEvents` does
  (`prefix = ""`) — a minor API inconsistency.
- In `EventTypeProvider`, both `AddInputEvents` and `AddOutputEvents` deduplicate with
  `.Except(this.outputTypes.Keys)`. For output registration this excludes types against *itself* (not against
  input types), so a type registered as both input and output is allowed — which the publisher relies on (it emits
  to both routing keys when they differ). Worth a quick confirmation that this is intended.
- `SqlServer.ConnectionString` empty now silently means "no SQL Server storage" — make sure services that expect
  the outbox actually set it.
