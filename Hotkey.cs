using System.Windows.Input;

namespace Drawski;

public readonly record struct Hotkey(Key Key, ModifierKeys Modifiers)
{
    public static readonly Hotkey NONE = new(Key.None, ModifierKeys.None);

    public bool IsNone => Key == Key.None;
    public bool HasCtrlOrAlt => Modifiers.HasFlag(ModifierKeys.Control) || Modifiers.HasFlag(ModifierKeys.Alt);

    // Saved form uses enum names ("Ctrl+OemOpenBrackets"), Display is what people read ("Ctrl+[")
    public string Serialize() => IsNone ? "" : string.Join("+", ModifierNames().Append(Key.ToString()));

    public string Display => IsNone ? "None" : string.Join("+", ModifierNames().Append(KeyName()));

    public static Hotkey Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return NONE;

        string[] parts = text.Split('+');
        ModifierKeys modifiers = ModifierKeys.None;
        foreach (string part in parts[..^1])
        {
            modifiers |= part switch
            {
                "Ctrl" => ModifierKeys.Control,
                "Alt" => ModifierKeys.Alt,
                "Shift" => ModifierKeys.Shift,
                "Win" => ModifierKeys.Windows,
                _ => ModifierKeys.None,
            };
        }
        return Enum.TryParse(parts[^1], out Key key) ? new Hotkey(key, modifiers) : NONE;
    }

    IEnumerable<string> ModifierNames()
    {
        if (Modifiers.HasFlag(ModifierKeys.Control)) yield return "Ctrl";
        if (Modifiers.HasFlag(ModifierKeys.Alt)) yield return "Alt";
        if (Modifiers.HasFlag(ModifierKeys.Shift)) yield return "Shift";
        if (Modifiers.HasFlag(ModifierKeys.Windows)) yield return "Win";
    }

    string KeyName() => Key switch
    {
        >= Key.D0 and <= Key.D9 => (Key - Key.D0).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (Key - Key.NumPad0),
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemQuestion => "/",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemPipe => "\\",
        Key.OemTilde => "`",
        Key.Delete => "Del",
        Key.Escape => "Esc",
        Key.Back => "Backspace",
        Key.Return => "Enter",
        Key.Prior => "Page Up",
        Key.Next => "Page Down",
        _ => Key.ToString(),
    };
}
