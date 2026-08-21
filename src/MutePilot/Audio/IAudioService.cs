using MutePilot.Volume;

namespace MutePilot.Audio;

public interface IAudioService
{
    bool GetMasterMuteState();

    void SetMasterMuteState(bool isMuted);

    bool ToggleMasterMuteState();

    int GetMasterVolumePercent();

    int SetMasterVolumePercent(int percent);

    MasterVolumeSnapshot CaptureMasterVolumeSnapshot();

    MasterVolumeSnapshot ApplyMasterVolumePreset(
        MasterVolumeSnapshot snapshot,
        int percent);

    MasterVolumeSnapshot RestoreMasterVolumeSnapshot(MasterVolumeSnapshot snapshot);

    IReadOnlyList<ApplicationAudioSession> GetActiveApplicationSessions();

    ApplicationAudioSession ToggleApplicationMute(string applicationKey);

    ApplicationAudioSession SetApplicationVolumePercent(string applicationKey, int percent);

    ApplicationVolumeSnapshot CaptureApplicationVolumeSnapshot(string applicationKey);

    ApplicationAudioSession ApplyApplicationVolumePreset(
        ApplicationVolumeSnapshot snapshot,
        int percent);

    ApplicationAudioSession RestoreApplicationVolumeSnapshot(
        ApplicationVolumeSnapshot snapshot);
}
