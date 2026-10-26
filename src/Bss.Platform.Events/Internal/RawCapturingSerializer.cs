using System.Text;

using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Serialization;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Events.Internal;

/// <summary>
/// Wraps the default CAP serializer to preserve the raw wire body in a message header before it gets
/// deserialized into the subscriber's parameter type (which may have fewer fields than the original message).
/// Stored as a header (not a process-local cache) so it survives retries and the DB-backed retry processor,
/// which can re-execute a failed message on any instance in a multi-pod deployment.
/// Also dead-letters messages that fail deserialization itself, since CAP never routes those into
/// <see cref="DotNetCore.CAP.Filter.ISubscribeFilter" /> - they are acked off the broker after a single attempt.
/// </summary>
internal sealed class RawCapturingSerializer(IOptions<CapOptions> capOptions, DeadLetterProcessor deadLetterProcessor) : ISerializer
{
    internal const string RawBodyHeader = "x-cap-raw-body";

    private readonly JsonUtf8Serializer inner = new(capOptions);

    public ValueTask<TransportMessage> SerializeAsync(Message message) =>
        this.inner.SerializeAsync(message);

    public async ValueTask<Message> DeserializeAsync(TransportMessage transportMessage, Type? valueType)
    {
        if (transportMessage.Body.Length > 0 && !transportMessage.Headers.ContainsKey(RawBodyHeader))
        {
            transportMessage.Headers[RawBodyHeader] = Encoding.UTF8.GetString(transportMessage.Body.Span);
        }

        Message message;
        try
        {
            message = await this.inner.DeserializeAsync(transportMessage, valueType);
        }
        catch (Exception ex)
        {
            transportMessage.Headers.TryGetValue(RawBodyHeader, out var rawBody);
            deadLetterProcessor.SendDeadLetter(transportMessage.GetName(), ex, rawBody);
            throw;
        }

        return message;
    }

    public string Serialize(Message message) =>
        this.inner.Serialize(message);

    public Message? Deserialize(string json) =>
        this.inner.Deserialize(json);

    public object? Deserialize(object value, Type valueType) =>
        this.inner.Deserialize(value, valueType);

    public bool IsJsonType(object jsonObject) =>
        this.inner.IsJsonType(jsonObject);
}
