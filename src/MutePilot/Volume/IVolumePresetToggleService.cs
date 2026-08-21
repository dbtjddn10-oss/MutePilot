using MutePilot.Audio;

namespace MutePilot.Volume;

public interface IVolumePresetToggleService
{
    bool IsMasterPresetActive { get; }

    bool IsApplicationPresetActive(string applicationKey);

    VolumePresetToggleResult ToggleMaster(int presetPercent);

    VolumePresetToggleResult ToggleApplication(string applicationKey, int presetPercent);

    bool InvalidateStaleMasterBaseline(string currentDeviceId);

    bool InvalidateStaleApplicationBaselines(
        IReadOnlyList<ApplicationAudioSession> activeSessions);

    void Clear();
}

public sealed record VolumePresetToggleResult(
    bool IsPresetActive,
    bool RestoredBaseline);
