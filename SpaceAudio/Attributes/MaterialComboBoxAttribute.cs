using SpaceAudio.Views;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace SpaceAudio.Attributes;

internal abstract class MaterialComboBoxAttributeBase : PropertyEditorAttribute2
{
    protected abstract string PropertyPath { get; }

    public override FrameworkElement Create() => new MaterialComboBoxControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not MaterialComboBoxControl combo) return;
        var owner = itemProperties.FirstOrDefault()?.PropertyOwner;
        if (owner is null) return;

        combo.SetBinding(MaterialComboBoxControl.SelectedMaterialIdProperty,
            new Binding(PropertyPath) { Source = owner, Mode = BindingMode.TwoWay });
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is MaterialComboBoxControl combo)
            BindingOperations.ClearBinding(combo, MaterialComboBoxControl.SelectedMaterialIdProperty);
    }
}

internal sealed class WallMaterialComboBoxAttribute : MaterialComboBoxAttributeBase
{
    protected override string PropertyPath => $"Effect.{nameof(SpaceAudioEffect.WallMaterialId)}";
}

internal sealed class FloorMaterialComboBoxAttribute : MaterialComboBoxAttributeBase
{
    protected override string PropertyPath => $"Effect.{nameof(SpaceAudioEffect.FloorMaterialId)}";
}

internal sealed class CeilingMaterialComboBoxAttribute : MaterialComboBoxAttributeBase
{
    protected override string PropertyPath => $"Effect.{nameof(SpaceAudioEffect.CeilingMaterialId)}";
}
