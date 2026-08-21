using MutePilot.Startup;

namespace MutePilot;

internal sealed record ApplicationLaunchOptions(
    bool StartInBackground,
    bool IsElevationHandoff,
    StartupTaskCommand? StartupTaskCommand)
{
    private const string BackgroundArgument = "--background";
    private const string ElevationHandoffArgument = "--elevated-restart";
    private const string StartupTaskArgument = "--startup-task";

    public static ApplicationLaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        var startInBackground = arguments.Any(argument =>
            string.Equals(argument, BackgroundArgument, StringComparison.OrdinalIgnoreCase));
        var isElevationHandoff = false;
        StartupTaskCommand? startupTaskCommand = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            if (string.Equals(
                    arguments[index],
                    ElevationHandoffArgument,
                    StringComparison.OrdinalIgnoreCase) &&
                index + 1 < arguments.Count &&
                Guid.TryParse(arguments[index + 1], out _))
            {
                isElevationHandoff = true;
                index++;
            }
            else if (string.Equals(
                         arguments[index],
                         StartupTaskArgument,
                         StringComparison.OrdinalIgnoreCase) &&
                     index + 1 < arguments.Count &&
                     Enum.TryParse<StartupTaskCommand>(
                         arguments[index + 1],
                         true,
                         out var parsedCommand))
            {
                startupTaskCommand = parsedCommand;
                index++;
            }
        }

        return new ApplicationLaunchOptions(
            startInBackground,
            isElevationHandoff,
            startupTaskCommand);
    }
}
