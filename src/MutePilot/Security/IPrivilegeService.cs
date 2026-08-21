namespace MutePilot.Security;

public interface IPrivilegeService
{
    bool IsElevated { get; }

    ElevationRestartResult RestartAsAdministrator(bool startInBackground);

    StandardRestartResult RestartAsStandardUser(bool startInBackground);
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

public enum StandardRestartOutcome
{
    Started,
    AlreadyStandard,
    Failed
}

public sealed record StandardRestartResult(
    StandardRestartOutcome Outcome,
    string? Message = null);
