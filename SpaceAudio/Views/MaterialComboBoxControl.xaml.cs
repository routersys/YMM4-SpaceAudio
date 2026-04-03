using SpaceAudio.Models;
using SpaceAudio.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace SpaceAudio.Views;

public partial class MaterialComboBoxControl : UserControl, IPropertyEditorControl
{
    public static readonly DependencyProperty SelectedMaterialIdProperty =
        DependencyProperty.Register(
            nameof(SelectedMaterialId),
            typeof(string),
            typeof(MaterialComboBoxControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string? SelectedMaterialId
    {
        get => (string?)GetValue(SelectedMaterialIdProperty);
        set => SetValue(SelectedMaterialIdProperty, value);
    }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    private readonly ObservableCollection<CustomMaterial> _materials = new();

    public MaterialComboBoxControl()
    {
        InitializeComponent();

        InnerCombo.ItemsSource = _materials;

        Loaded += (_, _) =>
        {
            ServiceLocator.MaterialService.MaterialsChanged -= OnMaterialsChanged;
            ServiceLocator.MaterialService.MaterialsChanged += OnMaterialsChanged;
            UpdateMaterials();
        };

        Unloaded += (_, _) =>
        {
            ServiceLocator.MaterialService.MaterialsChanged -= OnMaterialsChanged;
        };

        InnerCombo.DropDownOpened += (_, _) => BeginEdit?.Invoke(this, EventArgs.Empty);
        InnerCombo.DropDownClosed += (_, _) => EndEdit?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaterialsChanged(object? s, EventArgs e) => Dispatcher.InvokeAsync(UpdateMaterials);

    private void UpdateMaterials()
    {
        var currentId = SelectedMaterialId;
        var newList = ServiceLocator.MaterialService.GetAll();

        for (int i = _materials.Count - 1; i >= 0; i--)
        {
            if (!newList.Any(m => m.Id == _materials[i].Id))
                _materials.RemoveAt(i);
        }

        for (int i = 0; i < newList.Count; i++)
        {
            var newItem = newList[i];
            var existingIdx = -1;
            for (int j = 0; j < _materials.Count; j++)
            {
                if (_materials[j].Id == newItem.Id) { existingIdx = j; break; }
            }

            if (existingIdx >= 0)
            {
                if (_materials[existingIdx].Name != newItem.Name || _materials[existingIdx].Absorption != newItem.Absorption)
                    _materials[existingIdx] = newItem;
                if (existingIdx != i)
                    _materials.Move(existingIdx, i);
            }
            else
            {
                _materials.Insert(i, newItem);
            }
        }

        if (!string.IsNullOrEmpty(currentId) && _materials.Any(m => m.Id == currentId))
        {
            if (SelectedMaterialId != currentId)
                SelectedMaterialId = currentId;
        }
        else if (_materials.Count > 0)
        {
            SelectedMaterialId = _materials.FirstOrDefault()?.Id;
        }
    }
}
