// ABOUTME: Modal dialog for the stair builder - start floor height, per-step delta, and whether to move ceilings.

using System.Globalization;
using Avalonia.Controls;

namespace DBuilder.Editor;

public sealed class StairBuilderDialog : PropertyDialog
{
    private readonly TextBox _start, _step;
    private readonly CheckBox _moveCeiling;

    public int ResultStart, ResultStep;
    public bool ResultMoveCeiling;

    public StairBuilderDialog(int defaultStart, int defaultStep)
        : base("Build Stairs", "Floors step up across the selected sectors, in order.")
    {
        _start = AddField("Start floor height", defaultStart.ToString(CultureInfo.InvariantCulture));
        _step = AddField("Step (per sector)", defaultStep.ToString(CultureInfo.InvariantCulture));
        _moveCeiling = AddCheckBox("Move ceilings too (keep headroom)", true);
        ResultStart = defaultStart;
        ResultStep = defaultStep;
        ResultMoveCeiling = true;
    }

    protected override void OnConfirm()
    {
        ResultStart = ParseInt(_start, 0);
        ResultStep = ParseInt(_step, 8);
        ResultMoveCeiling = _moveCeiling.IsChecked == true;
    }
}
