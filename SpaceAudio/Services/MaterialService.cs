using Newtonsoft.Json;
using SpaceAudio.Enums;
using SpaceAudio.Infrastructure;
using SpaceAudio.Interfaces;
using SpaceAudio.Models;
using System.IO;
using System.Reflection;
using System.Text;

namespace SpaceAudio.Services;

public sealed class MaterialService : IMaterialService
{
    private const string FileName = "materials.json";
    private readonly string _filePath;
    private readonly IUserNotificationService _notifications;
    private readonly Lock _lock = new();
    private List<CustomMaterial>? _cache;
    private volatile bool _initialized;

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

    public IReadOnlyList<CustomMaterial> GetAll()
    {
        EnsureInit();
        lock (_lock) return [.. GetBuiltIn(), .. (_cache ?? [])];
    }

    public IReadOnlyList<CustomMaterial> GetBuiltIn()
    {
        return
        [
            new("concrete", "Concrete", MaterialCoefficients.GetAbsorption(WallMaterial.Concrete), true),
            new("wood", "Wood", MaterialCoefficients.GetAbsorption(WallMaterial.Wood), true),
            new("glass", "Glass", MaterialCoefficients.GetAbsorption(WallMaterial.Glass), true),
            new("carpet", "Carpet", MaterialCoefficients.GetAbsorption(WallMaterial.Carpet), true),
            new("acoustic", "Acoustic Panel", MaterialCoefficients.GetAbsorption(WallMaterial.AcousticPanel), true),
            new("brick", "Brick", MaterialCoefficients.GetAbsorption(WallMaterial.Brick), true),
            new("drywall", "Drywall", MaterialCoefficients.GetAbsorption(WallMaterial.Drywall), true),
            new("tile", "Tile", MaterialCoefficients.GetAbsorption(WallMaterial.Tile), true)
        ];
    }

    public CustomMaterial? GetById(string id)
    {
        EnsureInit();
        foreach (var m in GetBuiltIn()) if (m.Id == id) return m;
        lock (_lock) return _cache?.FirstOrDefault(m => m.Id == id);
    }

    public bool Save(CustomMaterial material)
    {
        EnsureInit();
        if (material.IsBuiltIn) return false;
        lock (_lock)
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
        EnsureInit();
        lock (_lock)
        {
            _cache?.RemoveAll(m => m.Id == id);
            Persist();
        }
        MaterialsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureInit()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            byte[]? data = AtomicFileOperations.ReadWithFallback(_filePath);
            if (data is { Length: > 0 })
            {
                try
                {
                    string json = Encoding.UTF8.GetString(data);
                    _cache = JsonConvert.DeserializeObject<List<CustomMaterial>>(json, JsonSettings) ?? [];
                }
                catch { _cache = []; }
            }
            else _cache = [];
            _initialized = true;
        }
    }

    private void Persist()
    {
        try
        {
            string json = JsonConvert.SerializeObject(_cache ?? [], JsonSettings);
            AtomicFileOperations.Write(_filePath, Encoding.UTF8.GetBytes(json));
        }
        catch { }
    }
}
