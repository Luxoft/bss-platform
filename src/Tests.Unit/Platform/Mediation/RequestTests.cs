using Bss.Platform.Mediation;
using Bss.Platform.Mediation.Abstractions;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Tests.Unit.Platform.Mediation;

public class RequestTests
{
    private readonly ServiceCollection services = [];

    private readonly List<string> executionLog = [];

    public RequestTests()
    {
        this.services.AddSingleton(this.executionLog);
        this.services.AddTransient<IMediator, Mediator>();
    }

    public record PingRequest(string Name) : IRequest<string>;

    public class PingHandler(List<string> log) : IRequestHandler<PingRequest, string>
    {
        public Task<string> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            log.Add("Handler");
            return Task.FromResult($"Hello {request.Name}");
        }
    }

    public class LoggingBehavior<TRequest, TResult>(List<string> log) : IPipelineBehavior<TRequest, TResult>
    {
        public async Task<TResult> Handle(TRequest request, RequestHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            log.Add("Behavior Pre");
            var result = await next();
            log.Add("Behavior Post");
            return result;
        }
    }

    public record VoidRequest : IRequest;

    public class VoidHandler(List<string> log) : IRequestHandler<VoidRequest>
    {
        public Task Handle(VoidRequest request, CancellationToken cancellationToken)
        {
            log.Add("VoidHandler");
            return Task.CompletedTask;
        }
    }

    public class LoggingVoidBehavior<TRequest>(List<string> log) : IPipelineBehavior<TRequest>
    {
        public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
        {
            log.Add("VoidBehavior Pre");
            await next();
            log.Add("VoidBehavior Post");
        }
    }

    [Fact]
    public async Task Send_RequestWithResult_ExecutesBehaviorsAndHandler()
    {
        // Arrange
        this.services.AddTransient<IPipelineBehavior<PingRequest, string>, LoggingBehavior<PingRequest, string>>();
        this.services.AddTransient<IRequestHandler<PingRequest, string>, PingHandler>();

        var provider = this.services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new PingRequest("World"), CancellationToken.None);

        // Assert
        result.Should().Be("Hello World");
        this.executionLog.Should()
            .ContainInOrder(
                "Behavior Pre",
                "Handler",
                "Behavior Post"
            );
    }

    [Fact]
    public async Task Send_VoidRequest_ExecutesBehaviorsAndHandler()
    {
        // Arrange
        this.services.AddTransient<IPipelineBehavior<VoidRequest>, LoggingVoidBehavior<VoidRequest>>();
        this.services.AddTransient<IRequestHandler<VoidRequest>, VoidHandler>();

        var provider = this.services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new VoidRequest(), CancellationToken.None);

        // Assert
        this.executionLog.Should()
            .ContainInOrder(
                "VoidBehavior Pre",
                "VoidHandler",
                "VoidBehavior Post"
            );
    }
}
