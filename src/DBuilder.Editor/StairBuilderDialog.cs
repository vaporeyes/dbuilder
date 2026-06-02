// ABOUTME: Modal dialog for the stair builder start heights and per-step deltas.
// ABOUTME: Provides UDB-style prefab save, load, delete, and default controls for stair settings.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class StairBuilderDialog : PropertyDialog
{
    private readonly TextBox _floorStart, _floorStep, _ceilingStart, _ceilingStep;
    private readonly TextBox _prefabName = new();
    private readonly CheckBox _applyCeiling;
    private readonly ListBox _prefabs = new();
    private readonly TextBlock _prefabStatus = new();
    private List<StairBuilderPrefab> _prefabList;
    private string? _pendingOverwriteName;

    public int ResultFloorStart, ResultFloorStep, ResultCeilingStart, ResultCeilingStep;
    public bool ResultApplyCeiling;
    public IReadOnlyList<StairBuilderPrefab> ResultPrefabs => _prefabList;
    public bool PrefabsChanged { get; private set; }

    public StairBuilderDialog(
        int defaultFloorStart,
        int defaultFloorStep,
        int defaultCeilingStart,
        int defaultCeilingStep,
        IReadOnlyList<StairBuilderPrefab>? prefabs = null)
        : base("Build Stairs", "Floors step up across the selected sectors, in order.")
    {
        _prefabList = prefabs?.ToList() ?? new List<StairBuilderPrefab>();
        _floorStart = AddField("Start floor height", defaultFloorStart.ToString(CultureInfo.InvariantCulture));
        _floorStep = AddField("Floor step", defaultFloorStep.ToString(CultureInfo.InvariantCulture));
        _applyCeiling = AddCheckBox("Apply ceiling heights", true);
        _ceilingStart = AddField("Start ceiling height", defaultCeilingStart.ToString(CultureInfo.InvariantCulture));
        _ceilingStep = AddField("Ceiling step", defaultCeilingStep.ToString(CultureInfo.InvariantCulture));
        AddPrefabControls();
        ResultFloorStart = defaultFloorStart;
        ResultFloorStep = defaultFloorStep;
        ResultCeilingStart = defaultCeilingStart;
        ResultCeilingStep = defaultCeilingStep;
        ResultApplyCeiling = true;

        StairBuilderPrefab? defaultPrefab = _prefabList.FirstOrDefault(prefab => prefab.Name == StairBuilderPrefabSettings.DefaultPrefabName);
        if (defaultPrefab != null) LoadPrefab(defaultPrefab);
        else _prefabName.Text = StairBuilderPrefabSettings.CreateSuggestedName(_prefabList);
        RefreshPrefabs();
    }

    protected override void OnConfirm()
    {
        ResultFloorStart = ParseInt(_floorStart, 0);
        ResultFloorStep = ParseInt(_floorStep, 8);
        ResultApplyCeiling = _applyCeiling.IsChecked == true;
        ResultCeilingStart = ParseInt(_ceilingStart, 128);
        ResultCeilingStep = ParseInt(_ceilingStep, ResultFloorStep);
        SavePreviousPrefab();
    }

    private void AddPrefabControls()
    {
        _prefabName.Watermark = "Prefab name";
        _prefabs.MinHeight = 120;
        _prefabs.DoubleTapped += (_, _) => LoadSelectedPrefab();
        _prefabStatus.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                Button("Save", SaveNamedPrefab),
                Button("Load", LoadSelectedPrefab),
                Button("Delete", DeleteSelectedPrefab),
                Button("Set default", SaveDefaultPrefab),
            },
        };

        var panel = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                new TextBlock { Text = "Prefabs" },
                _prefabName,
                new Border
                {
                    BorderBrush = Avalonia.Media.Brushes.DimGray,
                    BorderThickness = new Thickness(1),
                    Child = _prefabs,
                },
                buttons,
                _prefabStatus,
            },
        };
        AddCustomRow(panel);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, MinWidth = 72 };
        button.Click += (_, _) => action();
        return button;
    }

    private void RefreshPrefabs(int selectedIndex = -1)
    {
        var rows = new List<ListBoxItem>();
        for (int i = 0; i < _prefabList.Count; i++)
        {
            StairBuilderPrefab prefab = _prefabList[i];
            rows.Add(new ListBoxItem
            {
                Content = $"{prefab.Name} ({TabName(prefab)})",
                Tag = prefab,
            });
        }

        _prefabs.ItemsSource = rows;
        if (selectedIndex >= 0 && selectedIndex < rows.Count) _prefabs.SelectedIndex = selectedIndex;
    }

    private void SaveNamedPrefab()
    {
        StairBuilderPrefab draft = CurrentPrefab((_prefabName.Text ?? "").Trim());
        bool overwrite = _pendingOverwriteName == draft.Name;
        StairBuilderPrefabSaveResult result = StairBuilderPrefabSettings.SaveManualPrefab(_prefabList, draft, overwrite);
        ApplySaveResult(result, draft.Name, duplicateMessage: "Prefab already exists. Click Save again to overwrite.");
    }

    private void SaveDefaultPrefab()
    {
        StairBuilderPrefab draft = CurrentPrefab(StairBuilderPrefabSettings.DefaultPrefabName);
        StairBuilderPrefabSaveResult result = StairBuilderPrefabSettings.SaveForcedPrefab(_prefabList, draft, position: 0);
        ApplySaveResult(result, draft.Name, duplicateMessage: "");
    }

    private void SavePreviousPrefab()
    {
        StairBuilderPrefab draft = CurrentPrefab(StairBuilderPrefabSettings.PreviousPrefabName);
        StairBuilderPrefabSaveResult result = StairBuilderPrefabSettings.SaveForcedPrefab(
            _prefabList,
            draft,
            StairBuilderPrefabSettings.PreviousInsertPosition(_prefabList));
        if (result.Status == StairBuilderPrefabSaveStatus.Saved)
        {
            _prefabList = result.Prefabs.ToList();
            PrefabsChanged = true;
        }
    }

    private void ApplySaveResult(StairBuilderPrefabSaveResult result, string name, string duplicateMessage)
    {
        if (result.Status == StairBuilderPrefabSaveStatus.EmptyName)
        {
            _prefabStatus.Text = "Please enter a name for the prefab.";
            return;
        }

        if (result.Status == StairBuilderPrefabSaveStatus.ReservedName)
        {
            _prefabStatus.Text = "The prefab name is reserved.";
            return;
        }

        if (result.Status == StairBuilderPrefabSaveStatus.DuplicateName)
        {
            _pendingOverwriteName = name;
            _prefabStatus.Text = duplicateMessage;
            return;
        }

        _pendingOverwriteName = null;
        _prefabList = result.Prefabs.ToList();
        PrefabsChanged = true;
        RefreshPrefabs(result.Index);
        _prefabStatus.Text = $"Saved prefab {name}.";
    }

    private void LoadSelectedPrefab()
    {
        if (_prefabs.SelectedItem is not ListBoxItem { Tag: StairBuilderPrefab prefab }) return;
        LoadPrefab(prefab);
        _prefabStatus.Text = $"Loaded prefab {prefab.Name}.";
    }

    private void LoadPrefab(StairBuilderPrefab prefab)
    {
        _prefabName.Text = prefab.Name;
        _floorStep.Text = prefab.FloorStep.ToString(CultureInfo.InvariantCulture);
        _applyCeiling.IsChecked = prefab.ApplyCeilingHeight;
        _ceilingStep.Text = prefab.CeilingStep.ToString(CultureInfo.InvariantCulture);
    }

    private void DeleteSelectedPrefab()
    {
        int index = _prefabs.SelectedIndex;
        if (index < 0 || index >= _prefabList.Count) return;
        string name = _prefabList[index].Name;
        _prefabList.RemoveAt(index);
        PrefabsChanged = true;
        RefreshPrefabs(Math.Min(index, _prefabList.Count - 1));
        _prefabStatus.Text = $"Deleted prefab {name}.";
    }

    private StairBuilderPrefab CurrentPrefab(string name)
        => new()
        {
            Name = name,
            StairType = (int)StairBuilderTab.Straight,
            ApplyFloorHeight = true,
            FloorStep = ParseInt(_floorStep, 8),
            ApplyCeilingHeight = _applyCeiling.IsChecked == true,
            CeilingStep = ParseInt(_ceilingStep, ParseInt(_floorStep, 8)),
        };

    private static string TabName(StairBuilderPrefab prefab)
        => Enum.IsDefined(typeof(StairBuilderTab), prefab.StairType)
            ? ((StairBuilderTab)prefab.StairType).ToString()
            : StairBuilderTab.Straight.ToString();
}
