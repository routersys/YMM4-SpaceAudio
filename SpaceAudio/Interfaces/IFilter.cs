namespace SpaceAudio.Interfaces;

internal interface IFilter : IDisposable
{
    void Reset();
    float Process(float input);
}
