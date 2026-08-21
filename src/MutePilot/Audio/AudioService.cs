using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MutePilot.Audio;

public sealed class AudioService : IAudioService
{
    public bool GetMasterMuteState()
    {
        return UseDefaultOutputDevice(
            device => device.AudioEndpointVolume.Mute,
            "음소거 상태 확인");
    }

    public void SetMasterMuteState(bool isMuted)
    {
        UseDefaultOutputDevice(
            device =>
            {
                device.AudioEndpointVolume.Mute = isMuted;
                return true;
            },
            isMuted ? "음소거" : "음소거 해제");
    }

    public bool ToggleMasterMuteState()
    {
        var nextState = !GetMasterMuteState();
        SetMasterMuteState(nextState);

        return GetMasterMuteState();
    }

    public IReadOnlyList<ApplicationAudioSession> GetActiveApplicationSessions()
    {
        return UseDefaultOutputDevice(
            ReadActiveApplicationSessions,
            "앱별 오디오 세션 조회");
    }

    public ApplicationAudioSession ToggleApplicationMute(string applicationKey)
    {
        if (string.IsNullOrWhiteSpace(applicationKey))
        {
            throw new ArgumentException("Application key is required.", nameof(applicationKey));
        }

        var currentSession = FindApplicationSession(applicationKey);
        var nextMuteState = !currentSession.IsMuted;

        UseDefaultOutputDevice(
            device =>
            {
                SetApplicationMute(device, applicationKey, nextMuteState);
                return true;
            },
            $"{currentSession.ApplicationName} 음소거 상태 변경");

        return FindApplicationSession(applicationKey);
    }

    private ApplicationAudioSession FindApplicationSession(string applicationKey)
    {
        var session = GetActiveApplicationSessions()
            .FirstOrDefault(item => string.Equals(
                item.ApplicationKey,
                applicationKey,
                StringComparison.OrdinalIgnoreCase));

        return session ?? throw new AudioServiceException(
            "대상 애플리케이션의 활성 오디오 세션을 찾을 수 없습니다.",
            new InvalidOperationException($"Audio session group '{applicationKey}' is unavailable."));
    }

    private static IReadOnlyList<ApplicationAudioSession> ReadActiveApplicationSessions(
        MMDevice device)
    {
        var snapshots = new List<ApplicationSessionSnapshot>();
        var sessionManager = device.AudioSessionManager;
        sessionManager.RefreshSessions();
        var sessions = sessionManager.Sessions;
        var sessionCount = sessions.Count;

        for (var index = 0; index < sessionCount; index++)
        {
            try
            {
                using var session = sessions[index];
                var identity = TryReadSessionIdentity(session);

                if (identity is null)
                {
                    continue;
                }

                snapshots.Add(new ApplicationSessionSnapshot(
                    identity.ProcessName,
                    identity.ProcessId,
                    session.SimpleAudioVolume.Mute));
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Audio session {index} could not be read: {exception}");
            }
        }

        return snapshots
            .GroupBy(snapshot => snapshot.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var muteStates = group.Select(snapshot => snapshot.IsMuted).ToArray();
                var processIds = group
                    .Select(snapshot => snapshot.ProcessId)
                    .Distinct()
                    .OrderBy(processId => processId)
                    .ToArray();

                return new ApplicationAudioSession(
                    group.Key,
                    group.Key,
                    processIds,
                    muteStates.All(isMuted => isMuted),
                    muteStates.Any(isMuted => isMuted) && muteStates.Any(isMuted => !isMuted),
                    muteStates.Length);
            })
            .OrderBy(session => session.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void SetApplicationMute(
        MMDevice device,
        string applicationKey,
        bool isMuted)
    {
        var sessionManager = device.AudioSessionManager;
        sessionManager.RefreshSessions();
        var sessions = sessionManager.Sessions;
        var sessionCount = sessions.Count;
        var matchedSessionCount = 0;
        var updatedSessionCount = 0;
        var failures = new List<Exception>();

        for (var index = 0; index < sessionCount; index++)
        {
            try
            {
                using var session = sessions[index];
                var identity = TryReadSessionIdentity(session);

                if (identity is null || !string.Equals(
                        identity.ProcessName,
                        applicationKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchedSessionCount++;

                try
                {
                    session.SimpleAudioVolume.Mute = isMuted;
                    updatedSessionCount++;
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                    Debug.WriteLine($"Audio session {index} could not be updated: {exception}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Audio session {index} disappeared while updating: {exception}");
            }
        }

        if (matchedSessionCount == 0)
        {
            throw new AudioServiceException(
                "대상 애플리케이션의 활성 오디오 세션이 사라졌습니다.",
                new InvalidOperationException($"Audio session group '{applicationKey}' disappeared."));
        }

        if (updatedSessionCount == 0)
        {
            throw new AudioServiceException(
                "대상 애플리케이션의 오디오 세션을 변경할 수 없습니다.",
                failures.Count > 0
                    ? new AggregateException(failures)
                    : new InvalidOperationException("No audio sessions were updated."));
        }

        if (failures.Count > 0)
        {
            throw new AudioServiceException(
                "일부 오디오 세션을 변경하지 못했습니다. 목록을 새로고침해 주세요.",
                new AggregateException(failures));
        }
    }

    private static ApplicationSessionIdentity? TryReadSessionIdentity(
        AudioSessionControl session)
    {
        try
        {
            if (session.State != AudioSessionState.AudioSessionStateActive ||
                session.IsSystemSoundsSession)
            {
                return null;
            }

            var processId = session.GetProcessID;

            if (processId == 0 || processId > int.MaxValue)
            {
                return null;
            }

            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;

            return string.IsNullOrWhiteSpace(processName)
                ? null
                : new ApplicationSessionIdentity(processName, (int)processId);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Audio session process information is unavailable: {exception}");
            return null;
        }
    }

    private static TResult UseDefaultOutputDevice<TResult>(
        Func<MMDevice, TResult> operation,
        string operationName)
    {
        try
        {
            using var deviceEnumerator = new MMDeviceEnumerator();
            using var device = deviceEnumerator.GetDefaultAudioEndpoint(
                DataFlow.Render,
                Role.Multimedia);

            return operation(device);
        }
        catch (Exception exception) when (exception is not AudioServiceException)
        {
            throw new AudioServiceException(
                $"Windows 기본 출력 장치의 {operationName} 작업에 실패했습니다.",
                exception);
        }
    }

    private sealed record ApplicationSessionIdentity(string ProcessName, int ProcessId);

    private sealed record ApplicationSessionSnapshot(
        string ProcessName,
        int ProcessId,
        bool IsMuted);
}
