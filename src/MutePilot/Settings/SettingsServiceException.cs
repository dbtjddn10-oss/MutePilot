namespace MutePilot.Settings;

public sealed class SettingsServiceException : Exception
{
    public SettingsServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
