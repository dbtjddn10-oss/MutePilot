using MutePilot.Audio;

namespace MutePilot.Volume;

public sealed class VolumePresetToggleService(IAudioService audioService) :
    IVolumePresetToggleService
{
    private readonly IAudioService _audioService = audioService ??
        throw new ArgumentNullException(nameof(audioService));
    private readonly Dictionary<string, ApplicationVolumeSnapshot> _applicationBaselines =
        new(StringComparer.OrdinalIgnoreCase);
    private MasterVolumeSnapshot? _masterBaseline;

    public bool IsMasterPresetActive => _masterBaseline is not null;

    public bool IsApplicationPresetActive(string applicationKey)
    {
        return !string.IsNullOrWhiteSpace(applicationKey) &&
            _applicationBaselines.ContainsKey(applicationKey);
    }

    public VolumePresetToggleResult ToggleMaster(int presetPercent)
    {
        var current = _audioService.CaptureMasterVolumeSnapshot();

        if (_masterBaseline is not null && !string.Equals(
                _masterBaseline.DeviceId,
                current.DeviceId,
                StringComparison.Ordinal))
        {
            _masterBaseline = null;
        }

        if (_masterBaseline is not null)
        {
            try
            {
                _audioService.RestoreMasterVolumeSnapshot(_masterBaseline);
                _masterBaseline = null;
                return new VolumePresetToggleResult(false, true);
            }
            catch (Exception exception)
            {
                throw new VolumePresetToggleException(
                    "기본 Master Audio 상태를 완전히 복원하지 못했습니다. 다시 시도해 주세요.",
                    exception);
            }
        }

        try
        {
            _audioService.ApplyMasterVolumePreset(current, presetPercent);
            _masterBaseline = current;
            return new VolumePresetToggleResult(true, false);
        }
        catch (Exception exception)
        {
            ThrowAfterMasterActivationRollback(current, exception);
            throw;
        }
    }

    public VolumePresetToggleResult ToggleApplication(
        string applicationKey,
        int presetPercent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationKey);
        var current = _audioService.CaptureApplicationVolumeSnapshot(applicationKey);

        if (_applicationBaselines.TryGetValue(applicationKey, out var baseline) &&
            !HaveSameSessionSet(baseline.Sessions, current.Sessions))
        {
            _applicationBaselines.Remove(applicationKey);
            baseline = null;
        }

        if (baseline is not null)
        {
            try
            {
                _audioService.RestoreApplicationVolumeSnapshot(baseline);
                _applicationBaselines.Remove(applicationKey);
                return new VolumePresetToggleResult(false, true);
            }
            catch (Exception exception)
            {
                throw new VolumePresetToggleException(
                    $"{applicationKey}의 기본 소리 상태를 완전히 복원하지 못했습니다. 다시 시도해 주세요.",
                    exception);
            }
        }

        try
        {
            _audioService.ApplyApplicationVolumePreset(current, presetPercent);
            _applicationBaselines[applicationKey] = current;
            return new VolumePresetToggleResult(true, false);
        }
        catch (Exception exception)
        {
            ThrowAfterApplicationActivationRollback(current, exception);
            throw;
        }
    }

    public bool InvalidateStaleMasterBaseline(string currentDeviceId)
    {
        if (_masterBaseline is not null && !string.Equals(
                _masterBaseline.DeviceId,
                currentDeviceId,
                StringComparison.Ordinal))
        {
            _masterBaseline = null;
            return true;
        }

        return false;
    }

    public bool InvalidateStaleApplicationBaselines(
        IReadOnlyList<ApplicationAudioSession> activeSessions)
    {
        ArgumentNullException.ThrowIfNull(activeSessions);
        var sessionsByApplication = activeSessions.ToDictionary(
            session => session.ApplicationKey,
            StringComparer.OrdinalIgnoreCase);

        var invalidated = false;

        foreach (var pair in _applicationBaselines.ToArray())
        {
            if (!sessionsByApplication.TryGetValue(pair.Key, out var current) ||
                !HaveSameSessionIds(pair.Value.Sessions, current.SessionInstanceIds))
            {
                _applicationBaselines.Remove(pair.Key);
                invalidated = true;
            }
        }

        return invalidated;
    }

    public void Clear()
    {
        _masterBaseline = null;
        _applicationBaselines.Clear();
    }

    private void ThrowAfterMasterActivationRollback(
        MasterVolumeSnapshot baseline,
        Exception activationException)
    {
        try
        {
            _audioService.RestoreMasterVolumeSnapshot(baseline);
        }
        catch (Exception rollbackException)
        {
            throw new VolumePresetToggleException(
                "Master Audio 프리셋 적용과 원상 복구에 실패했습니다. 현재 오디오 상태를 확인해 주세요.",
                new AggregateException(activationException, rollbackException));
        }

        throw new VolumePresetToggleException(
            "Master Audio 프리셋을 적용하지 못해 원래 상태로 되돌렸습니다.",
            activationException);
    }

    private void ThrowAfterApplicationActivationRollback(
        ApplicationVolumeSnapshot baseline,
        Exception activationException)
    {
        try
        {
            _audioService.RestoreApplicationVolumeSnapshot(baseline);
        }
        catch (Exception rollbackException)
        {
            throw new VolumePresetToggleException(
                $"{baseline.ApplicationKey} 프리셋 적용과 원상 복구에 실패했습니다. 현재 오디오 상태를 확인해 주세요.",
                new AggregateException(activationException, rollbackException));
        }

        throw new VolumePresetToggleException(
            $"{baseline.ApplicationKey} 프리셋을 적용하지 못해 원래 상태로 되돌렸습니다.",
            activationException);
    }

    private static bool HaveSameSessionSet(
        IReadOnlyList<ApplicationSessionVolumeSnapshot> left,
        IReadOnlyList<ApplicationSessionVolumeSnapshot> right)
    {
        return HaveSameSessionIds(left, right.Select(session => session.SessionInstanceId));
    }

    private static bool HaveSameSessionIds(
        IReadOnlyList<ApplicationSessionVolumeSnapshot> snapshots,
        IEnumerable<string> sessionInstanceIds)
    {
        return snapshots.Select(session => session.SessionInstanceId)
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals(sessionInstanceIds);
    }
}
