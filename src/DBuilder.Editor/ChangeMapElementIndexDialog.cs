// ABOUTME: Modal dialog for UDB-style map element index reassignment.
// ABOUTME: Captures the requested index for one selected vertex, linedef, sector, or thing.

using System.Globalization;
using Avalonia.Controls;

namespace DBuilder.Editor;

public sealed class ChangeMapElementIndexDialog : PropertyDialog
{
    private readonly TextBox _index;
    private readonly int _currentIndex;

    public int NewIndex { get; private set; }

    public ChangeMapElementIndexDialog(string elementName, int currentIndex, int maxIndex)
        : base("Change Map Element Index", "Enter a new " + elementName + " index from 0 to " + maxIndex + ".")
    {
        _currentIndex = currentIndex;
        NewIndex = currentIndex;
        _index = AddField("Index", currentIndex.ToString(CultureInfo.InvariantCulture));
    }

    protected override void OnConfirm()
    {
        NewIndex = ParseInt(_index, _currentIndex);
    }
}
