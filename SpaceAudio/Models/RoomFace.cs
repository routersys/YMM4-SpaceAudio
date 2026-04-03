namespace SpaceAudio.Models;

public sealed record RoomFace
{
    public int[] VertexIndices { get; set; } = [];
    public int MaterialIndex { get; set; }

    public RoomFace() { }

    public RoomFace(int[] vertexIndices, int materialIndex = 0)
    {
        VertexIndices = vertexIndices;
        MaterialIndex = materialIndex;
    }

    public RoomFace DeepClone() => this with { VertexIndices = (int[])VertexIndices.Clone() };
}
