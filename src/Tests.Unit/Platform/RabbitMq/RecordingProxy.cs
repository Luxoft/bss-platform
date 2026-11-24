using System.Reflection;

namespace Tests.Unit.Platform.RabbitMq;

public class RecordingProxy<T> : DispatchProxy where T : class
{
    public List<(string Method, object?[] Arguments)> Calls { get; } = [];
    public Func<MethodInfo, object?[], object?>? Handler { get; set; }

    public static (T Object, RecordingProxy<T> Proxy) Create()
    {
        var value = Create<T, RecordingProxy<T>>();
        return (value, (RecordingProxy<T>)(object)value);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        var arguments = args ?? [];
        this.Calls.Add((method.Name, arguments));
        if (this.Handler is not null)
        {
            return this.Handler(method, arguments);
        }

        return method.ReturnType != typeof(void) && method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
