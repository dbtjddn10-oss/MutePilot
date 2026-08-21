using System.Diagnostics;
using MutePilot.Volume;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MutePilot.Audio;

public sealed class AudioService : IAudioService
{
    private const float ScalarComparisonTolerance = 0.001f;

    public bool GetMasterMuteState() => CaptureMasterVolumeSnapshot().IsMuted;

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

    public int GetMasterVolumePercent() => CaptureMasterVolumeSnapshot().VolumePercent;

    public int SetMasterVolumePercent(int percent)
    {
        var normalizedPercent = NormalizeVolumePercent(percent);

        return UseDefaultOutputDevice(
            device =>
            {
                var muteState = device.AudioEndpointVolume.Mute;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = normalizedPercent / 100f;
                var appliedPercent = ToVolumePercent(
                    device.AudioEndpointVolume.MasterVolumeLevelScalar);

                if (device.AudioEndpointVolume.Mute != muteState ||
                    Math.Abs(appliedPercent - normalizedPercent) > 1)
                {
                    throw new AudioServiceException(
                        "Master Audio 현재 볼륨을 정확히 변경하지 못했습니다.",
                        new InvalidOperationException("The live master volume differs from the request."));
                }

                return appliedPercent;
            },
            $"현재 볼륨을 {normalizedPercent}%로 설정");
    }

    public MasterVolumeSnapshot ApplyMasterVolumePreset(
        MasterVolumeSnapshot snapshot,
        int percent)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedPercent = NormalizeVolumePercent(percent);

        return UseDefaultOutputDevice(
            device =>
            {
                if (!string.Equals(device.ID, snapshot.DeviceId, StringComparison.Ordinal))
                {
                    throw new AudioServiceException(
                        "기본 출력 장치가 바뀌어 Master Audio 프리셋을 적용하지 않았습니다.",
                        new InvalidOperationException("The default output device changed."));
                }

                device.AudioEndpointVolume.MasterVolumeLevelScalar = normalizedPercent / 100f;
                device.AudioEndpointVolume.Mute = false;

                var applied = new MasterVolumeSnapshot(
                    device.ID,
                    device.AudioEndpointVolume.MasterVolumeLevelScalar,
                    device.AudioEndpointVolume.Mute);

                if (applied.IsMuted ||
                    Math.Abs(applied.VolumeScalar - normalizedPercent / 100f) >
                    ScalarComparisonTolerance)
                {
                    throw new AudioServiceException(
                        "Master Audio 프리셋 적용 결과가 요청한 값과 다릅니다.",
                        new InvalidOperationException("The applied master state differs from the preset."));
                }

                return applied;
            },
            $"볼륨을 {normalizedPercent}%로 설정");
    }

    public MasterVolumeSnapshot CaptureMasterVolumeSnapshot()
    {
        return UseDefaultOutputDevice(
            device => new MasterVolumeSnapshot(
                device.ID,
                device.AudioEndpointVolume.MasterVolumeLevelScalar,
                device.AudioEndpointVolume.Mute),
            "현재 소리 상태 확인");
    }

    public MasterVolumeSnapshot RestoreMasterVolumeSnapshot(MasterVolumeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return UseDefaultOutputDevice(
            device =>
            {
                if (!string.Equals(device.ID, snapshot.DeviceId, StringComparison.Ordinal))
                {
                    throw new AudioServiceException(
                        "기본 출력 장치가 바뀌어 이전 Master Audio 상태를 복원하지 않았습니다.",
                        new InvalidOperationException("The default output device changed."));
                }

                device.AudioEndpointVolume.MasterVolumeLevelScalar =
                    Math.Clamp(snapshot.VolumeScalar, 0f, 1f);
                device.AudioEndpointVolume.Mute = snapshot.IsMuted;

                var restored = new MasterVolumeSnapshot(
                    device.ID,
                    device.AudioEndpointVolume.MasterVolumeLevelScalar,
                    device.AudioEndpointVolume.Mute);

                if (restored.IsMuted != snapshot.IsMuted ||
                    Math.Abs(restored.VolumeScalar - snapshot.VolumeScalar) >
                    ScalarComparisonTolerance)
                {
                    throw new AudioServiceException(
                        "Master Audio의 기본 소리 상태를 완전히 복원하지 못했습니다.",
                        new InvalidOperationException("The restored master state differs from the baseline."));
                }

                return restored;
            },
            "기본 소리 상태 복원");
    }

    public IReadOnlyList<ApplicationAudioSession> GetActiveApplicationSessions()
    {
        return UseDefaultOutputDevice(
            device => AggregateApplicationSessions(ReadApplicationSessionSnapshots(device)),
            "앱별 오디오 세션 조회");
    }

    public ApplicationAudioSession ToggleApplicationMute(string applicationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);
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

    public ApplicationAudioSession SetApplicationVolumePercent(
        string applicationKey,
        int percent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);
        var normalizedPercent = NormalizeVolumePercent(percent);

        return UseDefaultOutputDevice(
            device =>
            {
                SetApplicationLiveVolume(device, applicationKey, normalizedPercent);
                var aggregate = AggregateApplicationSessions(
                        ReadApplicationSessionSnapshots(device))
                    .FirstOrDefault(session => string.Equals(
                        session.ApplicationKey,
                        applicationKey,
                        StringComparison.OrdinalIgnoreCase));

                return aggregate ?? throw new AudioServiceException(
                    "변경한 애플리케이션 오디오 세션을 다시 확인할 수 없습니다.",
                    new InvalidOperationException("The updated application session disappeared."));
            },
            $"{applicationKey} 현재 볼륨을 {normalizedPercent}%로 설정");
    }

    public ApplicationVolumeSnapshot CaptureApplicationVolumeSnapshot(string applicationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);
        return UseDefaultOutputDevice(
            device => CaptureApplicationVolumeSnapshot(device, applicationKey),
            $"{applicationKey}의 현재 소리 상태 확인");
    }

    public ApplicationAudioSession ApplyApplicationVolumePreset(
        ApplicationVolumeSnapshot snapshot,
        int percent)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedPercent = NormalizeVolumePercent(percent);

        return UseDefaultOutputDevice(
            device => ChangeApplicationSessionStates(
                device,
                snapshot,
                normalizedPercent),
            $"{snapshot.ApplicationKey} 볼륨을 {normalizedPercent}%로 전환");
    }

    public ApplicationAudioSession RestoreApplicationVolumeSnapshot(
        ApplicationVolumeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return UseDefaultOutputDevice(
            device => ChangeApplicationSessionStates(device, snapshot, null),
            $"{snapshot.ApplicationKey} 기본 소리 상태 복원");
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

    private static ApplicationVolumeSnapshot CaptureApplicationVolumeSnapshot(
        MMDevice device,
        string applicationKey)
    {
        var sessions = ReadApplicationSessionSnapshots(device)
            .Where(session => string.Equals(
                session.ProcessName,
                applicationKey,
                StringComparison.OrdinalIgnoreCase))
            .Select(session => new ApplicationSessionVolumeSnapshot(
                session.SessionInstanceId,
                session.ProcessId,
                session.VolumeScalar,
                session.IsMuted))
            .OrderBy(session => session.SessionInstanceId, StringComparer.Ordinal)
            .ToArray();

        if (sessions.Length == 0)
        {
            throw new AudioServiceException(
                "대상 애플리케이션의 활성 오디오 세션을 찾을 수 없습니다.",
                new InvalidOperationException($"Audio session group '{applicationKey}' is unavailable."));
        }

        return new ApplicationVolumeSnapshot(applicationKey, sessions);
    }

    private static IReadOnlyList<ApplicationAudioSession> AggregateApplicationSessions(
        IReadOnlyList<ApplicationSessionSnapshot> snapshots)
    {
        return snapshots
            .GroupBy(snapshot => snapshot.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var muteStates = group.Select(snapshot => snapshot.IsMuted).ToArray();
                var volumeLevels = group.Select(snapshot => snapshot.VolumePercent).ToArray();
                var processIds = group.Select(snapshot => snapshot.ProcessId)
                    .Distinct()
                    .OrderBy(processId => processId)
                    .ToArray();
                var sessionIds = group.Select(snapshot => snapshot.SessionInstanceId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(sessionId => sessionId, StringComparer.Ordinal)
                    .ToArray();

                return new ApplicationAudioSession(
                    group.Key,
                    group.Key,
                    processIds,
                    sessionIds,
                    muteStates.All(isMuted => isMuted),
                    muteStates.Any(isMuted => isMuted) && muteStates.Any(isMuted => !isMuted),
                    (int)Math.Round(volumeLevels.Average(), MidpointRounding.AwayFromZero),
                    volumeLevels.Distinct().Skip(1).Any(),
                    muteStates.Length);
            })
            .OrderBy(session => session.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ApplicationSessionSnapshot> ReadApplicationSessionSnapshots(
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
                    identity.SessionInstanceId,
                    session.SimpleAudioVolume.Mute,
                    session.SimpleAudioVolume.Volume));
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Audio session {index} could not be read: {exception}");
            }
        }

        return snapshots;
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

        ThrowIfApplicationUpdateFailed(
            applicationKey,
            matchedSessionCount,
            updatedSessionCount,
            failures,
            "오디오 세션");
    }

    private static void SetApplicationLiveVolume(
        MMDevice device,
        string applicationKey,
        int percent)
    {
        var sessionManager = device.AudioSessionManager;
        sessionManager.RefreshSessions();
        var sessions = sessionManager.Sessions;
        var sessionCount = sessions.Count;
        var matchedSessionCount = 0;
        var updatedSessionCount = 0;
        var failures = new List<Exception>();
        var scalar = percent / 100f;

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
                    var muteState = session.SimpleAudioVolume.Mute;
                    session.SimpleAudioVolume.Volume = scalar;

                    if (session.SimpleAudioVolume.Mute != muteState ||
                        Math.Abs(session.SimpleAudioVolume.Volume - scalar) >
                        ScalarComparisonTolerance)
                    {
                        throw new InvalidOperationException(
                            "The live application volume differs from the request.");
                    }

                    updatedSessionCount++;
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                    Debug.WriteLine(
                        $"Audio session {index} live volume could not be updated: {exception}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    $"Audio session {index} disappeared while updating live volume: {exception}");
            }
        }

        ThrowIfApplicationUpdateFailed(
            applicationKey,
            matchedSessionCount,
            updatedSessionCount,
            failures,
            "현재 볼륨");
    }

    private static ApplicationAudioSession ChangeApplicationSessionStates(
        MMDevice device,
        ApplicationVolumeSnapshot snapshot,
        int? presetPercent)
    {
        var expectedById = snapshot.Sessions.ToDictionary(
            session => session.SessionInstanceId,
            StringComparer.Ordinal);
        var current = ReadApplicationSessionSnapshots(device)
            .Where(session => string.Equals(
                session.ProcessName,
                snapshot.ApplicationKey,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!expectedById.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(current.Select(session => session.SessionInstanceId)))
        {
            throw new AudioServiceException(
                "애플리케이션의 오디오 세션이 바뀌어 이전 상태를 적용하지 않았습니다.",
                new InvalidOperationException("The application audio session set changed."));
        }

        var sessionManager = device.AudioSessionManager;
        sessionManager.RefreshSessions();
        var sessions = sessionManager.Sessions;
        var sessionCount = sessions.Count;
        var updatedIds = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<Exception>();

        for (var index = 0; index < sessionCount; index++)
        {
            try
            {
                using var session = sessions[index];
                var identity = TryReadSessionIdentity(session);

                if (identity is null ||
                    !expectedById.TryGetValue(identity.SessionInstanceId, out var baseline))
                {
                    continue;
                }

                try
                {
                    session.SimpleAudioVolume.Volume = presetPercent is int percent
                        ? percent / 100f
                        : Math.Clamp(baseline.VolumeScalar, 0f, 1f);
                    session.SimpleAudioVolume.Mute = presetPercent is null && baseline.IsMuted;
                    updatedIds.Add(identity.SessionInstanceId);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                    Debug.WriteLine($"Audio session {index} state could not be updated: {exception}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Audio session {index} disappeared while updating state: {exception}");
            }
        }

        if (!updatedIds.SetEquals(expectedById.Keys) || failures.Count > 0)
        {
            throw new AudioServiceException(
                "일부 오디오 세션의 소리 상태를 변경하지 못했습니다.",
                failures.Count > 0
                    ? new AggregateException(failures)
                    : new InvalidOperationException("Not all expected sessions were updated."));
        }

        var verifiedSnapshot = CaptureApplicationVolumeSnapshot(device, snapshot.ApplicationKey);
        VerifyApplicationState(snapshot, verifiedSnapshot, presetPercent);
        var aggregate = AggregateApplicationSessions(ReadApplicationSessionSnapshots(device))
            .FirstOrDefault(session => string.Equals(
                session.ApplicationKey,
                snapshot.ApplicationKey,
                StringComparison.OrdinalIgnoreCase));

        return aggregate ?? throw new AudioServiceException(
            "변경한 애플리케이션 오디오 세션을 다시 확인할 수 없습니다.",
            new InvalidOperationException("The updated application session disappeared."));
    }

    private static void VerifyApplicationState(
        ApplicationVolumeSnapshot baseline,
        ApplicationVolumeSnapshot current,
        int? presetPercent)
    {
        var currentById = current.Sessions.ToDictionary(
            session => session.SessionInstanceId,
            StringComparer.Ordinal);

        if (!currentById.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(baseline.Sessions.Select(session => session.SessionInstanceId)))
        {
            throw new AudioServiceException(
                "오디오 세션 구성이 변경되어 결과를 확인할 수 없습니다.",
                new InvalidOperationException("The session set changed while verifying audio state."));
        }

        foreach (var expected in baseline.Sessions)
        {
            var actual = currentById[expected.SessionInstanceId];
            var expectedScalar = presetPercent is int percent
                ? percent / 100f
                : expected.VolumeScalar;
            var expectedMuted = presetPercent is null && expected.IsMuted;

            if (actual.IsMuted != expectedMuted ||
                Math.Abs(actual.VolumeScalar - expectedScalar) > ScalarComparisonTolerance)
            {
                throw new AudioServiceException(
                    "오디오 세션의 소리 상태가 요청한 값과 다릅니다.",
                    new InvalidOperationException(
                        $"Session '{expected.SessionInstanceId}' did not reach the requested state."));
            }
        }
    }

    private static void ThrowIfApplicationUpdateFailed(
        string applicationKey,
        int matchedSessionCount,
        int updatedSessionCount,
        IReadOnlyCollection<Exception> failures,
        string operationName)
    {
        if (matchedSessionCount == 0)
        {
            throw new AudioServiceException(
                "대상 애플리케이션의 활성 오디오 세션이 사라졌습니다.",
                new InvalidOperationException($"Audio session group '{applicationKey}' disappeared."));
        }

        if (updatedSessionCount == 0 || failures.Count > 0)
        {
            throw new AudioServiceException(
                $"대상 애플리케이션의 {operationName}을(를) 완전히 변경하지 못했습니다.",
                failures.Count > 0
                    ? new AggregateException(failures)
                    : new InvalidOperationException("No audio sessions were updated."));
        }
    }

    private static int NormalizeVolumePercent(int percent) => Math.Clamp(percent, 0, 100);

    private static int ToVolumePercent(float scalar) =>
        Math.Clamp((int)Math.Round(scalar * 100, MidpointRounding.AwayFromZero), 0, 100);

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
            var sessionInstanceId = session.GetSessionInstanceIdentifier;

            if (processId == 0 || processId > int.MaxValue ||
                string.IsNullOrWhiteSpace(sessionInstanceId))
            {
                return null;
            }

            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;

            return string.IsNullOrWhiteSpace(processName)
                ? null
                : new ApplicationSessionIdentity(
                    processName,
                    (int)processId,
                    sessionInstanceId);
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

    private sealed record ApplicationSessionIdentity(
        string ProcessName,
        int ProcessId,
        string SessionInstanceId);

    private sealed record ApplicationSessionSnapshot(
        string ProcessName,
        int ProcessId,
        string SessionInstanceId,
        bool IsMuted,
        float VolumeScalar)
    {
        public int VolumePercent => ToVolumePercent(VolumeScalar);
    }
}
