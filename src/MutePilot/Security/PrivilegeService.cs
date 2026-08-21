using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace MutePilot.Security;

public sealed class PrivilegeService : IPrivilegeService
{
    private const int UacCancelledErrorCode = 1223;

    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public ElevationRestartResult RestartAsAdministrator(bool startInBackground)
    {
        if (IsElevated)
        {
            return new ElevationRestartResult(ElevationRestartOutcome.AlreadyElevated);
        }

        var processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath))
        {
            return new ElevationRestartResult(
                ElevationRestartOutcome.Failed,
                "현재 MutePilot 실행 파일 경로를 확인하지 못했습니다.");
        }

        var handoffToken = Guid.NewGuid();
        var arguments = $"--elevated-restart {handoffToken:D}";

        if (startInBackground)
        {
            arguments += " --background";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using var process = Process.Start(startInfo);
            return process is null
                ? new ElevationRestartResult(
                    ElevationRestartOutcome.Failed,
                    "관리자 권한 MutePilot을 시작하지 못했습니다.")
                : new ElevationRestartResult(ElevationRestartOutcome.Started);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == UacCancelledErrorCode)
        {
            return new ElevationRestartResult(
                ElevationRestartOutcome.Cancelled,
                "관리자 권한 요청을 취소했습니다. 현재 MutePilot은 계속 실행됩니다.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new ElevationRestartResult(
                ElevationRestartOutcome.Failed,
                "MutePilot을 관리자 권한으로 재시작하지 못했습니다.");
        }
    }

    public StandardRestartResult RestartAsStandardUser(bool startInBackground)
    {
        if (!IsElevated)
        {
            return new StandardRestartResult(StandardRestartOutcome.AlreadyStandard);
        }

        var processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath))
        {
            return new StandardRestartResult(
                StandardRestartOutcome.Failed,
                "현재 MutePilot 실행 파일 경로를 확인하지 못했습니다.");
        }

        object? shell = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application") ??
                throw new InvalidOperationException("Windows Shell을 찾을 수 없습니다.");
            shell = Activator.CreateInstance(shellType) ??
                throw new InvalidOperationException("Windows Shell을 시작할 수 없습니다.");
            var handoffToken = Guid.NewGuid();
            var arguments = $"--elevated-restart {handoffToken:D}";

            if (startInBackground)
            {
                arguments += " --background";
            }

            shellType.InvokeMember(
                "ShellExecute",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                [processPath, arguments, Path.GetDirectoryName(processPath) ?? string.Empty, "open", 1]);
            return new StandardRestartResult(StandardRestartOutcome.Started);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            return new StandardRestartResult(
                StandardRestartOutcome.Failed,
                "일반 권한 MutePilot 시작을 Windows Shell에 요청하지 못했습니다.");
        }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
