using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MutePilot.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

[JsonConverter(typeof(HotkeyGestureJsonConverter))]
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    private const int MaximumVirtualKey = 0xFF;

    [JsonIgnore]
    public bool IsStandalone => Modifiers == HotkeyModifiers.None;

    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var parts = new List<string>();

            if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");

            parts.Add(FormatVirtualKey(VirtualKey));
            return string.Join(" + ", parts);
        }
    }

    public static HotkeyGesture FromKey(HotkeyModifiers modifiers, Key key) =>
        new(modifiers, KeyInterop.VirtualKeyFromKey(key));

    public bool TryValidate(out string errorMessage)
    {
        const HotkeyModifiers supportedModifiers =
            HotkeyModifiers.Control |
            HotkeyModifiers.Alt |
            HotkeyModifiers.Shift |
            HotkeyModifiers.Windows;

        if ((Modifiers & ~supportedModifiers) != 0)
        {
            errorMessage = "Windows에서 인식할 수 없는 modifier 조합입니다.";
            return false;
        }

        if (!TryGetRepresentedKey(VirtualKey, out _) || IsModifierVirtualKey(VirtualKey))
        {
            errorMessage = "이 키는 단독 단축키로 사용할 수 없습니다.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static bool IsModifierKey(Key key)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return IsModifierVirtualKey(virtualKey);
    }

    private static bool TryGetRepresentedKey(int virtualKey, out Key key)
    {
        key = Key.None;

        if (virtualKey <= 0 || virtualKey > MaximumVirtualKey)
        {
            return false;
        }

        key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key != Key.None && KeyInterop.VirtualKeyFromKey(key) == virtualKey;
    }

    private static bool IsModifierVirtualKey(int virtualKey) => virtualKey is
        0x10 or // VK_SHIFT
        0x11 or // VK_CONTROL
        0x12 or // VK_MENU
        0x5B or // VK_LWIN
        0x5C or // VK_RWIN
        0xA0 or // VK_LSHIFT
        0xA1 or // VK_RSHIFT
        0xA2 or // VK_LCONTROL
        0xA3 or // VK_RCONTROL
        0xA4 or // VK_LMENU
        0xA5;   // VK_RMENU

    private static string FormatVirtualKey(int virtualKey)
    {
        if (!TryGetRepresentedKey(virtualKey, out var key))
        {
            return $"VK 0x{virtualKey:X2}";
        }

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

public sealed class HotkeyGestureJsonConverter : JsonConverter<HotkeyGesture>
{
    public override HotkeyGesture Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var modifiers = ReadModifiers(root, options);
        var virtualKey = root.TryGetProperty("virtualKey", out var virtualKeyElement) &&
            virtualKeyElement.TryGetInt32(out var storedVirtualKey)
                ? storedVirtualKey
                : ReadLegacyVirtualKey(root, options);

        return new HotkeyGesture(modifiers, virtualKey);
    }

    public override void Write(
        Utf8JsonWriter writer,
        HotkeyGesture value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("modifiers");
        JsonSerializer.Serialize(writer, value.Modifiers, options);
        writer.WriteNumber("virtualKey", value.VirtualKey);
        writer.WriteEndObject();
    }

    private static HotkeyModifiers ReadModifiers(
        JsonElement root,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty("modifiers", out var element))
        {
            return HotkeyModifiers.None;
        }

        return JsonSerializer.Deserialize<HotkeyModifiers>(element.GetRawText(), options);
    }

    private static int ReadLegacyVirtualKey(
        JsonElement root,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty("key", out var element))
        {
            return 0;
        }

        var key = JsonSerializer.Deserialize<Key>(element.GetRawText(), options);
        return KeyInterop.VirtualKeyFromKey(key);
    }
}
