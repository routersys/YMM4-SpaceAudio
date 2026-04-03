namespace SpaceAudio.Audio.Bvh;

internal sealed class BvhNode
{
    public AabbBox Bounds;
    public BvhNode? Left;
    public BvhNode? Right;
    public int FaceIndex = -1;

    public bool IsLeaf => Left is null;
}
