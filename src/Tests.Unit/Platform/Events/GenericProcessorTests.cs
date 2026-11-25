using System.Reflection;

using Bss.Platform.Events;
using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;

using DotNetCore.CAP;
using DotNetCore.CAP.Internal;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace Tests.Unit.Platform.Events;

public class GenericProcessorTests
{
    public interface IOwnEvent;
    public record OwnEvent(string Value) : IOwnEvent;
    public record LegacyEvent : IIntegrationEvent;
    public class OwnProcessor : IIntegrationEventProcessor<IOwnEvent>
    {
        public IOwnEvent? Received { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task ProcessAsync(IOwnEvent @event, CancellationToken token)
        {
            this.Received = @event;
            this.Token = token;
            return Task.CompletedTask;
        }
    }

    public class LegacyProcessor : IIntegrationEventProcessor
    {
        public Task ProcessAsync(IIntegrationEvent @event, CancellationToken token) => Task.CompletedTask;
    }

    [Fact]
    public async Task Custom_base_type_is_discovered_and_processed_without_mediation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformIntegrationEvents<OwnProcessor, IOwnEvent>(typeof(OwnEvent).Assembly);
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<IConsumerServiceSelector>();
        var method = typeof(CapConsumerServiceSelector).GetMethod("FindConsumersFromInterfaceTypes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var descriptors = ((IEnumerable<ConsumerExecutorDescriptor>)method.Invoke(selector, [provider])!).ToArray();
        var descriptor = Assert.Single(descriptors);
        Assert.Equal(nameof(OwnEvent), descriptor.Attribute.Name);
        var executor = ActivatorUtilities.CreateInstance(provider, descriptor.ImplTypeInfo.AsType());
        var message = new OwnEvent("test");
        using var cancellation = new CancellationTokenSource();
        await (Task)descriptor.MethodInfo.Invoke(executor, [message, cancellation.Token])!;
        var processor = Assert.IsType<OwnProcessor>(provider.GetRequiredService<IIntegrationEventProcessor<IOwnEvent>>());
        Assert.Same(message, processor.Received);
        Assert.Equal(cancellation.Token, processor.Token);
        Assert.Null(provider.GetService<IIntegrationEventProcessor>());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Namespace?.StartsWith("Bss.Platform.RabbitMq", StringComparison.Ordinal) == true);
        var rabbit = provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
        Assert.Equal("quorum", rabbit.QueueArguments.QueueType);
    }

    [Fact]
    public void Legacy_registration_still_discovers_only_integration_events()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformIntegrationEvents<LegacyProcessor>(typeof(LegacyEvent).Assembly);
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<IConsumerServiceSelector>();
        var method = typeof(CapConsumerServiceSelector).GetMethod("FindConsumersFromInterfaceTypes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var descriptors = ((IEnumerable<ConsumerExecutorDescriptor>)method.Invoke(selector, [provider])!).ToArray();
        Assert.Equal(nameof(LegacyEvent), Assert.Single(descriptors).Attribute.Name);
        Assert.IsType<LegacyProcessor>(provider.GetRequiredService<IIntegrationEventProcessor>());
    }
}
