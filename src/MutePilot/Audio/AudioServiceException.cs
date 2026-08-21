namespace MutePilot.Audio;

public sealed class AudioServiceException : Exception
{
    public AudioServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
