using System.Runtime.CompilerServices;

namespace SpaceAudio.Infrastructure;

internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320u : c >> 1;
            table[i] = c;
        }
        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        ref uint tableRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(Table);
        for (int i = 0; i < data.Length; i++)
            crc = (crc >> 8) ^ Unsafe.Add(ref tableRef, (int)((crc ^ data[i]) & 0xFF));
        return crc ^ 0xFFFFFFFFu;
    }
}
