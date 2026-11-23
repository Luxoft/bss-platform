using System.Text.Json;

using Bss.Platform.Events.Abstractions;

using DotNetCore.CAP;
using DotNetCore.CAP.Filter;
using DotNetCore.CAP.Serialization;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Events.Internal;

internal sealed class CapExceptionFilter<TInputEvent>(
    IOptions<CapOptions> capOptions,
    IEnumerable<IFailedEventProcessor<TInputEvent>> failsProcessors,
    ISerializer serializer)
    : SubscribeFilter where TInputEvent : class
{
    private const string ErrorDetailsHeader = "x-cap-failure-details";

    // NOTE: -1 value because the CAP incremented after filters and handle 0 retries case
    private int LatestRetryCount => Math.Max(capOptions.Value.FailedRetryCount - 1, 0);

    public override Task OnSubscribeExceptionAsync(ExceptionContext context)
    {
        if (context.MediumMessage.Retries != this.LatestRetryCount)
        {
            return Task.CompletedTask;
        }

        var ex = context.Exception;
        var errorText = ex.GetBaseException().Message;

        var details = new CapFailureDetails(ex.GetType().FullName ?? ex.GetType().Name, errorText, ex.StackTrace);
        context.DeliverMessage.Headers[ErrorDetailsHeader] = JsonSerializer.Serialize(details);
        context.DeliverMessage.Headers.TryGetValue(RawCapturingSerializer.RawBodyHeader, out var rawMessageBody);

        var payloadParam = context.ConsumerDescriptor.Parameters.SingleOrDefault(p => !p.IsFromCap);
        var value = context.DeliverMessage.Value;
        var payload = payloadParam is not null && value is not null && serializer.IsJsonType(value)
            ? serializer.Deserialize(value, payloadParam.ParameterType)
            : value;

        return Task.WhenAll(failsProcessors.Select(x => x.HandleAsync(payload as TInputEvent, ex, rawMessageBody)));
    }

    internal sealed record CapFailureDetails(string ExceptionType, string Message, string? StackTrace);
}
