using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

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
}
