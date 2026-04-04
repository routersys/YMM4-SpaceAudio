using SpaceAudio.Interfaces;
using SpaceAudio.Services;

namespace SpaceAudio.ViewModels;

public sealed class TimelineViewModel : ViewModelBase
{
    private readonly IPlaybackTimelineService _service;
    private bool _isUserSeeking;
    private bool _isPinned;
    private long _displayFrame;
    private long _pinnedFrame;

    public TimelineViewModel() : this(ServiceLocator.TimelineService) { }

    public TimelineViewModel(IPlaybackTimelineService service)
    {
        _service = service;
    }

    public IPlaybackTimelineService Service => _service;

    public bool IsUserSeeking => _isUserSeeking;

    public long DisplayFrame
    {
        get
        {
            if (_isUserSeeking) return _displayFrame;
            if (_isPinned) return _pinnedFrame;
            return _service.CurrentFrame;
        }
    }

    public void BeginSeek(long frame)
    {
        _isPinned = false;
        _displayFrame = frame;
        _isUserSeeking = true;
    }

    public void UpdateSeek(long frame)
    {
        _displayFrame = frame;
    }

    public void EndSeek()
    {
        _pinnedFrame = _displayFrame;
        _isPinned = true;
        _isUserSeeking = false;
    }

    public void ReleasePin()
    {
        _isPinned = false;
    }
}
