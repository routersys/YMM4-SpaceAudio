namespace SpaceAudio.Interfaces;

public interface IUpdateService
{
    Task<(bool HasUpdate, string UpdateUrl)> CheckForUpdatesAsync();
}
