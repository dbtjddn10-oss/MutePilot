namespace MutePilot.Volume;

public sealed record ApplicationVolumeSnapshot(
    string ApplicationKey,
    IReadOnlyList<ApplicationSessionVolumeSnapshot> Sessions);

public sealed record ApplicationSessionVolumeSnapshot(
    string SessionInstanceId,
    int ProcessId,
    float VolumeScalar,
    bool IsMuted)
{
    public int VolumePercent => VolumeSnapshotMath.ToPercent(VolumeScalar);
}
