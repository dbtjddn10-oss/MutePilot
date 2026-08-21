using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MutePilot.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public SettingsService()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        SettingsFilePath = Path.Combine(
            localApplicationData,
            "MutePilot",
            "settings.json");
    }

    public string SettingsFilePath { get; }

    public SettingsLoadResult Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            var defaultSettings = new AppSettings();

            try
            {
                Save(defaultSettings);
                return new SettingsLoadResult(defaultSettings, null);
            }
            catch (SettingsServiceException exception)
            {
                Debug.WriteLine(exception);
                return new SettingsLoadResult(
                    defaultSettings,
                    "설정 파일을 만들 수 없습니다. 단축키 설정이 저장되지 않을 수 있습니다.");
            }
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ??
                new AppSettings();
            settings.ApplicationBindings ??= [];
            var bindingCount = settings.ApplicationBindings.Count;
            settings.ApplicationBindings = settings.ApplicationBindings
                .Where(setting =>
                    setting is not null &&
                    !string.IsNullOrWhiteSpace(setting.ProcessName) &&
                    setting.Hotkey is not null)
                .ToList();

            var warningMessage = settings.ApplicationBindings.Count == bindingCount
                ? null
                : "내용이 올바르지 않은 앱 단축키 설정은 건너뛰었습니다. 기존 파일은 변경하지 않았습니다.";
            return new SettingsLoadResult(settings, warningMessage);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine(exception);
            return new SettingsLoadResult(
                new AppSettings(),
                "설정 파일을 읽을 수 없어 기본 설정으로 시작했습니다. 기존 파일은 변경하지 않았습니다.");
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var settingsDirectory = Path.GetDirectoryName(SettingsFilePath) ??
            throw new InvalidOperationException("Settings directory is unavailable.");
        var temporaryFilePath = Path.Combine(
            settingsDirectory,
            $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(settingsDirectory);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(temporaryFilePath, json);
            File.Move(temporaryFilePath, SettingsFilePath, true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine(exception);
            throw new SettingsServiceException("단축키 설정을 저장하지 못했습니다.", exception);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                try
                {
                    File.Delete(temporaryFilePath);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }
        }
    }
}
