// ABOUTME: Modal dialog for the stair builder - start heights and per-step deltas for floor and ceiling stairs.

using System.Globalization;
using Avalonia.Controls;

namespace DBuilder.Editor;

public sealed class StairBuilderDialog : PropertyDialog
{
    private readonly TextBox _floorStart, _floorStep, _ceilingStart, _ceilingStep;
    private readonly CheckBox _applyCeiling;

    public int ResultFloorStart, ResultFloorStep, ResultCeilingStart, ResultCeilingStep;
    public bool ResultApplyCeiling;

    public StairBuilderDialog(int defaultFloorStart, int defaultFloorStep, int defaultCeilingStart, int defaultCeilingStep)
        : base("Build Stairs", "Floors step up across the selected sectors, in order.")
    {
        _floorStart = AddField("Start floor height", defaultFloorStart.ToString(CultureInfo.InvariantCulture));
        _floorStep = AddField("Floor step", defaultFloorStep.ToString(CultureInfo.InvariantCulture));
        _applyCeiling = AddCheckBox("Apply ceiling heights", true);
        _ceilingStart = AddField("Start ceiling height", defaultCeilingStart.ToString(CultureInfo.InvariantCulture));
        _ceilingStep = AddField("Ceiling step", defaultCeilingStep.ToString(CultureInfo.InvariantCulture));
        ResultFloorStart = defaultFloorStart;
        ResultFloorStep = defaultFloorStep;
        ResultCeilingStart = defaultCeilingStart;
        ResultCeilingStep = defaultCeilingStep;
        ResultApplyCeiling = true;
    }

    protected override void OnConfirm()
    {
        ResultFloorStart = ParseInt(_floorStart, 0);
        ResultFloorStep = ParseInt(_floorStep, 8);
        ResultApplyCeiling = _applyCeiling.IsChecked == true;
        ResultCeilingStart = ParseInt(_ceilingStart, 128);
        ResultCeilingStep = ParseInt(_ceilingStep, ResultFloorStep);
    }
}
