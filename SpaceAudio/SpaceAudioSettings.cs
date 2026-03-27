using SpaceAudio.Enums;
using SpaceAudio.Localization;
using SpaceAudio.ViewModels;
using SpaceAudio.Views;
using YukkuriMovieMaker.Plugin;

namespace SpaceAudio;

public sealed class SpaceAudioSettings : SettingsBase<SpaceAudioSettings>
{
    public override string Name => Texts.SettingsTitle;
    public override SettingsCategory Category => SettingsCategory.AudioEffect;
    public override bool HasSettingView => false;

    public override object SettingView => new SpaceAudioSettingsWindow
    {
        DataContext = new SpaceAudioSettingsViewModel()
    };

    public override void Initialize() { }

    public ReverbQuality Quality { get; set; } = ReverbQuality.Standard;
    public double EditorHeight { get; set; } = 280;
    public string DefaultPreset { get; set; } = "";
}
