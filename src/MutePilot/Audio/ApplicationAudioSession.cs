namespace MutePilot.Audio;

public sealed record ApplicationAudioSession(
    string ApplicationKey,
    string ApplicationName,
    IReadOnlyList<int> ProcessIds,
    IReadOnlyList<string> SessionInstanceIds,
    bool IsMuted,
    bool HasMixedMuteState,
    int VolumePercent,
    bool HasMixedVolume,
    int SessionCount);
