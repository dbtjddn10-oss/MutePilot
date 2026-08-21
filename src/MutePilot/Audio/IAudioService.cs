namespace MutePilot.Audio;

public interface IAudioService
{
    bool GetMasterMuteState();

    void SetMasterMuteState(bool isMuted);

    bool ToggleMasterMuteState();

    IReadOnlyList<ApplicationAudioSession> GetActiveApplicationSessions();

    ApplicationAudioSession ToggleApplicationMute(string applicationKey);
}
