namespace SpaceAudio.Interfaces;

public interface IToastHandle
{
    event EventHandler Closed;
    void AnimateTop(double top);
}
