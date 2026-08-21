using System.Globalization;

namespace MutePilot.Volume;

internal static class PresetVolumeInput
{
    public const string ValidationMessage = "0~100 사이의 정수를 입력해 주세요.";

    public static bool TryParse(string? text, out int percent)
    {
        var trimmedText = text?.Trim();
        return int.TryParse(
                trimmedText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out percent) &&
            percent is >= 0 and <= 100;
    }
}
