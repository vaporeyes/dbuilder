// ABOUTME: Verifies reflection-based editor action registration for stable command ids.
// ABOUTME: Covers begin and end action hooks used by the phased UDB action manager port.

using DBuilder.IO;

namespace DBuilder.Tests;

public class EditorActionRegistryTests
{
    [Fact]
    public void DiscoversBeginAndEndActionBindings()
    {
        var target = new ActionTarget();

        var bindings = EditorActionRegistry.Discover(target);

        Assert.Contains(bindings, binding =>
            binding.CommandId == "map2d.draw-sector"
            && binding.Phase == EditorActionPhase.Begin
            && binding.Target == target
            && !binding.BaseAction
            && binding.Library == "");
        Assert.Contains(bindings, binding =>
            binding.CommandId == "map2d.draw-sector"
            && binding.Phase == EditorActionPhase.End
            && binding.Target == target);
    }

    [Fact]
    public void InvokesVoidAndBoolActionBindings()
    {
        var target = new ActionTarget();
        var bindings = EditorActionRegistry.Discover(target);

        var begin = Assert.Single(bindings, binding => binding.Phase == EditorActionPhase.Begin && binding.CommandId == "map2d.draw-sector");
        var end = Assert.Single(bindings, binding => binding.Phase == EditorActionPhase.End && binding.CommandId == "map2d.draw-sector");
        var rejected = Assert.Single(bindings, binding => binding.CommandId == "map2d.cancel-draw");

        Assert.True(begin.Invoke());
        Assert.True(end.Invoke());
        Assert.False(rejected.Invoke());
        Assert.Equal(2, target.Invocations);
    }

    [Fact]
    public void DiscoversStaticActionBindings()
    {
        var bindings = EditorActionRegistry.DiscoverStatic(typeof(StaticActionTarget));

        var binding = Assert.Single(bindings);
        Assert.Equal("window.save", binding.CommandId);
        Assert.Equal(EditorActionPhase.Begin, binding.Phase);
        Assert.Null(binding.Target);
        Assert.True(binding.BaseAction);
        Assert.Equal("core", binding.Library);
    }

    [Fact]
    public void IgnoresUnsupportedActionMethods()
    {
        var bindings = EditorActionRegistry.Discover(new UnsupportedActionTarget());

        Assert.Empty(bindings);
    }

    [Fact]
    public void DispatcherInvokesBindingsByCommandAndPhase()
    {
        var first = new OrderedActionTarget("first");
        var second = new OrderedActionTarget("second");
        var dispatcher = EditorActionDispatcher.FromTargets(first, second);

        Assert.Equal(2, dispatcher.BindingsFor("map2d.draw-sector", EditorActionPhase.Begin).Count);
        Assert.Empty(dispatcher.BindingsFor("window.save", EditorActionPhase.Begin));
        Assert.False(dispatcher.Begin("missing.command"));

        Assert.True(dispatcher.Begin("map2d.draw-sector"));
        Assert.True(dispatcher.End("map2d.draw-sector"));

        Assert.Equal(new[] { "first:begin", "first:end" }, first.Events);
        Assert.Equal(new[] { "second:begin", "second:end" }, second.Events);
    }

    [Fact]
    public void DispatcherReportsUnhandledWhenAllBindingsReturnFalse()
    {
        var dispatcher = EditorActionDispatcher.FromTargets(new ActionTarget());

        Assert.False(dispatcher.Begin("map2d.cancel-draw"));
    }

    private sealed class ActionTarget
    {
        public int Invocations { get; private set; }

        [BeginEditorAction("map2d.draw-sector")]
        private void BeginDrawSector()
        {
            Invocations++;
        }

        [EndEditorAction("map2d.draw-sector")]
        private bool EndDrawSector()
        {
            Invocations++;
            return true;
        }

        [BeginEditorAction("map2d.cancel-draw")]
        private bool CancelDraw()
        {
            return false;
        }
    }

    private static class StaticActionTarget
    {
        [BeginEditorAction("window.save", BaseAction = true, Library = "core")]
        public static void Save()
        {
        }
    }

    private sealed class UnsupportedActionTarget
    {
        [BeginEditorAction("window.save")]
        public void WithParameter(string value)
        {
            _ = value;
        }

        [EndEditorAction("window.save")]
        public int WrongReturnType()
        {
            return 1;
        }
    }

    private sealed class OrderedActionTarget
    {
        private readonly string _name;

        public OrderedActionTarget(string name)
        {
            _name = name;
        }

        public List<string> Events { get; } = new();

        [BeginEditorAction("map2d.draw-sector")]
        public bool Begin()
        {
            Events.Add($"{_name}:begin");
            return true;
        }

        [EndEditorAction("map2d.draw-sector")]
        public void End()
        {
            Events.Add($"{_name}:end");
        }
    }
}
