using Newtonsoft.Json;
using SpaceAudio.Infrastructure;
using SpaceAudio.Interfaces;
using SpaceAudio.Models;
using System.IO;
using System.Reflection;
using System.Text;

namespace SpaceAudio.Services;

internal sealed class RoomGeometryService : FileBackedServiceBase, IRoomGeometryService
{
    private const string Extension = ".srg";
    private readonly string _dir;
    private readonly IUserNotificationService _notifications;

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

    protected override string DirectoryPath => _dir;

    public IReadOnlyList<string> GetAllIds()
    {
        EnsureInitialized();
        if (!Directory.Exists(_dir)) return [];
        return [.. Directory.GetFiles(_dir, "*" + Extension)
            .Select(p => Path.GetFileNameWithoutExtension(p)!)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    public RoomGeometry? Load(string id)
    {
        EnsureInitialized();
        byte[]? data = AtomicFileOperations.ReadWithFallback(FilePath(id));
        if (data is null) return null;
        try
        {
            string json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<RoomGeometry>(json, JsonSettings);
        }
        catch (JsonException) { return null; }
        catch (DecoderFallbackException) { return null; }
    }

    public bool Save(RoomGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(geometry.ShapeId)) return false;
        try
        {
            string json = JsonConvert.SerializeObject(geometry, JsonSettings);
            byte[] data = Encoding.UTF8.GetBytes(json);
            AtomicFileOperations.Write(FilePath(geometry.ShapeId), data);
            GeometriesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (IOException) { return false; }
        catch (JsonException) { return false; }
    }

    public void Delete(string id)
    {
        EnsureInitialized();
        string path = FilePath(id);
        if (!File.Exists(path)) return;
        try { File.Delete(path); }
        catch (IOException) { return; }
        GeometriesChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Rename(string oldId, string newId)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(newId)) return false;
        string oldPath = FilePath(oldId);
        string newPath = FilePath(newId);
        if (File.Exists(newPath) || !File.Exists(oldPath)) return false;
        try { File.Move(oldPath, newPath); }
        catch (IOException) { return false; }
        GeometriesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private string FilePath(string id) => Path.Combine(_dir, id + Extension);
}
