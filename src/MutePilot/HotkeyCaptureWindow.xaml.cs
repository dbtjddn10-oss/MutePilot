using System.Windows;
using System.Windows.Input;
using MutePilot.Hotkeys;

namespace MutePilot;

public partial class HotkeyCaptureWindow : Window
{
    private HotkeyGesture? _capturedGesture;

    public HotkeyCaptureWindow(HotkeyGesture? currentGesture, string contextText)
    {
        InitializeComponent();
        CaptureContextText.Text = contextText;

        if (currentGesture is not null)
        {
            CapturedHotkeyText.Text = $"현재: {currentGesture.DisplayText}";
        }
    }

    public HotkeyGesture? SelectedGesture { get; private set; }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = GetActualKey(e);
        var modifiers = Keyboard.Modifiers;

        if (HotkeyGesture.IsModifierKey(key))
        {
            RejectInput("이 키는 단독 단축키로 사용할 수 없습니다.");
            return;
        }

        var gesture = HotkeyGesture.FromKey(ToHotkeyModifiers(modifiers), key);

        if (!gesture.TryValidate(out var errorMessage))
        {
            RejectInput(errorMessage);
            return;
        }

        _capturedGesture = gesture;
        CapturedHotkeyText.Text = gesture.DisplayText;
        CaptureErrorText.Text = string.Empty;
        SaveButton.IsEnabled = true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_capturedGesture is null)
        {
            return;
        }

        SelectedGesture = _capturedGesture;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void RejectInput(string message)
    {
        _capturedGesture = null;
        CapturedHotkeyText.Text = "사용할 수 없는 조합";
        CaptureErrorText.Text = message;
        SaveButton.IsEnabled = false;
    }

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;

        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Windows;

        return result;
    }

    private static Key GetActualKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key
    };
}
