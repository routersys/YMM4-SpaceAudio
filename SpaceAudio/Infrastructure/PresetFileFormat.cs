using Newtonsoft.Json;
using SpaceAudio.Models;
using System.IO;
using System.Text;

namespace SpaceAudio.Infrastructure;

internal static class PresetFileFormat
{
    private static ReadOnlySpan<byte> Magic => "SARP"u8;
    private const ushort FormatVersion = 1;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.None
    };

    public static byte[] Serialize(RoomConfiguration config)
    {
        string json = JsonConvert.SerializeObject(config, JsonSettings);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        uint checksum = Crc32.Compute(jsonBytes);

        int capacity = Magic.Length + sizeof(ushort) + sizeof(int) + sizeof(uint) + jsonBytes.Length;
        using var output = new MemoryStream(capacity);
        output.Write(Magic);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(FormatVersion);
        writer.Write(jsonBytes.Length);
        writer.Write(checksum);
        output.Write(jsonBytes);
        return output.ToArray();
    }

    public static RoomConfiguration? Deserialize(byte[] fileData)
    {
        if (fileData is null || fileData.Length == 0) return null;
        return IsNewFormat(fileData) ? DeserializeNew(fileData) : DeserializeLegacy(fileData);
    }

    private static bool IsNewFormat(ReadOnlySpan<byte> data) =>
        data.Length >= Magic.Length && data[..Magic.Length].SequenceEqual(Magic);

    private static RoomConfiguration? DeserializeNew(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
            reader.ReadBytes(Magic.Length);
            ushort version = reader.ReadUInt16();
            if (version > FormatVersion) return null;
            int jsonLength = reader.ReadInt32();
            uint storedChecksum = reader.ReadUInt32();
            if (ms.Position + jsonLength > data.Length) return null;
            byte[] jsonBytes = reader.ReadBytes(jsonLength);
            if (Crc32.Compute(jsonBytes) != storedChecksum) return null;
            string json = Encoding.UTF8.GetString(jsonBytes);
            return JsonConvert.DeserializeObject<RoomConfiguration>(json);
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    private static RoomConfiguration? DeserializeLegacy(byte[] data)
    {
        try
        {
            string json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<RoomConfiguration>(json);
        }
        catch (JsonException) { return null; }
        catch (DecoderFallbackException) { return null; }
    }
}
