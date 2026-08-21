namespace MutePilot.Volume;

public sealed class VolumePresetToggleException : Exception
{
    public VolumePresetToggleException(string message, Exception innerException) :
        base(message, innerException)
    {
    }
}
