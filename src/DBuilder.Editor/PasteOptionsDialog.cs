// ABOUTME: Modal dialog for UDB-style one-shot paste options before pasting a copied selection.
// ABOUTME: Initializes from saved paste defaults and exposes the selected options without persisting them.

using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class PasteOptionsDialog : PropertyDialog
{
    private readonly ComboBox _pasteTagMode;
    private readonly CheckBox _pasteRemoveActions;

    public PasteOptions PasteOptions { get; private set; }

    public PasteOptionsDialog(PasteOptions defaults)
        : base("Paste Special", "Choose how tags and actions are handled for this paste.")
    {
        Width = 420;
        PasteOptions = defaults.Copy();
        _pasteTagMode = AddCombo("Pasted tags", PasteTagModeItems(), (int)PasteOptions.ChangeTags);
        _pasteRemoveActions = AddCheckBox("Remove pasted actions", PasteOptions.RemoveActions);
    }

    protected override void OnConfirm()
    {
        PasteOptions = new PasteOptions
        {
            ChangeTags = (PasteTagMode)ComboNumber(_pasteTagMode, (int)PasteTagMode.Keep),
            RemoveActions = _pasteRemoveActions.IsChecked == true,
        };
    }

    private static IEnumerable<CatalogItem> PasteTagModeItems()
    {
        yield return new CatalogItem((int)PasteTagMode.Keep, "Keep tags");
        yield return new CatalogItem((int)PasteTagMode.Renumber, "Renumber conflicting tags");
        yield return new CatalogItem((int)PasteTagMode.Remove, "Remove tags");
    }
}
