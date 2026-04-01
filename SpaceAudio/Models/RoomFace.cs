namespace SpaceAudio.Models;

public sealed class RoomFace
{
    public int[] VertexIndices { get; set; } = [];
    public int MaterialIndex { get; set; }

    public RoomFace() { }

    public RoomFace(int[] vertexIndices, int materialIndex = 0)
    {
        VertexIndices = vertexIndices;
        MaterialIndex = materialIndex;
    }

    public RoomFace Clone() => new()
    {
        VertexIndices = (int[])VertexIndices.Clone(),
        MaterialIndex = MaterialIndex
    };
}
