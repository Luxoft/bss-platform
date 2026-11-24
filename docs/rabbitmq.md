# RabbitMQ initialization and schema publication

## Behavior after upgrading

| Registration | Automatic startup work |
| --- | --- |
| Client only, no contracts or system bindings | No new connection or topology work |
| Typed consumer (builder/attributes) | Input topology and input-schema publication |
| Low-level `IRabbitMqMessageProcessor` consumer | Input topology; no inferred input schemas |
| Client with output contracts | Output-schema publication and configured destination queues/bindings |
| CAP/Events | No new topology or schema publication; optional generic processor API only |

Existing registrations remain available. Initializers run sequentially before consuming.
They use a temporary startup connection and separate channels. Existing raw publishing is unchanged;
the new `IRabbitMqEventPublisher` checks declared types when explicitly used.
All platform-owned queues default to durable quorum: input, output, optional dead-letter and
documenter queues. An existing classic queue requires a matching custom queue policy or a separate
migration; RabbitMQ cannot redeclare it as quorum.

## Producer-only application

Use the `Luxoft.Bss.Platform.RabbitMq` package:

```csharp
using Bss.Platform.RabbitMq;

services.AddPlatformRabbitMqClient(configuration)
    .RegisterRabbitMqOutputEvents(events => events
        .AddOutputEvent<OrderCreated>("Orders.Created")
        .AddOutputEvent<OrderCancelled>("Orders.Cancelled"));

// Bulk registration is also supported:
// services.RegisterRabbitMqOutputEvents(events =>
//     events.AddOutputEvents<IOrderEvent>("Orders.", typeof(IOrderEvent).Assembly));
```

The catalog describes declared contracts; it does not validate or restrict your existing
`BasicPublish` calls. `IRabbitMqEventPublisher` enforces the catalog for its own calls. Bulk registration finds concrete,
non-nested types assignable to the given base type. Explicit mappings are supported too.

Run under the normal .NET hosted-service lifecycle. Raw publishing that depends on platform
topology should await `RabbitMqInitializationService.EnsureInitializedAsync(token)` when
initialization runs in the background or when there is no host. `IRabbitMqEventPublisher`
ensures initialization before publication.

```json
{
  "RabbitMQ": {
    "Server": {
      "Host": "localhost", "Port": 5672, "VirtualHost": "/",
      "UserName": "guest", "Secret": "guest"
    },
    "Exchange": "orders",
    "ConnectionAttemptCount": 3,
    "Startup": { "ThrowOnFailure": false },
    "Producer": {
      "PublishConfirmationTimeout": "00:00:05",
      "DefaultExcludeOutputBindings": ["Orders.Internal*"],
      "SystemBindings": {
        "ToBilling": null,
        "ToAnalytics": [],
        "ToCrm": ["Orders.Created"]
      }
    },
    "Schemas": {
      "Enabled": true,
      "ThrowOnFailure": false,
      "SystemName": "Orders",
      "QueueName": "ToDocumenter",
      "RoutingKey": "RabbitEventSchemas",
      "ConfirmationTimeout": "00:00:05"
    }
  }
}
```

`RabbitMQ:ConnectionAttemptCount` applies to initialization, consumption, event publishing and
dead-letter publishing. It counts retries after the initial attempt, five seconds apart. `0` allows
only the initial attempt. If the common value is null, the effective `Consumer:ConnectionAttemptCount`
supplies the fallback. If both are null, connections retry until cancelled.
The common `Exchange` falls back to the effective `Consumer:Exchange`, including programmatic
consumer overrides. Conflicting values fail configuration validation. Multiple exchanges are
outside scope; custom behavior can be supplied through options/services.

## Starting without RabbitMQ

With `RabbitMQ:Startup:ThrowOnFailure` set to `false` (the default), the host starts while
RabbitMQ initializes in the background. The consumer waits for topology before reading.
If the connection retry limit is exhausted, the startup service logs the failure and the
consumer remains stopped until restart. With `true`, the host waits for initialization and
fails startup if the retry limit is exhausted. Set a finite `ConnectionAttemptCount` or a
host startup timeout to bound that wait.

Configuration validation still runs at startup. Schema publication retains its own
`RabbitMQ:Schemas:ThrowOnFailure` setting. Broker errors during topology declaration and
other initializer errors propagate to the host under its background-service error policy
when startup runs in the background. Explicit publication reports initialization errors to
the caller in both modes.

Concurrent callers share the current initialization attempt and can cancel their own wait.
The attempt continues until it succeeds, fails or the application stops. Successful initialization
is reused. A later explicit initialization or publish call can retry failed topology setup.
Custom initializers should tolerate being rerun after partial success. A subsequent attempt does
not restart a stopped consumer.

## Checked event publisher

`AddPlatformRabbitMqClient` registers `IRabbitMqEventPublisher` by default. Inject it to publish
declared events using the catalog routing key:

```csharp
using Bss.Platform.RabbitMq.Interfaces;

await provider.GetRequiredService<IRabbitMqEventPublisher>()
    .PublishAsync(new OrderCreated(/* values */), cancellationToken);
```

Resolution uses the exact runtime type, including when the variable is declared as a base type.
The publisher builds an immutable type-to-routing-keys index once at construction, after contract
registration. Lookups do not enumerate the catalog or allocate routing arrays per publication.
Runtime mutation of the catalog is not reflected in an existing publisher instance.
An unregistered subtype is not authorized by a base-type registration. Unregistered types throw
`InvalidOperationException` before initialization or broker operations. If a type has several
registered aliases, publication throws rather than choosing a route or sending multiple messages.

The default implementation is `RabbitMqEventPublisher`. Override its fallback to support other
types without changing registration or schemas:

```csharp
using Bss.Platform.RabbitMq;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Settings;
using Microsoft.Extensions.Options;

public class NameBasedPublisher(
    IOutputEventCatalog catalog,
    IRabbitMqClient client,
    RabbitMqInitializationService initialization,
    IOptions<RabbitMqSettings> options,
    IOptions<RabbitMqProducerSettings> producerOptions)
    : RabbitMqEventPublisher(catalog, client, initialization, options, producerOptions)
{
    protected override string ResolveUnregisteredRoutingKey(Type eventType) => eventType.Name;
}

// Register before AddPlatformRabbitMqClient, or remove the default registration first:
services.AddSingleton<IRabbitMqEventPublisher, NameBasedPublisher>();
```

`ResolveRoutingKey` is also virtual for explicit alias selection, and `Serialize` can customize
the payload. Fallback routing does not automatically declare contracts, bindings or schemas.

The publisher serializes the runtime object as JSON, sends persistent messages and waits for
publisher confirmation using `Producer:PublishConfirmationTimeout` (default five seconds).
Mandatory returns, connection failures and confirmation failures propagate to the caller; schema
`ThrowOnFailure` has no effect on business publication. Failed/uncertain publishes are not retried
automatically. Recipient queues must be configured or otherwise provided for the chosen route.

The singleton publisher reuses its connection and opens a dedicated channel per publication,
allowing concurrent calls. DI disposal closes the retained connection. CAP's publisher is unchanged.

## System selection

Queue keys are literal names: there is no automatic `To` prefix.

- Null/empty routing lists bind all declared output routes except `DefaultExcludeOutputBindings`.
  Exclusions support exact strings and `*` patterns, case-insensitively.
- Nonempty lists bind exactly those routes, bypassing exclusions. Unknown routing keys are allowed
  for bindings, but need a registered type to appear in schemas.
- Omit `SystemBindings`: no output queues, all declared output contracts in schemas.
- Explicit `SystemBindings: {}` or `SystemBindings: null`: no systems, empty output schema.
- With systems configured, schemas contain the union of declared output contracts selected by at
  least one system. Input schemas are unaffected.
- No obsolete queues/bindings are deleted.

The binder preserves null and empty values. In programmatic `RabbitMqProducerSettings` (namespace
`Bss.Platform.RabbitMq.Settings`), null `SystemBindings` means no selection; `new()` explicitly
selects no systems. `Configure` and `PostConfigure` can override configuration values.

## Consumer and dead letters

Builder/attribute registration supplies input types automatically. Raw consumers can provide
`IRabbitSchemaTypeProvider`. Custom schema providers are authoritative and bypass default system
filtering; they do not change business bindings. To alter those, replace `IOutputBindingsResolver`.
`HasContracts` can be overridden to announce intentionally empty schemas. The default provider
retains this signal when system filtering removes all declared output contracts.

Add `DeadLetterQueue` alongside existing `DeadLetterExchange` in `RabbitMQ:Consumer`:

```json
{
  "RabbitMQ": {
    "Consumer": {
      "Exchange": "orders", "Queue": "ToOrders",
      "DeadLetterExchange": "deadletters", "DeadLetterQueue": "FailedOrders"
    }
  }
}
```

Blank/null `DeadLetterQueue` means no error queue/binding is created. The existing fanout exchange
and consumer failure processing remain: manual republish after retries, ACK on successful publish,
requeue on publication failure. No broker DLX arguments or CAP dead-letter changes are added.

## Schema format and errors

The payload retains `input`, `output`, `systemName`, `exchange`, `queue`, and adds `schemaHash`.
Producer-only messages have `queue: null`; consumers supply their queue name.
`RabbitSchemaOptions.SourceQueueName` can override this metadata.

The SHA-256 hash uses normalized generated input/output schemas, with object properties sorted
ordinally. Metadata, assembly identity/version and schema-ignored members do not affect it.
Registration order is stable; aliases and recursive local references remain valid. The hash enables
duplicate detection; it does not establish version ordering or prevent stale updates.

The sender declares the exchange, destination queue and binding, enables publisher confirms,
publishes persistent JSON and waits for confirmation. Schema generation, connection and publication
errors are logged/suppressed by default; `ThrowOnFailure = true` propagates them. Cancellation and
invalid configuration are not suppressed. Required business topology failures stop startup.

The existing HTTP middleware and `ProducedEventTypes` API remain, using shared schema generation.

## Independent RabbitMQ application

Install only `Luxoft.Bss.Platform.RabbitMq.Schemas`. It depends on the lightweight `Base` package,
RabbitMQ.Client and schema/DI libraries, with no platform client/consumer, CAP or Events dependency.

```csharp
using Bss.Platform.RabbitMq.Schemas;

services.AddRabbitMqSchemas(options => options.SystemName = "MyService");
services.AddSingleton<IRabbitSchemaTypeProvider, MyContractProvider>();

await provider.GetRequiredService<IRabbitSchemaSender>().SendAsync(
    myChannel,
    provider.GetRequiredService<IRabbitSchemaTypeProvider>(),
    new RabbitSchemaSource("orders"),
    cancellationToken);
```

The provider supplies `InputEvents`/`OutputEvents` dictionaries keyed by routing key with CLR types
as values. No host is registered. Use a dedicated channel: the sender enables publisher confirms.
The caller retains channel ownership. The async-channel-factory overload creates/disposes the
channel and includes connection failures in the sender's error policy. No business output queues
are created by the standalone sender.

## Replacing defaults

Values use options; algorithms use DI. Defaults use `TryAdd`, so custom services can precede the
platform registration. For later replacements use standard DI removal/registration:

```csharp
using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Schemas;
using Bss.Platform.RabbitMq.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

services.Configure<RabbitSchemaOptions>(x => x.Enabled = false);
services.PostConfigure<RabbitSchemaOptions>(x => x.QueueName = "MyDocumenter");
services.RemoveAll<IQueueDeclarationPolicy>();
services.AddSingleton<IQueueDeclarationPolicy, MyQueuePolicy>();
services.RemoveAll<IRabbitSchemaSender>();
services.AddSingleton<IRabbitSchemaSender, MySender>();
services.RemoveRabbitMqInitializer<OutputQueuesInitializer>();
services.RemoveRabbitMqInitializer<ExportSchemasInitializer>();
```

Sender replacement through DI is the primary extension mechanism; its virtual destination
property is optional convenience. Replacing the queue policy affects all four platform queue sites.
Removing the output initializer prevents provisioning without changing schema-selection settings.
Schema disabling also prevents preparation of the documenter destination.

`ConsumerTopologyInitializer` adapts existing `IRabbitMqInitializer` implementations. Register new
common work as `IRabbitInitializer` by implementation type for selective removal. `Order` defaults
to 0; platform defaults are consumer topology 0, outputs 100 and schemas 1000.

## Custom CAP processor

```csharp
services.AddPlatformIntegrationEvents<MyProcessor, IOwnEvent>(typeof(IOwnEvent).Assembly, setup);
```

Implement `IIntegrationEventProcessor<IOwnEvent>` with your own base type, without mediation.
The legacy registration remains. Exported concrete events retain topics based on type names and
existing CAP grouping. Native envelopes, serializer, QoS, quorum settings and publisher behavior
remain unchanged. No arbitrary raw-message ingestion, synthesized headers, additional failure
handling, schema export or output bindings are registered in CAP.

## Checks

Run `dotnet test src/Tests.Unit/Tests.Unit.csproj`. Tests cover compatibility, startup and declaration
order, optional dead-letter queues, output selection, actual JSON null/empty binding behavior,
DI overrides/removal, producer-only startup, cancellation/errors and content-hash stability across
assembly versions and ignored fields. They use recorded channel/connection doubles, not a live broker.
