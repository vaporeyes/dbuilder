// ABOUTME: Dialog for editing Sound Propagation overlay colors stored in editor settings.
// ABOUTME: Uses UDB color configuration labels while accepting compact ARGB hex values.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class SoundPropagationColorDialog : PropertyDialog
{
    private readonly TextBox _highlight;
    private readonly TextBox _level1;
    private readonly TextBox _level2;
    private readonly TextBox _noSound;
    private readonly TextBox _blockSound;
    private readonly SoundPropagationColorSettings _initial;

    public SoundPropagationColorSettings ResultColors { get; private set; }

    public SoundPropagationColorDialog(SoundPropagationColorSettings colors)
        : base(SoundPropagationColorSettings.ColorConfigurationTitle, "Use 8-digit ARGB hex values.")
    {
        _initial = colors;
        ResultColors = colors;

        _highlight = AddField("Highlight color", Format(colors.HighlightColor));
        _level1 = AddField("Level 1 color", Format(colors.Level1Color));
        _level2 = AddField("Level 2 color", Format(colors.Level2Color));
        _noSound = AddField("No sound color", Format(colors.NoSoundColor));
        _blockSound = AddField("Block sound color", Format(colors.BlockSoundColor));

        var reset = new Button { Content = SoundPropagationColorSettings.ResetColorsText, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        reset.Click += (_, _) => SetFields(SoundPropagationColorSettings.Default);
        AddCustomRow(reset);
    }

    protected override void OnConfirm()
    {
        ResultColors = _initial with
        {
            HighlightColor = ParseColor(_highlight, _initial.HighlightColor),
            Level1Color = ParseColor(_level1, _initial.Level1Color),
            Level2Color = ParseColor(_level2, _initial.Level2Color),
            NoSoundColor = ParseColor(_noSound, _initial.NoSoundColor),
            BlockSoundColor = ParseColor(_blockSound, _initial.BlockSoundColor),
        };
    }

    private void SetFields(SoundPropagationColorSettings colors)
    {
        _highlight.Text = Format(colors.HighlightColor);
        _level1.Text = Format(colors.Level1Color);
        _level2.Text = Format(colors.Level2Color);
        _noSound.Text = Format(colors.NoSoundColor);
        _blockSound.Text = Format(colors.BlockSoundColor);
    }

    private static string Format(uint color)
        => color.ToString("X8", CultureInfo.InvariantCulture);

    private static uint ParseColor(TextBox box, uint fallback)
    {
        string text = (box.Text ?? "").Trim();
        if (text.StartsWith("#", StringComparison.Ordinal)) text = text[1..];
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
            return hex;
        if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint numeric))
            return numeric;
        return fallback;
    }
}
