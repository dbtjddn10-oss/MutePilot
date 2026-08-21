namespace MutePilot.Startup;

public enum StartupTaskState
{
    Disabled,
    Enabled,
    ConfigurationMismatch,
    QueryFailed
}

public sealed record StartupStatus(
    StartupTaskState State,
    string? RegisteredExecutablePath = null,
    string? DetailMessage = null)
{
    public bool TaskExists => State is
        StartupTaskState.Enabled or StartupTaskState.ConfigurationMismatch;
}

public enum StartupTaskCommand
{
    Enable,
    Disable
}

public enum StartupChangeOutcome
{
    Succeeded,
    Cancelled,
    Failed
}

public sealed record StartupChangeResult(
    StartupChangeOutcome Outcome,
    StartupStatus Status,
    string? Message = null);
