namespace SpaceAudio.ViewModels;

public sealed class FaceVertexMembership : ViewModelBase
{
    private bool _isInFace;
    private int _faceOrder = -1;

    public required int VertexIndex { get; init; }
    public required string VertexLabel { get; init; }

    public bool IsInFace
    {
        get => _isInFace;
        set => SetProperty(ref _isInFace, value, () => OnPropertyChanged(nameof(FaceOrderLabel)));
    }

    public int FaceOrder
    {
        get => _faceOrder;
        set => SetProperty(ref _faceOrder, value, () => OnPropertyChanged(nameof(FaceOrderLabel)));
    }

    public string FaceOrderLabel => _isInFace && _faceOrder >= 0 ? $"#{_faceOrder + 1}" : "";
}

public sealed class VertexItem(int index, float x, float y, float z) : ViewModelBase
{
    private float _x = x;
    private float _y = y;
    private float _z = z;

    public int Index { get; } = index;
    public float X { get => _x; set => SetProperty(ref _x, value); }
    public float Y { get => _y; set => SetProperty(ref _y, value); }
    public float Z { get => _z; set => SetProperty(ref _z, value); }

    public override string ToString() => $"V{Index}  ({_x:F2}, {_y:F2}, {_z:F2})";
}

public sealed record FaceItem(int Index, string Indices, string Material)
{
    public override string ToString() => $"F{Index}  [{Indices}]  {Material}";
}
