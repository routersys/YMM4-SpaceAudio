using Newtonsoft.Json;
using SpaceAudio.Enums;
using System.Windows.Media;
using SpaceAudio.Infrastructure;
using SpaceAudio.Interfaces;
using SpaceAudio.Localization;
using SpaceAudio.Models;
using System.IO;
using System.Reflection;
using System.Text;

namespace SpaceAudio.Services;

internal sealed class MaterialService : FileBackedServiceBase, IMaterialService
{
    private const string FileName = "materials.json";
    private readonly string _filePath;
    private readonly IUserNotificationService _notifications;
    private List<CustomMaterial>? _cache;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };

    public event EventHandler? MaterialsChanged;

    public MaterialService(IUserNotificationService notifications)
    {
        _notifications = notifications;
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        _filePath = Path.Combine(assemblyDir, FileName);
    }

    protected override string DirectoryPath =>
        Path.GetDirectoryName(_filePath) ?? "";

    protected override void OnInitializing()
    {
        byte[]? data = AtomicFileOperations.ReadWithFallback(_filePath);
        if (data is { Length: > 0 })
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                _cache = JsonConvert.DeserializeObject<List<CustomMaterial>>(json, JsonSettings) ?? [];
            }
            catch (JsonException ex)
            {
                _notifications.ShowWarning($"{Texts.PresetLoadFailed}: {ex.Message}");
                _cache = [];
            }
        }
        else
        {
            _cache = [];
        }
    }

    public IReadOnlyList<CustomMaterial> GetAll()
    {
        EnsureInitialized();
        lock (SyncRoot) return [.. GetBuiltIn(), .. (_cache ?? [])];
    }

    public IReadOnlyList<CustomMaterial> GetBuiltIn() =>
    [
        new("concrete", Texts.MaterialConcrete, MaterialCoefficients.GetAbsorption(WallMaterial.Concrete), true, Color.FromRgb(150, 155, 160)),
        new("wood", Texts.MaterialWood, MaterialCoefficients.GetAbsorption(WallMaterial.Wood), true, Color.FromRgb(170, 100, 50)),
        new("glass", Texts.MaterialGlass, MaterialCoefficients.GetAbsorption(WallMaterial.Glass), true, Color.FromRgb(150, 220, 255)),
        new("carpet", Texts.MaterialCarpet, MaterialCoefficients.GetAbsorption(WallMaterial.Carpet), true, Color.FromRgb(180, 60, 60)),
        new("acoustic", Texts.MaterialAcousticPanel, MaterialCoefficients.GetAbsorption(WallMaterial.AcousticPanel), true, Color.FromRgb(80, 85, 95)),
        new("brick", Texts.MaterialBrick, MaterialCoefficients.GetAbsorption(WallMaterial.Brick), true, Color.FromRgb(190, 70, 50)),
        new("drywall", Texts.MaterialDrywall, MaterialCoefficients.GetAbsorption(WallMaterial.Drywall), true, Color.FromRgb(220, 225, 230)),
        new("tile", Texts.MaterialTile, MaterialCoefficients.GetAbsorption(WallMaterial.Tile), true, Color.FromRgb(210, 230, 235))
    ];

    public CustomMaterial? GetById(string id)
    {
        EnsureInitialized();
        foreach (var m in GetBuiltIn())
            if (m.Id == id) return m;
        lock (SyncRoot) return _cache?.Find(m => m.Id == id);
    }

    public bool Save(CustomMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        EnsureInitialized();
        if (material.IsBuiltIn) return false;
        lock (SyncRoot)
        {
            _cache ??= [];
            int idx = _cache.FindIndex(m => m.Id == material.Id);
            if (idx >= 0) _cache[idx] = material;
            else _cache.Add(material);
            Persist();
        }
        MaterialsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Delete(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        EnsureInitialized();
        lock (SyncRoot)
        {
            _cache?.RemoveAll(m => m.Id == id);
            Persist();
        }
        MaterialsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Persist()
    {
        try
        {
            string json = JsonConvert.SerializeObject(_cache ?? [], JsonSettings);
            AtomicFileOperations.Write(_filePath, Encoding.UTF8.GetBytes(json));
        }
        catch (IOException ex)
        {
            _notifications.ShowWarning($"{Texts.PresetSaveFailed}: {ex.Message}");
        }
    }
}
