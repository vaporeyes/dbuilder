// ABOUTME: Tests UDB-style linedef color preset defaults and matching rules.
// ABOUTME: Covers action, activation, required flag, restricted flag, and enabled semantics.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class LinedefColorPresetModelTests
{
    private static Linedef Line(int action = 0, int activation = 0, params string[] flags)
    {
        var line = new Linedef(
            new Vertex(new Vector2D(0, 0)),
            new Vertex(new Vector2D(64, 0)))
        {
            Action = action,
            Activate = activation,
        };

        foreach (string flag in flags)
        {
            line.UdmfFlags.Add(flag);
        }

        return line;
    }

    [Fact]
    public void DefaultPresetMatchesAnyActionWithNormalActivation()
    {
        LinedefColorPreset preset = Assert.Single(LinedefColorPresetModel.DefaultPresets);

        Assert.Equal("Any action", preset.Name);
        Assert.Equal(LinedefColorPresetModel.PaleGreenArgb, preset.Color);
        Assert.Equal(LinedefColorPresetModel.AnyAction, preset.Action);
        Assert.Equal(LinedefColorPresetModel.DefaultAnyActionActivation, preset.Activation);
        Assert.True(LinedefColorPresetModel.Matches(Line(action: 80, activation: 0), preset));
        Assert.False(LinedefColorPresetModel.Matches(Line(action: 80, activation: 1), preset));
    }

    [Fact]
    public void DisabledPresetDoesNotMatch()
    {
        var preset = new LinedefColorPreset("Exit", 0, Action: 11, Activation: 0, Enabled: false);

        Assert.False(LinedefColorPresetModel.Matches(Line(action: 11, activation: 0), preset));
    }

    [Fact]
    public void SpecificActionAndAnyActivationMatchExpectedLines()
    {
        var preset = new LinedefColorPreset("Exit", 0, Action: 11, Activation: LinedefColorPresetModel.AnyActivation);

        Assert.True(LinedefColorPresetModel.Matches(Line(action: 11, activation: 0), preset));
        Assert.True(LinedefColorPresetModel.Matches(Line(action: 11, activation: 1), preset));
        Assert.False(LinedefColorPresetModel.Matches(Line(action: 80, activation: 1), preset));
    }

    [Fact]
    public void RequiredAndRestrictedFlagsMatchCaseInsensitively()
    {
        var preset = new LinedefColorPreset(
            "Locked",
            0,
            Action: LinedefColorPresetModel.AnyAction,
            Activation: LinedefColorPresetModel.AnyActivation,
            Flags: new[] { "secret" },
            RestrictedFlags: new[] { "dontdraw" });

        Assert.True(LinedefColorPresetModel.Matches(Line(flags: "SECRET"), preset));
        Assert.False(LinedefColorPresetModel.Matches(Line(flags: new[] { "secret", "dontdraw" }), preset));
        Assert.False(LinedefColorPresetModel.Matches(Line(flags: "blocking"), preset));
    }
}
