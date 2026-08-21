using NAudio.CoreAudioApi;

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
}
