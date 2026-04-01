using SpaceAudio.Attributes;
using SpaceAudio.Enums;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace SpaceAudio;

[PluginDetails(AuthorName = "routersys", ContentId = "nc470045")]

[AudioEffect(nameof(Texts.PluginName), [AudioEffectCategories.Effect], [nameof(Texts.PluginDescription)], ResourceType = typeof(Texts), IsAviUtlSupported = false)]
public sealed class SpaceAudioEffect : AudioEffectBase
{
    public override string Label => BuildLabel();

    [JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [Display(GroupName = nameof(Texts.GroupEditor), ResourceType = typeof(Texts), Order = 0)]
    [RoomEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public object? EditorPlaceholder => null;

    public Animation RoomWidth { get; } = new Animation(8.0, 1, 50);
    public Animation RoomHeight { get; } = new Animation(3.0, 1, 20);
    public Animation RoomDepth { get; } = new Animation(6.0, 1, 50);

    public RoomShape RoomShapeValue { get => _roomShape; set => Set(ref _roomShape, value); }
    private RoomShape _roomShape = RoomShape.Rectangular;

    public Animation SourceX { get; } = new Animation(2.0, 0, 50);
    public Animation SourceY { get; } = new Animation(1.5, 0, 20);
    public Animation SourceZ { get; } = new Animation(3.0, 0, 50);

    public Animation ListenerX { get; } = new Animation(6.0, 0, 50);
    public Animation ListenerY { get; } = new Animation(1.5, 0, 20);
    public Animation ListenerZ { get; } = new Animation(3.0, 0, 50);

    public Animation PreDelayMs { get; } = new Animation(20.0, 0, 200);
    public Animation DecayTime { get; } = new Animation(1.5, 0.1, 10);
    public Animation HfDamping { get; } = new Animation(0.5, 0, 1);
    public Animation Diffusion { get; } = new Animation(0.7, 0, 1);
    public Animation EarlyLevel { get; } = new Animation(-3.0, -40, 6);
    public Animation LateLevel { get; } = new Animation(-6.0, -40, 6);
    public Animation DryWetMix { get; } = new Animation(0.3, 0, 1);

    public WallMaterial WallMaterialValue { get => _wallMaterial; set => Set(ref _wallMaterial, value); }
    private WallMaterial _wallMaterial = WallMaterial.Drywall;

    public WallMaterial FloorMaterialValue { get => _floorMaterial; set => Set(ref _floorMaterial, value); }
    private WallMaterial _floorMaterial = WallMaterial.Wood;

    public WallMaterial CeilingMaterialValue { get => _ceilingMaterial; set => Set(ref _ceilingMaterial, value); }
    private WallMaterial _ceilingMaterial = WallMaterial.Drywall;

    public string CustomGeometryId { get => _customGeometryId; set => Set(ref _customGeometryId, value); }
    private string _customGeometryId = "";

    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RoomGeometry? CustomGeometry { get => _customGeometry; set => Set(ref _customGeometry, value); }
    private RoomGeometry? _customGeometry;

    public float RoomWidthValue { get => (float)RoomWidth.Values[0].Value; set => RoomWidth.Values[0].Value = value; }
    public float RoomHeightValue { get => (float)RoomHeight.Values[0].Value; set => RoomHeight.Values[0].Value = value; }
    public float RoomDepthValue { get => (float)RoomDepth.Values[0].Value; set => RoomDepth.Values[0].Value = value; }
    public float SourceXValue { get => (float)SourceX.Values[0].Value; set => SourceX.Values[0].Value = value; }
    public float SourceYValue { get => (float)SourceY.Values[0].Value; set => SourceY.Values[0].Value = value; }
    public float SourceZValue { get => (float)SourceZ.Values[0].Value; set => SourceZ.Values[0].Value = value; }
    public float ListenerXValue { get => (float)ListenerX.Values[0].Value; set => ListenerX.Values[0].Value = value; }
    public float ListenerYValue { get => (float)ListenerY.Values[0].Value; set => ListenerY.Values[0].Value = value; }
    public float ListenerZValue { get => (float)ListenerZ.Values[0].Value; set => ListenerZ.Values[0].Value = value; }
    public float PreDelayMsValue { get => (float)PreDelayMs.Values[0].Value; set => PreDelayMs.Values[0].Value = value; }
    public float DecayTimeValue { get => (float)DecayTime.Values[0].Value; set => DecayTime.Values[0].Value = value; }
    public float HfDampingValue { get => (float)HfDamping.Values[0].Value; set => HfDamping.Values[0].Value = value; }
    public float DiffusionValue { get => (float)Diffusion.Values[0].Value; set => Diffusion.Values[0].Value = value; }
    public float EarlyLevelValue { get => (float)EarlyLevel.Values[0].Value; set => EarlyLevel.Values[0].Value = value; }
    public float LateLevelValue { get => (float)LateLevel.Values[0].Value; set => LateLevel.Values[0].Value = value; }
    public float DryWetMixValue { get => (float)DryWetMix.Values[0].Value; set => DryWetMix.Values[0].Value = value; }

    public RoomParameters GetRoomParameters() => new(this);
    public SourceParameters GetSourceParameters() => new(this);
    public ListenerParameters GetListenerParameters() => new(this);
    public ReverbParameters GetReverbParameters() => new(this);
    public MaterialParameters GetMaterialParameters() => new(this);

    public RoomGeometry? ResolveGeometry()
    {
        if (_roomShape == RoomShape.Custom)
        {
            if (_customGeometry is not null) return _customGeometry;
            if (!string.IsNullOrEmpty(_customGeometryId))
            {
                var loaded = Services.ServiceLocator.GeometryService.Load(_customGeometryId);
                if (loaded is not null) return loaded;
            }
        }
        return null;
    }

    public RoomSnapshot CreateSnapshot(long frame, long totalFrames, int hz)
    {
        float w = (float)RoomWidth.GetValue(frame, totalFrames, hz);
        float h = (float)RoomHeight.GetValue(frame, totalFrames, hz);
        float d = (float)RoomDepth.GetValue(frame, totalFrames, hz);

        var geometry = ResolveGeometry();
        if (geometry is null && _roomShape != RoomShape.Rectangular)
        {
            geometry = RoomGeometry.FromShape(_roomShape, w, h, d,
                MaterialCoefficients.GetAbsorption(_wallMaterial),
                MaterialCoefficients.GetAbsorption(_floorMaterial),
                MaterialCoefficients.GetAbsorption(_ceilingMaterial));
        }

        return new RoomSnapshot(
            w, h, d,
            Math.Clamp((float)SourceX.GetValue(frame, totalFrames, hz), 0, w),
            Math.Clamp((float)SourceY.GetValue(frame, totalFrames, hz), 0, h),
            Math.Clamp((float)SourceZ.GetValue(frame, totalFrames, hz), 0, d),
            Math.Clamp((float)ListenerX.GetValue(frame, totalFrames, hz), 0, w),
            Math.Clamp((float)ListenerY.GetValue(frame, totalFrames, hz), 0, h),
            Math.Clamp((float)ListenerZ.GetValue(frame, totalFrames, hz), 0, d),
            (float)PreDelayMs.GetValue(frame, totalFrames, hz),
            (float)DecayTime.GetValue(frame, totalFrames, hz),
            (float)HfDamping.GetValue(frame, totalFrames, hz),
            (float)Diffusion.GetValue(frame, totalFrames, hz),
            (float)EarlyLevel.GetValue(frame, totalFrames, hz),
            (float)LateLevel.GetValue(frame, totalFrames, hz),
            (float)DryWetMix.GetValue(frame, totalFrames, hz),
            MaterialCoefficients.GetAbsorption(_wallMaterial),
            MaterialCoefficients.GetAbsorption(_floorMaterial),
            MaterialCoefficients.GetAbsorption(_ceilingMaterial),
            SpaceAudioSettings.Default.Quality,
            _roomShape,
            _wallMaterial,
            _floorMaterial,
            _ceilingMaterial,
            geometry);
    }

    public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration) =>
        new SpaceAudioProcessor(this, duration);

    public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    protected override IEnumerable<IAnimatable> GetAnimatables() =>
    [
        RoomWidth, RoomHeight, RoomDepth,
        SourceX, SourceY, SourceZ,
        ListenerX, ListenerY, ListenerZ,
        PreDelayMs, DecayTime, HfDamping, Diffusion,
        EarlyLevel, LateLevel, DryWetMix
    ];

    private string BuildLabel()
    {
        var w = RoomWidth.Values[0].Value;
        var h = RoomHeight.Values[0].Value;
        var d = RoomDepth.Values[0].Value;
        return $"{Texts.PluginName} - {w:F0}x{h:F0}x{d:F0}m RT60:{DecayTime.Values[0].Value:F1}s";
    }

    public sealed class RoomParameters(SpaceAudioEffect parent)
    {
        [Display(GroupName = nameof(Texts.GroupRoom), Name = nameof(Texts.RoomWidth), Description = nameof(Texts.RoomWidthDesc), ResourceType = typeof(Texts), Order = 100)]
        [AnimationSlider("F1", "m", 1, 50)]
        public Animation RoomWidth => parent.RoomWidth;

        [Display(GroupName = nameof(Texts.GroupRoom), Name = nameof(Texts.RoomHeight), Description = nameof(Texts.RoomHeightDesc), ResourceType = typeof(Texts), Order = 101)]
        [AnimationSlider("F1", "m", 1, 20)]
        public Animation RoomHeight => parent.RoomHeight;

        [Display(GroupName = nameof(Texts.GroupRoom), Name = nameof(Texts.RoomDepth), Description = nameof(Texts.RoomDepthDesc), ResourceType = typeof(Texts), Order = 102)]
        [AnimationSlider("F1", "m", 1, 50)]
        public Animation RoomDepth => parent.RoomDepth;

        [Display(GroupName = nameof(Texts.GroupRoom), Name = nameof(Texts.RoomShape), Description = nameof(Texts.RoomShapeDesc), ResourceType = typeof(Texts), Order = 103)]
        [EnumComboBox]
        public RoomShape RoomShapeValue { get => parent.RoomShapeValue; set => parent.RoomShapeValue = value; }

        [Display(GroupName = nameof(Texts.GroupMix), Name = nameof(Texts.DryWetMix), Description = nameof(Texts.DryWetMixDesc), ResourceType = typeof(Texts), Order = 500)]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation DryWetMix => parent.DryWetMix;
    }

    public sealed class SourceParameters(SpaceAudioEffect parent)
    {
        [Display(GroupName = nameof(Texts.GroupSource), Name = nameof(Texts.SourceX), Description = nameof(Texts.SourcePosDesc), ResourceType = typeof(Texts), Order = 200)]
        [AnimationSlider("F1", "m", 0, 50)]
        public Animation SourceX => parent.SourceX;

        [Display(GroupName = nameof(Texts.GroupSource), Name = nameof(Texts.SourceY), Description = nameof(Texts.SourcePosDesc), ResourceType = typeof(Texts), Order = 201)]
        [AnimationSlider("F1", "m", 0, 20)]
        public Animation SourceY => parent.SourceY;

        [Display(GroupName = nameof(Texts.GroupSource), Name = nameof(Texts.SourceZ), Description = nameof(Texts.SourcePosDesc), ResourceType = typeof(Texts), Order = 202)]
        [AnimationSlider("F1", "m", 0, 50)]
        public Animation SourceZ => parent.SourceZ;
    }

    public sealed class ListenerParameters(SpaceAudioEffect parent)
    {
        [Display(GroupName = nameof(Texts.GroupListener), Name = nameof(Texts.SourceX), Description = nameof(Texts.ListenerPosDesc), ResourceType = typeof(Texts), Order = 300)]
        [AnimationSlider("F1", "m", 0, 50)]
        public Animation ListenerX => parent.ListenerX;

        [Display(GroupName = nameof(Texts.GroupListener), Name = nameof(Texts.SourceY), Description = nameof(Texts.ListenerPosDesc), ResourceType = typeof(Texts), Order = 301)]
        [AnimationSlider("F1", "m", 0, 20)]
        public Animation ListenerY => parent.ListenerY;

        [Display(GroupName = nameof(Texts.GroupListener), Name = nameof(Texts.SourceZ), Description = nameof(Texts.ListenerPosDesc), ResourceType = typeof(Texts), Order = 302)]
        [AnimationSlider("F1", "m", 0, 50)]
        public Animation ListenerZ => parent.ListenerZ;
    }

    public sealed class ReverbParameters(SpaceAudioEffect parent)
    {
        [Display(GroupName = nameof(Texts.GroupReverb), Name = nameof(Texts.PreDelay), Description = nameof(Texts.PreDelayDesc), ResourceType = typeof(Texts), Order = 400)]
        [AnimationSlider("F0", "ms", 0, 200)]
        public Animation PreDelayMs => parent.PreDelayMs;

        [Display(GroupName = nameof(Texts.GroupReverb), Name = nameof(Texts.DecayTime), Description = nameof(Texts.DecayTimeDesc), ResourceType = typeof(Texts), Order = 401)]
        [AnimationSlider("F2", "s", 0.1, 10)]
        public Animation DecayTime => parent.DecayTime;

        [Display(GroupName = nameof(Texts.GroupReverb), Name = nameof(Texts.HfDamping), Description = nameof(Texts.HfDampingDesc), ResourceType = typeof(Texts), Order = 402)]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation HfDamping => parent.HfDamping;

        [Display(GroupName = nameof(Texts.GroupReverb), Name = nameof(Texts.Diffusion), Description = nameof(Texts.DiffusionDesc), ResourceType = typeof(Texts), Order = 403)]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation Diffusion => parent.Diffusion;

        [Display(GroupName = nameof(Texts.GroupReverb), Name = nameof(Texts.EarlyLevel), Description = nameof(Texts.EarlyLevelDesc), ResourceType = typeof(Texts), Order = 404)]
        [AnimationSlider("F1", "dB", -40, 6)]
        public Animation EarlyLevel => parent.EarlyLevel;

        [Display(GroupName = nameof(Texts.GroupReverb), Name = nameof(Texts.LateLevel), Description = nameof(Texts.LateLevelDesc), ResourceType = typeof(Texts), Order = 405)]
        [AnimationSlider("F1", "dB", -40, 6)]
        public Animation LateLevel => parent.LateLevel;
    }

    public sealed class MaterialParameters(SpaceAudioEffect parent)
    {
        [Display(GroupName = nameof(Texts.GroupMaterial), Name = nameof(Texts.WallMaterial), Description = nameof(Texts.WallMaterialDesc), ResourceType = typeof(Texts), Order = 600)]
        [EnumComboBox]
        public WallMaterial WallMaterialValue { get => parent.WallMaterialValue; set => parent.WallMaterialValue = value; }

        [Display(GroupName = nameof(Texts.GroupMaterial), Name = nameof(Texts.FloorMaterial), Description = nameof(Texts.FloorMaterialDesc), ResourceType = typeof(Texts), Order = 601)]
        [EnumComboBox]
        public WallMaterial FloorMaterialValue { get => parent.FloorMaterialValue; set => parent.FloorMaterialValue = value; }

        [Display(GroupName = nameof(Texts.GroupMaterial), Name = nameof(Texts.CeilingMaterial), Description = nameof(Texts.CeilingMaterialDesc), ResourceType = typeof(Texts), Order = 602)]
        [EnumComboBox]
        public WallMaterial CeilingMaterialValue { get => parent.CeilingMaterialValue; set => parent.CeilingMaterialValue = value; }
    }
}
