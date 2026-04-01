using Newtonsoft.Json;
using SpaceAudio.Infrastructure;
using SpaceAudio.Interfaces;
using SpaceAudio.Models;
using System.IO;
using System.Reflection;
using System.Text;

namespace SpaceAudio.Services;

public sealed class RoomGeometryService : IRoomGeometryService
{
    private const string Extension = ".srg";
    private readonly string _dir;
    private readonly IUserNotificationService _notifications;
    private readonly Lock _lock = new();
    private volatile bool _initialized;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    public event EventHandler? GeometriesChanged;

    public RoomGeometryService(IUserNotificationService notifications)
    {
        _notifications = notifications;
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        _dir = Path.Combine(assemblyDir, "geometries");
    }

    public IReadOnlyList<string> GetAllIds()
    {
        EnsureInit();
        if (!Directory.Exists(_dir)) return [];
        return Directory.GetFiles(_dir, "*" + Extension)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public RoomGeometry? Load(string id)
    {
        EnsureInit();
        string path = FilePath(id);
        byte[]? data = AtomicFileOperations.ReadWithFallback(path);
        if (data is null) return null;
        try
        {
            string json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<RoomGeometry>(json, JsonSettings);
        }
        catch { return null; }
    }

    public bool Save(RoomGeometry geometry)
    {
        EnsureInit();
        if (string.IsNullOrWhiteSpace(geometry.ShapeId)) return false;
        try
        {
            string json = JsonConvert.SerializeObject(geometry, JsonSettings);
            byte[] data = Encoding.UTF8.GetBytes(json);
            AtomicFileOperations.Write(FilePath(geometry.ShapeId), data);
            GeometriesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch { return false; }
    }

    public void Delete(string id)
    {
        EnsureInit();
        string path = FilePath(id);
        if (!File.Exists(path)) return;
        try { File.Delete(path); }
        catch { return; }
        GeometriesChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Rename(string oldId, string newId)
    {
        EnsureInit();
        if (string.IsNullOrWhiteSpace(newId)) return false;
        string oldPath = FilePath(oldId);
        string newPath = FilePath(newId);
        if (File.Exists(newPath) || !File.Exists(oldPath)) return false;
        try { File.Move(oldPath, newPath); }
        catch { return false; }
        GeometriesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void EnsureInit()
    {
        if (_initialized) return;
        lock (_lock) { if (_initialized) return; Directory.CreateDirectory(_dir); _initialized = true; }
    }

    private string FilePath(string id) => Path.Combine(_dir, id + Extension);
}
