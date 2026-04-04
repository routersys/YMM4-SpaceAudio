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

    public WallMaterial WallMaterialValue { get => _wallMaterial; set { if (Set(ref _wallMaterial, value)) TryMigrateMaterial(value, ref _wallMaterialId); } }
    private WallMaterial _wallMaterial = WallMaterial.Drywall;

    public WallMaterial FloorMaterialValue { get => _floorMaterial; set { if (Set(ref _floorMaterial, value)) TryMigrateMaterial(value, ref _floorMaterialId); } }
    private WallMaterial _floorMaterial = WallMaterial.Wood;

    public WallMaterial CeilingMaterialValue { get => _ceilingMaterial; set { if (Set(ref _ceilingMaterial, value)) TryMigrateMaterial(value, ref _ceilingMaterialId); } }
    private WallMaterial _ceilingMaterial = WallMaterial.Drywall;

    public string WallMaterialId { get => _wallMaterialId; set => Set(ref _wallMaterialId, value); }
    private string _wallMaterialId = "drywall";

    public string FloorMaterialId { get => _floorMaterialId; set => Set(ref _floorMaterialId, value); }
    private string _floorMaterialId = "wood";

    public string CeilingMaterialId { get => _ceilingMaterialId; set => Set(ref _ceilingMaterialId, value); }
    private string _ceilingMaterialId = "drywall";

    private void TryMigrateMaterial(WallMaterial enumValue, ref string targetId)
    {
        string newId = enumValue.ToString().ToLowerInvariant();
        if (newId == "acousticpanel") newId = "acoustic";
        if (targetId != newId && string.IsNullOrEmpty(targetId))
            targetId = newId;
    }

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

    public RoomGeometry? ResolveScaledGeometry(float w, float h, float d)
    {
        var geo = ResolveGeometry();
        if (geo is not null && _roomShape == RoomShape.Custom)
        {
            return geo.CloneAndScale(w, h, d);
        }
        return geo;
    }

    private static (float Absorption, float SpectralDamping) ResolveMaterialProperties(string materialId, WallMaterial fallback)
    {
        var mat = Services.ServiceLocator.MaterialService.GetById(materialId);
        if (mat is not null)
        {
            float abs = mat.Absorption;
            float sd = mat.SpectralDamping;
            if (mat.BandAbsorption is { Length: >= MaterialCoefficients.BandCount })
            {
                abs = MaterialCoefficients.ComputeBroadband(mat.BandAbsorption);
                sd = MaterialCoefficients.ComputeSpectralDamping(mat.BandAbsorption);
            }
            return (abs, sd);
        }

        var bands = MaterialCoefficients.GetBandAbsorption(fallback);
        return (MaterialCoefficients.GetAbsorption(fallback), MaterialCoefficients.ComputeSpectralDamping(bands));
    }

    public RoomSnapshot CreateSnapshot(long frame, long totalFrames, int hz)
    {
        float w = (float)RoomWidth.GetValue(frame, totalFrames, hz);
        float h = (float)RoomHeight.GetValue(frame, totalFrames, hz);
        float d = (float)RoomDepth.GetValue(frame, totalFrames, hz);

        var geometry = ResolveScaledGeometry(w, h, d);

        float sx = (float)SourceX.GetValue(frame, totalFrames, hz);
        float sy = (float)SourceY.GetValue(frame, totalFrames, hz);
        float sz = (float)SourceZ.GetValue(frame, totalFrames, hz);
        float lx = (float)ListenerX.GetValue(frame, totalFrames, hz);
        float ly = (float)ListenerY.GetValue(frame, totalFrames, hz);
        float lz = (float)ListenerZ.GetValue(frame, totalFrames, hz);

        var (wallAbs, wallSD) = ResolveMaterialProperties(_wallMaterialId, _wallMaterial);
        var (floorAbs, floorSD) = ResolveMaterialProperties(_floorMaterialId, _floorMaterial);
        var (ceilAbs, ceilSD) = ResolveMaterialProperties(_ceilingMaterialId, _ceilingMaterial);

        if (geometry is null && _roomShape != RoomShape.Rectangular)
        {
            geometry = RoomGeometry.FromShape(_roomShape, w, h, d, wallAbs, floorAbs, ceilAbs);
            UpdateGeometryMaterialSpectralData(geometry, wallAbs, wallSD, floorAbs, floorSD, ceilAbs, ceilSD);
        }

        if (geometry is not null)
        {
            var sc = geometry.ClampToPolygonXZ(sx, sz);
            sx = sc.X; sz = sc.Z;
            var lc = geometry.ClampToPolygonXZ(lx, lz);
            lx = lc.X; lz = lc.Z;
            var syBounds = geometry.GetYBoundsAtXZ(sx, sz, 0, h);
            sy = Math.Clamp(sy, syBounds.MinY, syBounds.MaxY);
            var lyBounds = geometry.GetYBoundsAtXZ(lx, lz, 0, h);
            ly = Math.Clamp(ly, lyBounds.MinY, lyBounds.MaxY);
        }
        else
        {
            sx = Math.Clamp(sx, 0, w);
            sz = Math.Clamp(sz, 0, d);
            lx = Math.Clamp(lx, 0, w);
            lz = Math.Clamp(lz, 0, d);
            sy = Math.Clamp(sy, 0, h);
            ly = Math.Clamp(ly, 0, h);
        }

        return new RoomSnapshot(
            w, h, d,
            sx, sy, sz,
            lx, ly, lz,
            (float)PreDelayMs.GetValue(frame, totalFrames, hz),
            (float)DecayTime.GetValue(frame, totalFrames, hz),
            (float)HfDamping.GetValue(frame, totalFrames, hz),
            (float)Diffusion.GetValue(frame, totalFrames, hz),
            (float)EarlyLevel.GetValue(frame, totalFrames, hz),
            (float)LateLevel.GetValue(frame, totalFrames, hz),
            (float)DryWetMix.GetValue(frame, totalFrames, hz),
            wallAbs, floorAbs, ceilAbs,
            wallSD, floorSD, ceilSD,
            SpaceAudioSettings.Default.Quality,
            _roomShape, _wallMaterialId, _floorMaterialId, _ceilingMaterialId,
            geometry);
    }

    private static void UpdateGeometryMaterialSpectralData(RoomGeometry geometry,
        float wallAbs, float wallSD, float floorAbs, float floorSD, float ceilAbs, float ceilSD)
    {
        if (geometry.Materials.Length == 0) return;

        foreach (var mat in geometry.Materials)
        {
            string name = mat.Name.ToLowerInvariant();
            if (name.Contains("floor"))
            {
                mat.Absorption = floorAbs;
                mat.BandAbsorption = GenerateSyntheticBands(floorAbs, floorSD);
            }
            else if (name.Contains("ceil"))
            {
                mat.Absorption = ceilAbs;
                mat.BandAbsorption = GenerateSyntheticBands(ceilAbs, ceilSD);
            }
            else
            {
                mat.Absorption = wallAbs;
                mat.BandAbsorption = GenerateSyntheticBands(wallAbs, wallSD);
            }
        }

        geometry.Invalidate();
    }

    private static float[] GenerateSyntheticBands(float broadband, float spectralDamping)
    {
        float ratio = (1.0f - spectralDamping) / (1.0f + spectralDamping);
        ratio = Math.Clamp(ratio, 0.01f, 1.0f);

        float lowAbs = broadband * (2.0f / (1.0f + ratio));
        float highAbs = lowAbs * ratio;

        return
        [
            Math.Clamp(lowAbs * 0.9f, 0.0f, 0.99f),
            Math.Clamp(lowAbs, 0.0f, 0.99f),
            Math.Clamp(broadband, 0.0f, 0.99f),
            Math.Clamp(broadband * 1.05f, 0.0f, 0.99f),
            Math.Clamp(highAbs * 1.1f, 0.0f, 0.99f),
            Math.Clamp(highAbs, 0.0f, 0.99f)
        ];
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

    public sealed class RoomParameters : System.ComponentModel.INotifyPropertyChanged
    {
        private readonly SpaceAudioEffect parent;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public RoomParameters(SpaceAudioEffect parent)
        {
            this.parent = parent;
            parent.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(parent.RoomShapeValue))
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RoomShapeValue)));
            };
        }

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
        public SpaceAudioEffect Effect => parent;

        [Display(GroupName = nameof(Texts.GroupMaterial), Name = nameof(Texts.WallMaterial), Description = nameof(Texts.WallMaterialDesc), ResourceType = typeof(Texts), Order = 600)]
        [WallMaterialComboBox]
        public string WallMaterialId { get => parent.WallMaterialId; set => parent.WallMaterialId = value; }

        [Display(GroupName = nameof(Texts.GroupMaterial), Name = nameof(Texts.FloorMaterial), Description = nameof(Texts.FloorMaterialDesc), ResourceType = typeof(Texts), Order = 601)]
        [FloorMaterialComboBox]
        public string FloorMaterialId { get => parent.FloorMaterialId; set => parent.FloorMaterialId = value; }

        [Display(GroupName = nameof(Texts.GroupMaterial), Name = nameof(Texts.CeilingMaterial), Description = nameof(Texts.CeilingMaterialDesc), ResourceType = typeof(Texts), Order = 602)]
        [CeilingMaterialComboBox]
        public string CeilingMaterialId { get => parent.CeilingMaterialId; set => parent.CeilingMaterialId = value; }
    }
}
