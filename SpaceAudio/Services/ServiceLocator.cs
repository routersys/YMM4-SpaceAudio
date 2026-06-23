using SpaceAudio.Interfaces;

namespace SpaceAudio.Services;

public static class ServiceLocator
{
    private static volatile IToastPresenter? _registeredPresenter;

    private sealed class NullUserNotificationService : IUserNotificationService
    {
        public static readonly NullUserNotificationService Instance = new();

        private NullUserNotificationService() { }

        public void ShowError(string message) { }
        public void ShowWarning(string message) { }
        public void ShowInfo(string message) { }
        public Task<bool> ConfirmAsync(string message, string title) => Task.FromResult(false);
        public Task<string?> PromptAsync(string message, string title, string defaultText = "") => Task.FromResult<string?>(null);
    }

    private static IUserNotificationService ResolveForFileService()
    {
        var presenter = _registeredPresenter;
        return presenter is not null ? LazyNotification.Value : NullUserNotificationService.Instance;
    }

    private static readonly Lazy<IUserNotificationService> LazyNotification =
        new(() => new UserNotificationService(
            _registeredPresenter ?? throw new InvalidOperationException("Toast presenter not registered.")));

    private static readonly Lazy<IPresetService> LazyPreset =
        new(() => new PresetService(LazyNotification.Value));

    private static readonly Lazy<IWindowThemeService> LazyTheme =
        new(() => new WindowThemeService());

    private static readonly Lazy<IResourceTracker> LazyTracker =
        new(() => new SpaceAudio.Infrastructure.ResourceTracker());

    private static readonly Lazy<IUpdateService> LazyUpdate =
        new(() => new UpdateService());

    private static readonly Lazy<IRoomGeometryService> LazyGeometry =
        new(() => new RoomGeometryService(ResolveForFileService()));

    private static readonly Lazy<IMaterialService> LazyMaterial =
        new(() => new MaterialService(ResolveForFileService()));

    private static readonly Lazy<IPlaybackTimelineService> LazyTimeline =
        new(() => new PlaybackTimelineService());

    public static void RegisterToastPresenter(IToastPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        if (Interlocked.CompareExchange(ref _registeredPresenter, presenter, null) is not null)
            throw new InvalidOperationException("Toast presenter already registered.");
    }

    public static IUserNotificationService NotificationService => LazyNotification.Value;
    public static IPresetService PresetService => LazyPreset.Value;
    public static IWindowThemeService WindowThemeService => LazyTheme.Value;
    public static IResourceTracker ResourceTracker => LazyTracker.Value;
    public static IUpdateService UpdateService => LazyUpdate.Value;
    public static IRoomGeometryService GeometryService => LazyGeometry.Value;
    public static IMaterialService MaterialService => LazyMaterial.Value;
    public static IPlaybackTimelineService TimelineService => LazyTimeline.Value;
}
