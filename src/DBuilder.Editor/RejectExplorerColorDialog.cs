// ABOUTME: Dialog for editing Reject Explorer overlay colors stored in editor settings.
// ABOUTME: Uses UDB color configuration labels while accepting compact ARGB hex values.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class RejectExplorerColorDialog : PropertyDialog
{
    private readonly TextBox _default;
    private readonly TextBox _highlight;
    private readonly TextBox _bidirectional;
    private readonly TextBox _unidirectionalFrom;
    private readonly TextBox _unidirectionalTo;
    private readonly RejectExplorerColorSettings _initial;

    public RejectExplorerColorSettings ResultColors { get; private set; }

    public RejectExplorerColorDialog(RejectExplorerColorSettings colors)
        : base(RejectExplorerModel.ColorConfigurationTitle, "Use 8-digit ARGB hex values.")
    {
        _initial = colors;
        ResultColors = colors;

        _default = AddField("Default color", Format(colors.Default));
        _highlight = AddField("Highlight color", Format(colors.Highlight));
        _bidirectional = AddField("Bidirectional color", Format(colors.Bidirectional));
        _unidirectionalFrom = AddField("Unidirectional from color", Format(colors.UnidirectionalFrom));
        _unidirectionalTo = AddField("Unidirectional to color", Format(colors.UnidirectionalTo));

        var reset = new Button { Content = RejectExplorerModel.ResetColorsText, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        reset.Click += (_, _) => SetFields(RejectExplorerModel.DefaultColors);
        AddCustomRow(reset);
    }

    protected override void OnConfirm()
    {
        ResultColors = new RejectExplorerColorSettings(
            ParseColor(_default, _initial.Default),
            ParseColor(_highlight, _initial.Highlight),
            ParseColor(_bidirectional, _initial.Bidirectional),
            ParseColor(_unidirectionalFrom, _initial.UnidirectionalFrom),
            ParseColor(_unidirectionalTo, _initial.UnidirectionalTo));
    }

    private void SetFields(RejectExplorerColorSettings colors)
    {
        _default.Text = Format(colors.Default);
        _highlight.Text = Format(colors.Highlight);
        _bidirectional.Text = Format(colors.Bidirectional);
        _unidirectionalFrom.Text = Format(colors.UnidirectionalFrom);
        _unidirectionalTo.Text = Format(colors.UnidirectionalTo);
    }

    private static string Format(int color)
        => unchecked((uint)color).ToString("X8", CultureInfo.InvariantCulture);

    private static int ParseColor(TextBox box, int fallback)
    {
        string text = (box.Text ?? "").Trim();
        if (text.StartsWith("#", StringComparison.Ordinal)) text = text[1..];
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
            return unchecked((int)hex);
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
            return numeric;
        return fallback;
    }
}
