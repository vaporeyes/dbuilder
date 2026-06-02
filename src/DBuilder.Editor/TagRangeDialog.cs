// ABOUTME: Dialog for the TagRange plugin command that assigns ordered tag ranges.
// ABOUTME: Captures target type, start, step, relative mode, and used-tag skipping for selected elements.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class TagRangeDialog : PropertyDialog
{
    private readonly ComboBox _target;
    private readonly TextBox _startTag;
    private readonly TextBox _step;
    private readonly TextBox _maxTag;
    private readonly CheckBox _relative;
    private readonly CheckBox _skipUsedTags;

    public TagRangeTargetKind ResultTarget { get; private set; }
    public TagRangeOptions ResultOptions { get; private set; }

    public TagRangeDialog(TagRangeTargetKind target, int startTag, TagRangeStoredOptions storedOptions)
        : base(TagRangeModel.ToolWindowTitle)
    {
        storedOptions = TagRangeModel.NormalizeStoredOptions(storedOptions);
        ResultTarget = target;
        ResultOptions = new TagRangeOptions(startTag, storedOptions.Step, storedOptions.Relative, SkipUsedTags: false);

        _target = AddCombo(
            "Apply to",
            new[]
            {
                new CatalogItem((int)TagRangeTargetKind.Sectors, "Sectors"),
                new CatalogItem((int)TagRangeTargetKind.Linedefs, "Linedefs"),
                new CatalogItem((int)TagRangeTargetKind.Things, "Things"),
            },
            (int)target);
        _startTag = AddField(TagRangeModel.StartTagLabel, startTag.ToString(CultureInfo.InvariantCulture));
        _step = AddField(TagRangeModel.IncrementLabel, storedOptions.Step.ToString(CultureInfo.InvariantCulture));
        _maxTag = AddField("Max tag", int.MaxValue.ToString(CultureInfo.InvariantCulture));
        _relative = AddCheckBox(TagRangeModel.RelativeModeText, storedOptions.Relative);
        _skipUsedTags = AddCheckBox(TagRangeModel.SkipUsedTagsText, false);
    }

    protected override void OnConfirm()
    {
        ResultTarget = (TagRangeTargetKind)ComboNumber(_target, (int)ResultTarget);
        int step = ParseInt(_step, ResultOptions.Step);
        if (step == 0) step = 1;
        int maxTag = ParseInt(_maxTag, ResultOptions.MaxTag);
        ResultOptions = new TagRangeOptions(
            StartTag: ParseInt(_startTag, ResultOptions.StartTag),
            Step: step,
            Relative: _relative.IsChecked == true,
            SkipUsedTags: _skipUsedTags.IsChecked == true,
            MaxTag: maxTag);
    }
}
