using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Serialization;

using Bss.Platform.RabbitMq;
using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Consumer;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Schemas;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using Xunit;

namespace Tests.Unit.Platform.RabbitMq;

public class SchemaTests
{
    public record NestedEvent(Child Payload);
    public record Child(string Name, Child? Next);
    public record Event(string Name);
    public record OtherEvent(int Value);

    public sealed class Types(IReadOnlyDictionary<string, Type>? input = null, IReadOnlyDictionary<string, Type>? output = null) : IRabbitSchemaTypeProvider
    {
        public IReadOnlyDictionary<string, Type> InputEvents { get; } = input ?? new Dictionary<string, Type>();
        public IReadOnlyDictionary<string, Type> OutputEvents { get; } = output ?? new Dictionary<string, Type>();
    }

    public class Processor : IRabbitMqEventProcessor<Event>
    {
        public Task ProcessAsync(Event @event, CancellationToken token) => Task.CompletedTask;
    }

    private static RabbitSchemaMessage Build(Types types, string exchange = "events", string system = "system") =>
        new RabbitSchemaMessageBuilder(new RabbitEventsSchemaGenerator()).Build(types, new RabbitSchemaSource(exchange), system);

    [Fact]
    public void Hash_ignores_metadata_and_contract_registration_order()
    {
        var first = Build(new Types(output: new Dictionary<string, Type> { ["B"] = typeof(OtherEvent), ["A"] = typeof(Event) }));
        var second = Build(new Types(output: new Dictionary<string, Type> { ["A"] = typeof(Event), ["B"] = typeof(OtherEvent) }), "other-exchange", "other-system");
        Assert.Equal(first.SchemaHash, second.SchemaHash);
        Assert.Equal(first.Output.GetRawText(), second.Output.GetRawText());
        Assert.Equal(64, first.SchemaHash.Length);
        var changed = Build(new Types(output: new Dictionary<string, Type> { ["A"] = typeof(OtherEvent), ["B"] = typeof(OtherEvent) }));
        Assert.NotEqual(first.SchemaHash, changed.SchemaHash);
    }

    [Fact]
    public void Assembly_version_and_ignored_members_do_not_change_hash_but_visible_members_do()
    {
        var original = DynamicEvent(1, false, false);
        var changedAssemblyAndIgnoredField = DynamicEvent(2, true, false);
        var changedSchema = DynamicEvent(3, false, true);
        var first = Build(new Types(output: new Dictionary<string, Type> { ["Event"] = original }));
        var ignored = Build(new Types(output: new Dictionary<string, Type> { ["Event"] = changedAssemblyAndIgnoredField }));
        var visible = Build(new Types(output: new Dictionary<string, Type> { ["Event"] = changedSchema }));
        Assert.Equal(first.SchemaHash, ignored.SchemaHash);
        Assert.True(first.SchemaHash != visible.SchemaHash, $"original={first.Output}; visible={visible.Output}; properties={string.Join(',', changedSchema.GetProperties().Select(x => x.Name))}");
    }

    [Fact]
    public void Aliases_same_named_types_and_recursive_references_are_preserved()
    {
        var first = DynamicEvent(1, false, false);
        var second = DynamicEvent(2, false, true);
        var message = Build(new Types(output: new Dictionary<string, Type>
        {
            ["First"] = first, ["Alias"] = first, ["Second"] = second, ["Nested"] = typeof(NestedEvent)
        }));
        var definitions = message.Output.GetProperty("definitions");
        Assert.True(definitions.TryGetProperty("First", out _));
        Assert.True(definitions.TryGetProperty("Alias", out _));
        Assert.True(definitions.TryGetProperty("Second", out var other));
        Assert.True(other.GetRawText().Contains("Extra"), message.Output.GetRawText());
        // Every local reference must resolve in the published document.
        ValidateReferences(message.Output, message.Output);
    }

    [Fact]
    public async Task Standalone_sender_prepares_destination_and_confirms_publication_without_platform_client()
    {
        var services = new ServiceCollection();
        services.AddRabbitMqSchemas(x => { x.QueueName = "Docs"; x.RoutingKey = "Schemas"; x.SystemName = "App"; x.ThrowOnFailure = true; });
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IRabbitMqClient>());
        var (channel, proxy) = RecordingProxy<IModel>.Create();
        var (properties, _) = RecordingProxy<IBasicProperties>.Create();
        proxy.Handler = (method, _) => method.Name == nameof(IModel.CreateBasicProperties) ? properties : null;
        await provider.GetRequiredService<IRabbitSchemaSender>().SendAsync(channel, new Types(output: new Dictionary<string, Type> { ["Event"] = typeof(Event) }), new RabbitSchemaSource("events"));
        var names = proxy.Calls.Select(x => x.Method).ToArray();
        Assert.True(Array.IndexOf(names, nameof(IModel.ExchangeDeclare)) < Array.IndexOf(names, nameof(IModel.QueueBind)));
        Assert.Contains(proxy.Calls, x => x.Method == nameof(IModel.QueueDeclare) && Equals(x.Arguments[0], "Docs"));
        var publish = Assert.Single(proxy.Calls, x => x.Method == nameof(IModel.BasicPublish));
        Assert.Equal("events", publish.Arguments[0]);
        Assert.Equal("Schemas", publish.Arguments[1]);
        using var body = JsonDocument.Parse((ReadOnlyMemory<byte>)publish.Arguments[4]!);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("queue").ValueKind);
        Assert.Equal("App", body.RootElement.GetProperty("systemName").GetString());
        Assert.Contains(proxy.Calls, x => x.Method == nameof(IModel.ConfirmSelect));
        Assert.Contains(proxy.Calls, x => x.Method == nameof(IModel.WaitForConfirmsOrDie));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Schema_connection_errors_follow_sender_failure_policy(bool throwOnFailure)
    {
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["RabbitMQ:Exchange"] = "events" }).Build());
        services.RegisterRabbitMqOutputEvents(x => x.AddOutputEvent<Event>("Event"));
        services.AddSingleton<IRabbitMqClient>(new InitializationTests.FakeClient(null));
        services.Configure<RabbitMqSettings>(x => x.ConnectionAttemptCount = 0);
        services.Configure<RabbitSchemaOptions>(x => x.ThrowOnFailure = throwOnFailure);
        using var provider = services.BuildServiceProvider();
        var task = provider.GetRequiredService<RabbitMqInitializationService>().StartAsync(default);
        if (throwOnFailure) await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        else await task;
    }

    [Fact]
    public async Task Sender_cancellation_is_not_suppressed()
    {
        var services = new ServiceCollection();
        services.AddRabbitMqSchemas();
        using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetRequiredService<IRabbitSchemaSender>().SendAsync(
            _ => throw new OperationCanceledException(), new Types(), new RabbitSchemaSource("events")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publication_errors_follow_failure_policy(bool throwOnFailure)
    {
        var services = new ServiceCollection();
        services.AddRabbitMqSchemas(x => x.ThrowOnFailure = throwOnFailure);
        using var provider = services.BuildServiceProvider();
        var (channel, proxy) = RecordingProxy<IModel>.Create();
        var (properties, _) = RecordingProxy<IBasicProperties>.Create();
        proxy.Handler = (method, _) => method.Name switch
        {
            nameof(IModel.CreateBasicProperties) => properties,
            nameof(IModel.BasicPublish) => throw new IOException("publication failed"),
            _ => null
        };
        var send = provider.GetRequiredService<IRabbitSchemaSender>().SendAsync(channel, new Types(), new RabbitSchemaSource("events"));
        if (throwOnFailure) await Assert.ThrowsAsync<IOException>(() => send);
        else await send;
        Assert.DoesNotContain(proxy.Calls, x => x.Method == nameof(IModel.WaitForConfirmsOrDie));
    }

    [Fact]
    public async Task Invalid_schema_options_are_not_suppressed()
    {
        var services = new ServiceCollection();
        services.AddRabbitMqSchemas(x => x.QueueName = "");
        using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAsync<OptionsValidationException>(() => provider.GetRequiredService<IRabbitSchemaSender>().SendAsync(
            _ => throw new Exception("must not connect"), new Types(), new RabbitSchemaSource("events")));
    }

    [Fact]
    public async Task Disabled_schema_sender_does_not_open_channel()
    {
        var services = new ServiceCollection();
        services.AddRabbitMqSchemas(x => x.Enabled = false);
        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IRabbitSchemaSender>().SendAsync(_ => throw new Exception("must not connect"), new Types(), new RabbitSchemaSource("events"));
    }

    [Fact]
    public void Consumer_input_types_are_automatic_and_custom_schema_provider_bypasses_system_selection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddPlatformRabbitMqClient(configuration);
        services.AddPlatformRabbitMqConsumerWithMessages<Processor, Event>(configuration, x => x.Add<Event>("Incoming"));
        services.Configure<RabbitMqConsumerSettings>(x => { x.Exchange = "events"; x.Queue = "ToApp"; });
        services.RegisterRabbitMqOutputEvents(x => x.AddOutputEvent<OtherEvent>("Outgoing"));
        services.Configure<RabbitMqProducerSettings>(x => x.SystemBindings = []);
        using (var provider = services.BuildServiceProvider())
        {
            var types = provider.GetRequiredService<IRabbitSchemaTypeProvider>();
            Assert.Equal(typeof(Event), Assert.Single(types.InputEvents).Value);
            Assert.Empty(types.OutputEvents);
            Assert.True(types.HasContracts);
        }
        var custom = new Types(output: new Dictionary<string, Type> { ["Custom"] = typeof(Event) });
        services.AddSingleton<IRabbitSchemaTypeProvider>(custom);
        using var overridden = services.BuildServiceProvider();
        Assert.Same(custom, overridden.GetRequiredService<IRabbitSchemaTypeProvider>());
        Assert.Empty(overridden.GetRequiredService<IOutputBindingsResolver>().ResolveQueueBindings());
    }

    [Fact]
    public async Task Producer_only_startup_provisions_outputs_and_publishes_schema_once()
    {
        var (channel, channelProxy) = RecordingProxy<IModel>.Create();
        var (properties, _) = RecordingProxy<IBasicProperties>.Create();
        channelProxy.Handler = (method, _) => method.Name == nameof(IModel.CreateBasicProperties) ? properties : null;
        var (connection, connectionProxy) = RecordingProxy<IConnection>.Create();
        connectionProxy.Handler = (method, _) => method.Name == nameof(IConnection.CreateModel) ? channel : null;
        var client = new InitializationTests.FakeClient(connection);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Exchange"] = "events",
            ["RabbitMQ:Producer:SystemBindings:ToBilling"] = null,
            ["RabbitMQ:Schemas:ThrowOnFailure"] = "true"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IRabbitMqClient>(client);
        services.AddPlatformRabbitMqClient(configuration).RegisterRabbitMqOutputEvents(x => x.AddOutputEvent<Event>("Outgoing"));
        using var provider = services.BuildServiceProvider();
        var initialization = provider.GetRequiredService<RabbitMqInitializationService>();
        await initialization.StartAsync(default);
        await initialization.EnsureInitializedAsync(default);
        Assert.Equal(1, client.Connections);
        Assert.Null(provider.GetService<IRabbitMqConsumer>());
        var queues = channelProxy.Calls.Where(x => x.Method == nameof(IModel.QueueDeclare)).ToArray();
        Assert.Equal(new[] { "ToBilling", "ToDocumenter" }, queues.Select(x => (string)x.Arguments[0]!).ToArray());
        var publication = Assert.Single(channelProxy.Calls, x => x.Method == nameof(IModel.BasicPublish));
        using var json = JsonDocument.Parse((ReadOnlyMemory<byte>)publication.Arguments[4]!);
        Assert.True(json.RootElement.GetProperty("output").GetProperty("definitions").TryGetProperty("Outgoing", out _));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("queue").ValueKind);
    }

    [Fact]
    public void Custom_queue_policy_and_sender_registered_before_platform_are_retained()
    {
        var services = new ServiceCollection();
        var policy = new ClassicQueues();
        var sender = new NoOpSender();
        services.AddSingleton<IQueueDeclarationPolicy>(policy);
        services.AddSingleton<IRabbitSchemaSender>(sender);
        services.AddPlatformRabbitMqClient(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.Same(policy, provider.GetRequiredService<IQueueDeclarationPolicy>());
        Assert.Same(sender, provider.GetRequiredService<IRabbitSchemaSender>());
    }

    [Fact]
    public async Task Removing_output_initializer_does_not_change_schema_selection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Exchange"] = "events", ["RabbitMQ:Producer:SystemBindings:ToBilling:0"] = "B"
        }).Build();
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(configuration).RegisterRabbitMqOutputEvents(x => x.AddOutputEvent<Event>("A").AddOutputEvent<OtherEvent>("B"));
        services.RemoveRabbitMqInitializer<OutputQueuesInitializer>();
        services.RemoveRabbitMqInitializer<ExportSchemasInitializer>();
        var client = new InitializationTests.FakeClient(null);
        services.AddSingleton<IRabbitMqClient>(client);
        using var provider = services.BuildServiceProvider();
        Assert.Equal("B", Assert.Single(provider.GetRequiredService<IRabbitSchemaTypeProvider>().OutputEvents).Key);
        await provider.GetRequiredService<RabbitMqInitializationService>().StartAsync(default);
        Assert.Equal(0, client.Connections);
    }

    private sealed class ClassicQueues : IQueueDeclarationPolicy
    {
        public void Declare(IModel channel, string queue) => channel.QueueDeclare(queue, true, false, false, null);
    }

    private sealed class NoOpSender : IRabbitSchemaSender
    {
        public Task SendAsync(IModel channel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static Type DynamicEvent(int version, bool ignored, bool visible)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"Contract{Guid.NewGuid():N}") { Version = new Version(version, 0, 0, 0) }, AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Main").DefineType("Contracts.VersionedEvent", TypeAttributes.Public);
        AddProperty(type, "Name", false);
        if (ignored || visible) AddProperty(type, "Extra", ignored);
        return type.CreateType()!;
    }

    private static void AddProperty(TypeBuilder type, string name, bool ignored)
    {
        var field = type.DefineField($"_{name}", typeof(string), FieldAttributes.Private);
        var property = type.DefineProperty(name, PropertyAttributes.None, typeof(string), null);
        var getter = type.DefineMethod($"get_{name}", MethodAttributes.Public | MethodAttributes.SpecialName, typeof(string), Type.EmptyTypes);
        var il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
        property.SetGetMethod(getter);
        var setter = type.DefineMethod($"set_{name}", MethodAttributes.Public | MethodAttributes.SpecialName, typeof(void), [typeof(string)]);
        var setterIl = setter.GetILGenerator();
        setterIl.Emit(OpCodes.Ldarg_0); setterIl.Emit(OpCodes.Ldarg_1); setterIl.Emit(OpCodes.Stfld, field); setterIl.Emit(OpCodes.Ret);
        property.SetSetMethod(setter);
        if (ignored) property.SetCustomAttribute(new CustomAttributeBuilder(typeof(JsonIgnoreAttribute).GetConstructor(Type.EmptyTypes)!, []));
    }

    private static void ValidateReferences(JsonElement node, JsonElement root)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in node.EnumerateObject())
            {
                if (property.Name == "$ref")
                {
                    var pointer = property.Value.GetString()!;
                    Assert.StartsWith("#/", pointer);
                    var target = root;
                    foreach (var part in pointer[2..].Split('/')) target = target.GetProperty(part.Replace("~1", "/").Replace("~0", "~"));
                }
                else ValidateReferences(property.Value, root);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) ValidateReferences(item, root);
    }
}
