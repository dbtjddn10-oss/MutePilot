namespace MutePilot.Settings;

public interface ISettingsService
{
    string SettingsFilePath { get; }

    SettingsLoadResult Load();

    void Save(AppSettings settings);
}

public sealed record SettingsLoadResult(AppSettings Settings, string? WarningMessage);
