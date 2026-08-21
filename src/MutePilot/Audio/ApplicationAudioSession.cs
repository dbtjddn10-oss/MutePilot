namespace MutePilot.Audio;

public sealed record ApplicationAudioSession(
    string ApplicationKey,
    string ApplicationName,
    IReadOnlyList<int> ProcessIds,
    bool IsMuted,
    bool HasMixedMuteState,
    int SessionCount);
