// ABOUTME: Dialog for applying BuilderEffects directional flat shading to selected UDMF geometry.
// ABOUTME: Keeps compact numeric option parsing in the editor layer while BuilderEffects owns shading math.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class DirectionalShadingDialog : PropertyDialog
{
    private static DirectionalShadingOptions s_options = new();

    private readonly TextBox _sunAngle;
    private readonly TextBox _lightAmount;
    private readonly TextBox _lightColor;
    private readonly TextBox _shadeAmount;
    private readonly TextBox _shadeColor;

    public DirectionalShadingOptions ResultOptions { get; private set; } = new();

    public DirectionalShadingDialog()
        : base("Apply Directional Shading")
    {
        ResultOptions = s_options;
        DirectionalShadingOptions defaults = ResultOptions;
        _sunAngle = AddField("Sun angle", defaults.SunAngleDegrees.ToString(CultureInfo.InvariantCulture));
        _lightAmount = AddField("Light amount", defaults.LightAmount.ToString(CultureInfo.InvariantCulture));
        _lightColor = AddField("Light color", defaults.LightColor.ToString("X6", CultureInfo.InvariantCulture));
        _shadeAmount = AddField("Shade amount", defaults.ShadeAmount.ToString(CultureInfo.InvariantCulture));
        _shadeColor = AddField("Shade color", defaults.ShadeColor.ToString("X6", CultureInfo.InvariantCulture));
    }

    protected override void OnConfirm()
    {
        DirectionalShadingOptions defaults = ResultOptions;
        ResultOptions = new DirectionalShadingOptions(
            SunAngleDegrees: ParseInt(_sunAngle, defaults.SunAngleDegrees),
            LightAmount: ParseInt(_lightAmount, defaults.LightAmount),
            LightColor: ParseColor(_lightColor, defaults.LightColor),
            ShadeAmount: ParseInt(_shadeAmount, defaults.ShadeAmount),
            ShadeColor: ParseColor(_shadeColor, defaults.ShadeColor));
        s_options = ResultOptions;
    }

    private static int ParseColor(TextBox box, int fallback)
    {
        string text = (box.Text ?? "").Trim().TrimStart('#');
        return int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value)
            ? value & BuilderEffects.WhiteNoAlpha
            : fallback;
    }
}
