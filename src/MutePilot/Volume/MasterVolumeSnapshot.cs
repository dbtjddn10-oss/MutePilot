namespace MutePilot.Volume;

public sealed record MasterVolumeSnapshot(
    string DeviceId,
    float VolumeScalar,
    bool IsMuted)
{
    public int VolumePercent => VolumeSnapshotMath.ToPercent(VolumeScalar);
}

internal static class VolumeSnapshotMath
{
    public static int ToPercent(float scalar) =>
        Math.Clamp((int)Math.Round(scalar * 100, MidpointRounding.AwayFromZero), 0, 100);
}
