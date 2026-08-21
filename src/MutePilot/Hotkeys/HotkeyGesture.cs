using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MutePilot.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4
}

public sealed record HotkeyGesture
{
    [JsonConstructor]
    public HotkeyGesture(HotkeyModifiers modifiers, Key key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public HotkeyModifiers Modifiers { get; init; }

    public Key Key { get; init; }

    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var parts = new List<string>();

            if (Modifiers.HasFlag(HotkeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            parts.Add(FormatKey(Key));
            return string.Join(" + ", parts);
        }
    }

    public bool TryValidate(out string errorMessage)
    {
        const HotkeyModifiers supportedModifiers =
            HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;

        if ((Modifiers & ~supportedModifiers) != 0)
        {
            errorMessage = "Windows 키 조합은 지원하지 않습니다.";
            return false;
        }

        if (Key == Key.None || IsModifierKey(Key))
        {
            errorMessage = "Ctrl, Alt, Shift 외의 키를 함께 눌러 주세요.";
            return false;
        }

        if (Key == Key.F12)
        {
            errorMessage = "F12는 Windows에서 예약할 수 있어 사용할 수 없습니다.";
            return false;
        }

        var isFunctionKey = Key >= Key.F1 && Key <= Key.F11;
        var isLetter = Key >= Key.A && Key <= Key.Z;
        var isTopRowNumber = Key >= Key.D0 && Key <= Key.D9;
        var isNumberPadKey = Key >= Key.NumPad0 && Key <= Key.NumPad9;

        if (!isFunctionKey && !isLetter && !isTopRowNumber && !isNumberPadKey)
        {
            errorMessage = "F1~F11 또는 Ctrl/Alt/Shift와 조합한 영문·숫자 키를 사용해 주세요.";
            return false;
        }

        if (!isFunctionKey && Modifiers == HotkeyModifiers.None)
        {
            errorMessage = "영문·숫자 키에는 Ctrl, Alt, Shift 중 하나 이상이 필요합니다.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
    }

    private static string FormatKey(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
        {
            return key.ToString()[1..];
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return $"NumPad {key.ToString()[6..]}";
        }

        return key.ToString();
    }
}
