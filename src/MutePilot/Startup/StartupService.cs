using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MutePilot.Startup;

public sealed class StartupService : IStartupService
{
    public const string TaskName = "MutePilot Startup";

    private const string BackgroundArgument = "--background";
    private const int TaskActionExecute = 0;
    private const int TaskCreateOrUpdate = 6;
    private const int TaskInstancesIgnoreNew = 2;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskRunLevelHighest = 1;
    private const int TaskTriggerLogon = 9;
    private const uint ErrorFileNotFound = 0x80070002;
    private const uint SchedulerTaskNotFound = 0x8004130F;
    private const int UacCancelledErrorCode = 1223;

    public StartupStatus GetStatus()
    {
        object? scheduler = null;
        object? rootFolder = null;
        object? task = null;
        object? definition = null;
        object? trigger = null;
        object? action = null;

        try
        {
            scheduler = CreateScheduler();
            dynamic schedulerApi = scheduler;
            schedulerApi.Connect();
            rootFolder = schedulerApi.GetFolder("\\");
            task = ((dynamic)rootFolder).GetTask(TaskName);
            definition = ((dynamic)task).Definition;
            trigger = ((dynamic)definition).Triggers.Item(1);
            action = ((dynamic)definition).Actions.Item(1);

            var registeredPath = (string?)((dynamic)action).Path;
            var registeredArguments = ((string?)((dynamic)action).Arguments)?.Trim();
            var principal = ((dynamic)definition).Principal;
            var principalUserId = (string?)principal.UserId;
            var runLevel = (int)principal.RunLevel;
            var logonType = (int)principal.LogonType;
            var triggerType = (int)((dynamic)trigger).Type;
            var triggerUserId = (string?)((dynamic)trigger).UserId;
            using var identity = WindowsIdentity.GetCurrent();
            var currentUserSid = identity.User?.Value;
            var currentPath = GetCurrentExecutablePath();
            var configurationMatches = PathsEqual(registeredPath, currentPath) &&
                                       string.Equals(
                                           registeredArguments,
                                           BackgroundArgument,
                                           StringComparison.OrdinalIgnoreCase) &&
                                       runLevel == TaskRunLevelHighest &&
                                       logonType == TaskLogonInteractiveToken &&
                                       triggerType == TaskTriggerLogon &&
                                       MatchesCurrentUser(principalUserId, identity.Name, currentUserSid) &&
                                       MatchesCurrentUser(triggerUserId, identity.Name, currentUserSid);

            return configurationMatches
                ? new StartupStatus(StartupTaskState.Enabled, registeredPath)
                : new StartupStatus(
                    StartupTaskState.ConfigurationMismatch,
                    registeredPath,
                    "등록된 자동 시작 작업의 경로나 실행 조건이 현재 앱과 다릅니다.");
        }
        catch (Exception exception) when (IsTaskMissing(exception))
        {
            return new StartupStatus(StartupTaskState.Disabled);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new StartupStatus(
                StartupTaskState.QueryFailed,
                DetailMessage: "Windows 자동 시작 작업 상태를 확인하지 못했습니다.");
        }
        finally
        {
            ReleaseComObject(action);
            ReleaseComObject(trigger);
            ReleaseComObject(definition);
            ReleaseComObject(task);
            ReleaseComObject(rootFolder);
            ReleaseComObject(scheduler);
        }
    }

    public async Task<StartupChangeResult> SetEnabledAsync(bool isEnabled)
    {
        var command = isEnabled ? StartupTaskCommand.Enable : StartupTaskCommand.Disable;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GetCurrentExecutablePath(),
                Arguments = $"--startup-task {command}",
                UseShellExecute = true,
                Verb = "runas"
            };
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return FailedResult("Windows 자동 시작 작업 변경을 시작하지 못했습니다.");
            }

            await process.WaitForExitAsync();
            var status = GetStatus();
            var changedAsRequested = isEnabled
                ? status.State == StartupTaskState.Enabled
                : status.State == StartupTaskState.Disabled;

            if (process.ExitCode == 0 && changedAsRequested)
            {
                return new StartupChangeResult(StartupChangeOutcome.Succeeded, status);
            }

            return new StartupChangeResult(
                StartupChangeOutcome.Failed,
                status,
                isEnabled
                    ? "Windows 시작 작업을 만들지 못했습니다. 관리자 권한 승인 여부를 확인해 주세요."
                    : "Windows 시작 작업을 제거하지 못했습니다. 관리자 권한 승인 여부를 확인해 주세요.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == UacCancelledErrorCode)
        {
            return new StartupChangeResult(
                StartupChangeOutcome.Cancelled,
                GetStatus(),
                "관리자 권한 요청이 취소되어 Windows 시작 설정을 바꾸지 않았습니다.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return FailedResult("Windows 자동 시작 작업을 변경하지 못했습니다.");
        }
    }

    public int RunElevatedTaskCommand(StartupTaskCommand command)
    {
        try
        {
            if (command == StartupTaskCommand.Enable)
            {
                CreateOrUpdateTask();
            }
            else
            {
                DeleteTask();
            }

            return 0;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return 1;
        }
    }

    private static object CreateScheduler()
    {
        var schedulerType = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true) ??
            throw new InvalidOperationException("Windows Task Scheduler API를 사용할 수 없습니다.");
        return Activator.CreateInstance(schedulerType) ??
            throw new InvalidOperationException("Windows Task Scheduler에 연결하지 못했습니다.");
    }

    private static void CreateOrUpdateTask()
    {
        object? scheduler = null;
        object? rootFolder = null;
        object? definition = null;
        object? trigger = null;
        object? action = null;

        try
        {
            var executablePath = GetCurrentExecutablePath();
            using var identity = WindowsIdentity.GetCurrent();
            var userSid = identity.User?.Value ??
                throw new InvalidOperationException("현재 Windows 사용자 SID를 확인하지 못했습니다.");

            scheduler = CreateScheduler();
            dynamic schedulerApi = scheduler;
            schedulerApi.Connect();
            rootFolder = schedulerApi.GetFolder("\\");
            definition = schedulerApi.NewTask(0);
            dynamic taskDefinition = definition;
            taskDefinition.RegistrationInfo.Author = identity.Name;
            taskDefinition.RegistrationInfo.Description =
                "MutePilot을 Windows 로그인 시 관리자 권한으로 백그라운드 실행합니다.";
            taskDefinition.Principal.UserId = userSid;
            taskDefinition.Principal.LogonType = TaskLogonInteractiveToken;
            taskDefinition.Principal.RunLevel = TaskRunLevelHighest;
            taskDefinition.Settings.Enabled = true;
            taskDefinition.Settings.AllowDemandStart = true;
            taskDefinition.Settings.StartWhenAvailable = true;
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.Settings.StopIfGoingOnBatteries = false;
            taskDefinition.Settings.ExecutionTimeLimit = "PT0S";
            taskDefinition.Settings.MultipleInstances = TaskInstancesIgnoreNew;

            trigger = taskDefinition.Triggers.Create(TaskTriggerLogon);
            ((dynamic)trigger).UserId = userSid;

            action = taskDefinition.Actions.Create(TaskActionExecute);
            ((dynamic)action).Path = executablePath;
            ((dynamic)action).Arguments = BackgroundArgument;
            ((dynamic)action).WorkingDirectory =
                Path.GetDirectoryName(executablePath) ?? string.Empty;

            ((dynamic)rootFolder).RegisterTaskDefinition(
                TaskName,
                taskDefinition,
                TaskCreateOrUpdate,
                userSid,
                null,
                TaskLogonInteractiveToken,
                null);
        }
        finally
        {
            ReleaseComObject(action);
            ReleaseComObject(trigger);
            ReleaseComObject(definition);
            ReleaseComObject(rootFolder);
            ReleaseComObject(scheduler);
        }
    }

    private static void DeleteTask()
    {
        object? scheduler = null;
        object? rootFolder = null;

        try
        {
            scheduler = CreateScheduler();
            dynamic schedulerApi = scheduler;
            schedulerApi.Connect();
            rootFolder = schedulerApi.GetFolder("\\");

            try
            {
                ((dynamic)rootFolder).DeleteTask(TaskName, 0);
            }
            catch (Exception exception) when (IsTaskMissing(exception))
            {
            }
        }
        finally
        {
            ReleaseComObject(rootFolder);
            ReleaseComObject(scheduler);
        }
    }

    private StartupChangeResult FailedResult(string message) => new(
        StartupChangeOutcome.Failed,
        GetStatus(),
        message);

    private static string GetCurrentExecutablePath() =>
        Environment.ProcessPath ??
        throw new InvalidOperationException("현재 MutePilot 실행 파일 경로를 확인하지 못했습니다.");

    private static bool PathsEqual(string? first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesCurrentUser(
        string? registeredUser,
        string currentUserName,
        string? currentUserSid) =>
        !string.IsNullOrWhiteSpace(registeredUser) &&
        (string.Equals(registeredUser, currentUserName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(registeredUser, currentUserSid, StringComparison.OrdinalIgnoreCase));

    private static bool IsTaskMissing(Exception exception) =>
        unchecked((uint)exception.HResult) is ErrorFileNotFound or SchedulerTaskNotFound;

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(value);
        }
    }
}
