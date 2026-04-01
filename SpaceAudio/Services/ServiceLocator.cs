using SpaceAudio.Interfaces;

namespace SpaceAudio.Services;

public static class ServiceLocator
{
    private static volatile IToastPresenter? _registeredPresenter;

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
        new(() => new RoomGeometryService(LazyNotification.Value));

    private static readonly Lazy<IMaterialService> LazyMaterial =
        new(() => new MaterialService(LazyNotification.Value));

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
}
