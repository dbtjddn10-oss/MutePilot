namespace MutePilot.Audio;

public interface IAudioService
{
    bool GetMasterMuteState();

    void SetMasterMuteState(bool isMuted);

    bool ToggleMasterMuteState();

    int GetMasterVolumePercent();

    int SetMasterVolumePercent(int percent);

    IReadOnlyList<ApplicationAudioSession> GetActiveApplicationSessions();

    ApplicationAudioSession ToggleApplicationMute(string applicationKey);

    ApplicationAudioSession SetApplicationVolumePercent(string applicationKey, int percent);
}
