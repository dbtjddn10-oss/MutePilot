namespace MutePilot.Security;

public interface IPrivilegeService
{
    bool IsElevated { get; }

    ElevationRestartResult RestartAsAdministrator(bool startInBackground);
}

public enum ElevationRestartOutcome
{
    Started,
    AlreadyElevated,
    Cancelled,
    Failed
}

public sealed record ElevationRestartResult(
    ElevationRestartOutcome Outcome,
    string? Message = null);
