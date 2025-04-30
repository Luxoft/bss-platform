using System.Reflection;

namespace Bss.Platform.RabbitMq.Consumer.Internal;

internal class AttributeMessageLinkProvider<TAttribute>(Func<TAttribute, string> getRoutingKey)
    where TAttribute : Attribute
{
    public IEnumerable<MessageLink> Find() =>
        typeof(TAttribute).Assembly.GetTypes()
            .Select(x => new { Type = x, Attribute = x.GetCustomAttribute<TAttribute>() })
            .Where(x => x.Attribute != null)
            .Select(x => new MessageLink(x.Type, getRoutingKey(x.Attribute!)));
}
