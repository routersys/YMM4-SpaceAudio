using SpaceAudio.Views;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace SpaceAudio.Attributes;

internal sealed class RoomEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new RoomEditorControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        var editor = (RoomEditorControl)control;
        if (itemProperties.FirstOrDefault()?.PropertyOwner is SpaceAudioEffect effect)
            editor.Effect = effect;
    }

    public override void ClearBindings(FrameworkElement control)
    {
        var editor = (RoomEditorControl)control;
        editor.Effect = null;
    }
}
