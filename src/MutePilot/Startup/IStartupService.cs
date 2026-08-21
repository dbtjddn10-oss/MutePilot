namespace MutePilot.Startup;

public interface IStartupService
{
    StartupStatus GetStatus();

    Task<StartupChangeResult> SetEnabledAsync(bool isEnabled);
}
