using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Drawski;

public partial class SettingsWindow : Window
{
    static readonly Brush CAPTURE_BACKGROUND = new SolidColorBrush(Color.FromRgb(0x2A, 0x4A, 0x8A));
    static readonly Brush WARNING_FOREGROUND = new SolidColorBrush(Color.FromRgb(0xFF, 0xB4, 0x54));
    static readonly Brush MUTED_FOREGROUND = new SolidColorBrush(Color.FromRgb(0x9A, 0x9C, 0xA3));

    readonly OverlayWindow _overlay;

    Shortcut? __capturing;

    Settings Settings => _overlay.Settings;

    public SettingsWindow(OverlayWindow overlay)
    {
        InitializeComponent();
        _overlay = overlay;
        _startupCheck.IsChecked = Settings.ShowOnStartup;
        Deactivated += (_, _) => CancelCapture();
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelCapture();
        base.OnClosed(e);
    }

    void Refresh()
    {
        BuildPalette();
        _globalRows.Children.Clear();
        _localRows.Children.Clear();
        foreach (ShortcutInfo info in ShortcutInfo.ALL)
            (info.IsGlobal ? _globalRows : _localRows).Children.Add(ShortcutRow(info));
    }

    #region Colours

    void BuildPalette()
    {
        _paletteGrid.Children.Clear();
        for (int i = 0; i < Settings.Palette.Length; i++)
        {
            Color color = Settings.Palette[i];
            Button swatch = new()
            {
                Style = (Style)FindResource("SwatchButton"),
                Background = new SolidColorBrush(color),
                Tag = i,
                ToolTip = "Click to change",
            };
            swatch.Click += OnSwatchClick;

            StackPanel cell = new() { Margin = new Thickness(0, 0, 12, 0) };
            cell.Children.Add(swatch);
            cell.Children.Add(new TextBlock { Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}", FontSize = 12, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            cell.Children.Add(new TextBlock { Text = $"Key {Settings.Keys[Shortcut.Color1 + i].Display}", FontSize = 11, Foreground = MUTED_FOREGROUND, HorizontalAlignment = HorizontalAlignment.Center });
            _paletteGrid.Children.Add(cell);
        }
    }

    void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        int index = (int)((FrameworkElement)sender).Tag;
        ColorWheelWindow picker = new(Settings.Palette[index]) { Owner = this };
        if (picker.ShowDialog() != true) return;

        _overlay.SetPaletteColor(index, picker.SelectedColor);
        Refresh();
    }

    void OnResetColoursClick(object sender, RoutedEventArgs e)
    {
        _overlay.ResetPalette();
        Refresh();
    }

    #endregion

    #region Shortcuts

    Grid ShortcutRow(ShortcutInfo info)
    {
        Hotkey hotkey = Settings.Keys[info.Id];
        bool capturing = __capturing == info.Id;
        bool failed = info.IsGlobal && _overlay.HotkeyFailed(info.Id);

        Grid row = new() { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

        StackPanel label = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (info.Id is >= Shortcut.Color1 and <= Shortcut.Color6)
        {
            label.Children.Add(new Ellipse
            {
                Width = 12,
                Height = 12,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = new SolidColorBrush(Settings.Palette[info.Id - Shortcut.Color1]),
                Stroke = new SolidColorBrush(Color.FromArgb(0x59, 255, 255, 255)),
            });
        }
        label.Children.Add(new TextBlock { Text = info.Label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(label);

        Button keys = new()
        {
            Style = (Style)FindResource("DialogButton"),
            Content = capturing ? "Press keys..." : failed ? $"{hotkey.Display}  ⚠" : hotkey.Display,
            Tag = info.Id,
            MinWidth = 150,
            ToolTip = failed ? "Another app is already using this shortcut. Pick a different one." : "Click, then press the new keys",
        };
        if (capturing) keys.Background = CAPTURE_BACKGROUND;
        if (failed) keys.Foreground = WARNING_FOREGROUND;
        keys.Click += OnKeysClick;
        Grid.SetColumn(keys, 1);
        row.Children.Add(keys);

        if (hotkey != info.Default)
        {
            Button reset = new()
            {
                Style = (Style)FindResource("DialogButton"),
                Content = "",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                Padding = new Thickness(8, 7, 8, 7),
                Margin = new Thickness(6, 0, 0, 0),
                Tag = info.Id,
                ToolTip = $"Reset to {info.Default.Display}",
            };
            reset.Click += OnResetShortcutClick;
            Grid.SetColumn(reset, 2);
            row.Children.Add(reset);
        }
        return row;
    }

    void OnKeysClick(object sender, RoutedEventArgs e)
    {
        Shortcut id = (Shortcut)((FrameworkElement)sender).Tag;
        bool wasCapturing = __capturing == id;
        CancelCapture();

        if (!wasCapturing)
        {
            __capturing = id;
            // Otherwise pressing a global shortcut here would trigger it instead of recording it
            _overlay.PauseHotkeys(true);
            _shortcutMessage.Text = "";
        }
        Refresh();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (__capturing is not Shortcut id)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

        Hotkey hotkey = new(key, Keyboard.Modifiers);
        ShortcutInfo info = ShortcutInfo.Of(id);
        if (info.IsGlobal && !hotkey.HasCtrlOrAlt)
        {
            _shortcutMessage.Text = "Shortcuts that work in any app need Ctrl or Alt, so they don't take over normal typing.";
            return;
        }

        CancelCapture();
        Shortcut? taken = _overlay.SetShortcut(id, hotkey);
        _shortcutMessage.Text = "";
        if (taken is Shortcut other) _shortcutMessage.Text = $"{hotkey.Display} was used by \"{ShortcutInfo.Of(other).Label}\", which now has no shortcut.";
        if (info.IsGlobal && _overlay.HotkeyFailed(id)) _shortcutMessage.Text = $"{hotkey.Display} is already used by another app. Pick a different one.";
        Refresh();
    }

    void CancelCapture()
    {
        if (__capturing == null) return;

        __capturing = null;
        _overlay.PauseHotkeys(false);
        Refresh();
    }

    void OnResetShortcutClick(object sender, RoutedEventArgs e)
    {
        Shortcut id = (Shortcut)((FrameworkElement)sender).Tag;
        CancelCapture();
        Shortcut? taken = _overlay.SetShortcut(id, ShortcutInfo.Of(id).Default);
        _shortcutMessage.Text = taken is Shortcut other ? $"\"{ShortcutInfo.Of(other).Label}\" now has no shortcut." : "";
        Refresh();
    }

    void OnResetShortcutsClick(object sender, RoutedEventArgs e)
    {
        CancelCapture();
        _overlay.ResetShortcuts();
        _shortcutMessage.Text = "";
        Refresh();
    }

    #endregion

    void OnStartupCheckClick(object sender, RoutedEventArgs e)
    {
        Settings.ShowOnStartup = _startupCheck.IsChecked == true;
        Settings.Save();
    }

    void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    void OnStartDrawingClick(object sender, RoutedEventArgs e)
    {
        Close();
        if (_overlay.CurrentTool == Tool.Mouse) _overlay.ToggleDrawMode();
    }
}
