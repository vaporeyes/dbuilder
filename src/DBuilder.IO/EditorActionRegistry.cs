// ABOUTME: Provides reflection-based editor action attributes and bindings for command registration.
// ABOUTME: Mirrors UDB begin/end action hooks while binding directly to stable DBuilder command ids.

using System.Reflection;

namespace DBuilder.IO;

public enum EditorActionPhase
{
    Begin,
    End,
}

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public abstract class EditorActionAttribute : Attribute
{
    protected EditorActionAttribute(string commandId)
    {
        CommandId = commandId;
    }

    public string CommandId { get; }
    public bool BaseAction { get; set; }
    public string Library { get; set; } = "";
    public abstract EditorActionPhase Phase { get; }
}

public sealed class BeginEditorActionAttribute : EditorActionAttribute
{
    public BeginEditorActionAttribute(string commandId) : base(commandId)
    {
    }

    public override EditorActionPhase Phase => EditorActionPhase.Begin;
}

public sealed class EndEditorActionAttribute : EditorActionAttribute
{
    public EndEditorActionAttribute(string commandId) : base(commandId)
    {
    }

    public override EditorActionPhase Phase => EditorActionPhase.End;
}

public sealed record EditorActionBinding(
    string CommandId,
    EditorActionPhase Phase,
    object? Target,
    MethodInfo Method,
    bool BaseAction,
    string Library)
{
    public bool Invoke()
    {
        object? result = Method.Invoke(Target, Array.Empty<object>());
        return result is not bool handled || handled;
    }
}

public static class EditorActionRegistry
{
    public static IReadOnlyList<EditorActionBinding> Discover(params object[] targets)
        => targets.SelectMany(Discover).ToArray();

    public static IReadOnlyList<EditorActionBinding> Discover(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return Discover(target.GetType(), target).ToArray();
    }

    public static IReadOnlyList<EditorActionBinding> DiscoverStatic(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Discover(type, null).ToArray();
    }

    private static IEnumerable<EditorActionBinding> Discover(Type type, object? target)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var method in type.GetMethods(flags))
        {
            if (target is null && !method.IsStatic) continue;
            if (target is not null && method.IsStatic) continue;
            if (method.GetParameters().Length != 0) continue;
            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(bool)) continue;

            foreach (var attribute in method.GetCustomAttributes<EditorActionAttribute>(true))
            {
                if (string.IsNullOrWhiteSpace(attribute.CommandId)) continue;
                yield return new EditorActionBinding(
                    attribute.CommandId,
                    attribute.Phase,
                    target,
                    method,
                    attribute.BaseAction,
                    attribute.Library);
            }
        }
    }
}

public sealed class EditorActionDispatcher
{
    private readonly Dictionary<string, EditorActionBinding[]> _beginBindings;
    private readonly Dictionary<string, EditorActionBinding[]> _endBindings;

    public EditorActionDispatcher(IEnumerable<EditorActionBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var bindingList = bindings.ToArray();
        Bindings = bindingList;
        _beginBindings = Group(bindingList, EditorActionPhase.Begin);
        _endBindings = Group(bindingList, EditorActionPhase.End);
    }

    public IReadOnlyList<EditorActionBinding> Bindings { get; }

    public static EditorActionDispatcher FromTargets(params object[] targets)
        => new(EditorActionRegistry.Discover(targets));

    public bool Begin(string commandId) => Invoke(_beginBindings, commandId);

    public bool End(string commandId) => Invoke(_endBindings, commandId);

    public IReadOnlyList<EditorActionBinding> BindingsFor(string commandId, EditorActionPhase phase)
    {
        var groups = phase == EditorActionPhase.Begin ? _beginBindings : _endBindings;
        return groups.TryGetValue(commandId, out var bindings) ? bindings : Array.Empty<EditorActionBinding>();
    }

    private static Dictionary<string, EditorActionBinding[]> Group(
        IReadOnlyList<EditorActionBinding> bindings,
        EditorActionPhase phase)
        => bindings
            .Where(binding => binding.Phase == phase)
            .GroupBy(binding => binding.CommandId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

    private static bool Invoke(Dictionary<string, EditorActionBinding[]> groups, string commandId)
    {
        if (!groups.TryGetValue(commandId, out var bindings)) return false;

        bool handled = false;
        foreach (var binding in bindings)
            handled |= binding.Invoke();
        return handled;
    }
}
