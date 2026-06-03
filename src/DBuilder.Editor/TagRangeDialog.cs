// ABOUTME: Dialog for the TagRange plugin command that assigns ordered tag ranges.
// ABOUTME: Captures target type, start, step, relative mode, and used-tag skipping for selected elements.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
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
    private readonly TextBlock _endTag = new();
    private readonly TextBlock _warning = new();
    private readonly TagRangeSelectionContext? _selectionContext;

    public TagRangeTargetKind ResultTarget { get; private set; }
    public TagRangeOptions ResultOptions { get; private set; }

    public TagRangeDialog(
        TagRangeTargetKind target,
        int startTag,
        TagRangeStoredOptions storedOptions,
        TagRangeSelectionContext? selectionContext = null)
        : base(TagRangeModel.ToolWindowTitle)
    {
        storedOptions = TagRangeModel.NormalizeStoredOptions(storedOptions);
        _selectionContext = selectionContext;
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
        AddPreviewRows();
        WirePreviewRefresh();
        RefreshPreview();
    }

    protected override void OnConfirm()
    {
        ResultTarget = (TagRangeTargetKind)ComboNumber(_target, (int)ResultTarget);
        ResultOptions = CurrentOptions();
    }

    private void AddPreviewRows()
    {
        _endTag.Foreground = Brushes.LightSkyBlue;
        _warning.Foreground = Brushes.OrangeRed;
        _warning.TextWrapping = TextWrapping.Wrap;
        AddCustomRow(_endTag);
        AddCustomRow(_warning);
    }

    private void WirePreviewRefresh()
    {
        _target.SelectionChanged += (_, _) => RefreshPreview();
        _startTag.TextChanged += (_, _) => RefreshPreview();
        _step.TextChanged += (_, _) => RefreshPreview();
        _maxTag.TextChanged += (_, _) => RefreshPreview();
        _relative.IsCheckedChanged += (_, _) => RefreshPreview();
        _skipUsedTags.IsCheckedChanged += (_, _) => RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_selectionContext == null)
        {
            _endTag.Text = "";
            _warning.Text = "";
            return;
        }

        TagRangeTargetKind target = (TagRangeTargetKind)ComboNumber(_target, (int)ResultTarget);
        TagRangeOptions options = CurrentOptions();
        IReadOnlyList<int> initialTags = _selectionContext.InitialTags(target);
        TagRangePreviewState preview = TagRangeModel.CreatePreviewState(
            target,
            initialTags.Count,
            initialTags,
            _selectionContext.UsedTags,
            options);

        _endTag.Text = preview.EndTag.HasValue
            ? $"{TagRangeModel.EndTagLabel} {preview.EndTag.Value.ToString(CultureInfo.InvariantCulture)}"
            : $"{TagRangeModel.EndTagLabel} -";

        _warning.Text = preview.OutOfTagsWarningVisible
            ? TagRangeModel.OutOfTagsWarningText
            : preview.DoubleTagWarningVisible
                ? TagRangeModel.DuplicateWarningText
                : "";
    }

    private TagRangeOptions CurrentOptions()
    {
        int step = ParseInt(_step, ResultOptions.Step);
        if (step == 0) step = 1;
        return new TagRangeOptions(
            StartTag: ParseInt(_startTag, ResultOptions.StartTag),
            Step: step,
            Relative: _relative.IsChecked == true,
            SkipUsedTags: _skipUsedTags.IsChecked == true,
            MaxTag: ParseInt(_maxTag, ResultOptions.MaxTag));
    }
}
