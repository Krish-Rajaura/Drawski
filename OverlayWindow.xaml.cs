using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Drawski;

public enum Tool { Mouse, Pen, Highlighter, Laser, Line, Arrow, Rectangle, Ellipse, Eraser }

public partial class OverlayWindow : Window
{
    const double LASER_HOLD = 1.0, LASER_FADE = 0.6;
    static readonly double[] SIZES = { 2.5, 5, 10 };
    static readonly Brush HIT_BACKGROUND = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));
    static readonly Brush BOARD_BACKGROUND = new SolidColorBrush(Color.FromRgb(250, 250, 248));

    public Action onStateChanged;

    public readonly Settings Settings = new();

    public Tool CurrentTool { get; private set; } = Tool.Mouse;
    public Color CurrentColor { get; private set; }
    public int SizeIndex { get; private set; } = 1;
    public double CurrentSize => SIZES[SizeIndex];
    public bool BoardOn { get; private set; }
    public bool InkHidden { get; private set; }
    public bool PdfOpen => __pdf != null;

    readonly StrokeCollection _overlayStrokes = new(), _boardStrokes = new();
    readonly InkHistory _overlayHistory = new(), _boardHistory = new();
    readonly InkSurface _surface;
    readonly ToolbarWindow _toolbar;

    readonly string _startupFile;

    IntPtr __hwnd;
    Tool __lastDrawTool = Tool.Pen;
    PdfWindow __pdf;
    SettingsWindow __settingsWindow;
    readonly HashSet<Shortcut> __failedHotkeys = new();
    readonly List<(Stroke stroke, StrokeCollection owner, DateTime time)> __laserStrokes = new();
    readonly DispatcherTimer __laserTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };

    // Pdf open = tools draw on its pages, the overlay just stays click-through (unless whiteboard is up)
    bool PdfActive => __pdf != null && !BoardOn;
    InkHistory ActiveHistory => BoardOn ? _boardHistory : _overlayHistory;
    bool HasUnsavedInk => (_overlayHistory.Dirty && _overlayStrokes.Any(stroke => !InkSurface.IsLaser(stroke)))
                       || (_boardHistory.Dirty && _boardStrokes.Any(stroke => !InkSurface.IsLaser(stroke)));

    public OverlayWindow(string startupFile)
    {
        InitializeComponent();
        _startupFile = startupFile;
        Settings.Load();
        CurrentColor = Settings.Palette[0];

        _overlayHistory.Track(_overlayStrokes);
        _boardHistory.Track(_boardStrokes);
        _canvas.Strokes = _overlayStrokes;
        _surface = new InkSurface(this, _canvas);
        __laserTimer.Tick += OnLaserTick;

        _toolbar = new ToolbarWindow(this);
        Loaded += OnLoaded;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        __hwnd = new WindowInteropHelper(this).Handle;
        Native.SetExStyle(__hwnd, Native.WS_EX_TOOLWINDOW, true);
        Native.CoverVirtualScreen(__hwnd);
        HwndSource.FromHwnd(__hwnd).AddHook(WndProc);
        RegisterHotkeys();

        _toolbar.Owner = this;
        _toolbar.Show();
        ApplyState();

        if (_startupFile != null && Path.GetExtension(_startupFile).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(_startupFile))
            OpenPdf(_startupFile);
        else if (Settings.ShowOnStartup)
            OpenSettings();
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterHotkeys();
        base.OnClosed(e);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_DISPLAYCHANGE || msg == Native.WM_DPICHANGED)
            Dispatcher.BeginInvoke(() => Native.CoverVirtualScreen(__hwnd));

        if (msg != Native.WM_HOTKEY) return IntPtr.Zero;

        Execute((Shortcut)(wParam.ToInt32() - 1));
        handled = true;
        return IntPtr.Zero;
    }

    public void SetTool(Tool tool)
    {
        CurrentTool = tool;
        if (tool != Tool.Mouse)
        {
            __lastDrawTool = tool;
            InkHidden = false;
        }
        ApplyState();
    }

    public void ToggleDrawMode() => SetTool(CurrentTool == Tool.Mouse ? __lastDrawTool : Tool.Mouse);

    public void SetColor(Color color)
    {
        CurrentColor = color;
        if (CurrentTool == Tool.Mouse || CurrentTool == Tool.Eraser) CurrentTool = __lastDrawTool;
        ApplyState();
    }

    public void SetSize(int index)
    {
        SizeIndex = Math.Clamp(index, 0, SIZES.Length - 1);
        ApplyState();
    }

    public void ToggleBoard()
    {
        BoardOn = !BoardOn;
        if (BoardOn && CurrentTool == Tool.Mouse) CurrentTool = __lastDrawTool;
        ApplyState();
    }

    public void ToggleInkHidden()
    {
        InkHidden = !InkHidden;
        if (InkHidden) CurrentTool = Tool.Mouse;
        ApplyState();
    }

    public void Undo()
    {
        if (PdfActive) __pdf.History.Undo();
        else ActiveHistory.Undo();
    }

    public void Redo()
    {
        if (PdfActive) __pdf.History.Redo();
        else ActiveHistory.Redo();
    }

    public void Clear()
    {
        if (PdfActive) __pdf.ClearInk();
        else _canvas.Strokes.Clear();
    }

    void ApplyState()
    {
        bool drawing = CurrentTool != Tool.Mouse;
        bool capturing = drawing && (__pdf == null || BoardOn);

        Native.SetExStyle(__hwnd, Native.WS_EX_TRANSPARENT, !capturing);
        _canvas.Strokes = BoardOn ? _boardStrokes : _overlayStrokes;
        _canvas.Background = BoardOn ? BOARD_BACKGROUND : HIT_BACKGROUND;
        _canvas.Visibility = InkHidden ? Visibility.Hidden : Visibility.Visible;
        _surface.Apply();
        _drawBorder.Visibility = capturing && !BoardOn ? Visibility.Visible : Visibility.Collapsed;

        if (capturing) Activate();
        else if (drawing && __pdf != null) __pdf.Activate();
        onStateChanged?.Invoke();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (HandleKey(e)) e.Handled = true;
    }

    // Shared with the pdf window
    public bool HandleKey(KeyEventArgs e)
    {
        Hotkey pressed = new(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
        foreach (ShortcutInfo info in ShortcutInfo.ALL)
        {
            if (info.IsGlobal || Settings.Keys[info.Id] != pressed) continue;

            Execute(info.Id);
            return true;
        }
        return false;
    }

    public void Execute(Shortcut shortcut)
    {
        switch (shortcut)
        {
            case Shortcut.ToggleDraw: ToggleDrawMode(); break;
            case Shortcut.ClearAll or Shortcut.Clear: Clear(); break;
            case Shortcut.HideInk: ToggleInkHidden(); break;
            case >= Shortcut.Mouse and <= Shortcut.Ellipse: SetTool(Enum.Parse<Tool>(shortcut.ToString())); break;
            case >= Shortcut.Color1 and <= Shortcut.Color6: SetColor(Settings.Palette[shortcut - Shortcut.Color1]); break;
            case Shortcut.SizeDown: SetSize(SizeIndex - 1); break;
            case Shortcut.SizeUp: SetSize(SizeIndex + 1); break;
            case Shortcut.Whiteboard: ToggleBoard(); break;
            case Shortcut.Undo: Undo(); break;
            case Shortcut.Redo: Redo(); break;
            case Shortcut.Save: _ = Save(); break;
            case Shortcut.Open: Open(); break;
            case Shortcut.Export: ExportPdf(); break;
        }
    }

    #region Settings

    public void OpenSettings()
    {
        // Drawing mode would cover the settings window
        if (CurrentTool != Tool.Mouse) SetTool(Tool.Mouse);

        if (__settingsWindow != null)
        {
            if (__settingsWindow.WindowState == WindowState.Minimized) __settingsWindow.WindowState = WindowState.Normal;
            __settingsWindow.Activate();
            return;
        }

        __settingsWindow = new SettingsWindow(this);
        __settingsWindow.Closed += (_, _) => __settingsWindow = null;
        __settingsWindow.Show();
    }

    public void PickCustomColor()
    {
        ColorWheelWindow picker = new(CurrentColor, Settings.SavedColors) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        if (picker.ShowDialog() == true) SetColor(picker.SelectedColor);
    }

    public void SetPaletteColor(int index, Color color)
    {
        if (CurrentColor == Settings.Palette[index]) CurrentColor = color;
        Settings.Palette[index] = color;
        SaveSettings();
    }

    public void ResetPalette()
    {
        for (int i = 0; i < Settings.Palette.Length; i++)
        {
            if (CurrentColor == Settings.Palette[i]) CurrentColor = Settings.DEFAULT_PALETTE[i];
            Settings.Palette[i] = Settings.DEFAULT_PALETTE[i];
        }
        SaveSettings();
    }

    // Returns the shortcut that lost these keys, if another one had them
    public Shortcut? SetShortcut(Shortcut id, Hotkey hotkey)
    {
        Shortcut? taken = null;
        foreach ((Shortcut other, Hotkey keys) in Settings.Keys.ToList())
        {
            if (other == id || keys != hotkey || hotkey.IsNone) continue;

            Settings.Keys[other] = Hotkey.NONE;
            taken = other;
        }
        Settings.Keys[id] = hotkey;
        SaveSettings();
        return taken;
    }

    public void ResetShortcuts()
    {
        foreach (ShortcutInfo info in ShortcutInfo.ALL)
            Settings.Keys[info.Id] = info.Default;
        SaveSettings();
    }

    public bool HotkeyFailed(Shortcut id) => __failedHotkeys.Contains(id);

    public void PauseHotkeys(bool paused)
    {
        if (paused) UnregisterHotkeys();
        else RegisterHotkeys();
    }

    public void SaveSettings()
    {
        Settings.Save();
        RegisterHotkeys();
        ApplyState();
    }

    void RegisterHotkeys()
    {
        UnregisterHotkeys();
        foreach (ShortcutInfo info in ShortcutInfo.ALL.Where(info => info.IsGlobal))
        {
            Hotkey hotkey = Settings.Keys[info.Id];
            if (hotkey.IsNone) continue;

            // ModifierKeys uses the same bit values as the Win32 MOD_ flags
            uint modifiers = (uint)hotkey.Modifiers | Native.MOD_NOREPEAT;
            if (!Native.RegisterHotKey(__hwnd, HotkeyId(info.Id), modifiers, (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key)))
                __failedHotkeys.Add(info.Id);
        }
    }

    void UnregisterHotkeys()
    {
        foreach (ShortcutInfo info in ShortcutInfo.ALL.Where(info => info.IsGlobal))
            Native.UnregisterHotKey(__hwnd, HotkeyId(info.Id));
        __failedHotkeys.Clear();
    }

    int HotkeyId(Shortcut id) => (int)id + 1;

    #endregion

    #region Laser

    public void TrackLaser(Stroke stroke, StrokeCollection owner)
    {
        __laserStrokes.Add((stroke, owner, DateTime.Now));
        __laserTimer.Start();
    }

    void OnLaserTick(object sender, EventArgs e)
    {
        DateTime now = DateTime.Now;
        for (int i = __laserStrokes.Count - 1; i >= 0; i--)
        {
            (Stroke stroke, StrokeCollection owner, DateTime time) = __laserStrokes[i];
            double fadeTime = (now - time).TotalSeconds - LASER_HOLD;
            if (fadeTime < 0) continue;

            if (fadeTime >= LASER_FADE)
            {
                if (owner.Contains(stroke)) owner.Remove(stroke);
                __laserStrokes.RemoveAt(i);
                continue;
            }

            Color color = stroke.DrawingAttributes.Color;
            color.A = (byte)(255 * (1 - fadeTime / LASER_FADE));
            stroke.DrawingAttributes.Color = color;
        }

        if (__laserStrokes.Count == 0) __laserTimer.Stop();
    }

    #endregion

    #region Files

    // Pdf ink goes to its sidecar file, otherwise ask for png (screen + ink) or isf (reopenable ink)
    public async Task<bool> Save()
    {
        if (PdfActive) return __pdf.SaveInk();

        Visibility borderVisibility = _drawBorder.Visibility;
        _toolbar.Visibility = Visibility.Hidden;
        _drawBorder.Visibility = Visibility.Collapsed;
        await Task.Delay(150);
        BitmapSource snapshot = Native.CaptureVirtualScreen();
        _toolbar.Visibility = Visibility.Visible;
        _drawBorder.Visibility = borderVisibility;

        SaveFileDialog dialog = new()
        {
            Title = "Save drawing",
            FileName = $"Drawski {DateTime.Now:yyyy-MM-dd HH-mm}",
            Filter = "Screenshot with ink (*.png)|*.png|Drawski ink, can be reopened (*.isf)|*.isf",
        };
        if (dialog.ShowDialog(this) != true) return false;

        try
        {
            using FileStream stream = File.Create(dialog.FileName);
            if (Path.GetExtension(dialog.FileName).Equals(".isf", StringComparison.OrdinalIgnoreCase))
                new StrokeCollection(_canvas.Strokes.Where(stroke => !InkSurface.IsLaser(stroke))).Save(stream);
            else
            {
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(snapshot));
                encoder.Save(stream);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Couldn't save:\n{exception.Message}", "Drawski", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        ActiveHistory.Dirty = false;
        return true;
    }

    public void Open()
    {
        OpenFileDialog dialog = new() { Title = "Open", Filter = "PDF or Drawski ink (*.pdf;*.isf)|*.pdf;*.isf" };
        if (dialog.ShowDialog(this) != true) return;

        if (Path.GetExtension(dialog.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            OpenPdf(dialog.FileName);
            return;
        }

        try
        {
            using FileStream stream = File.OpenRead(dialog.FileName);
            _canvas.Strokes.Add(new StrokeCollection(stream));
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Couldn't open:\n{exception.Message}", "Drawski", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ExportPdf() => __pdf?.ExportPdf();

    void OpenPdf(string path)
    {
        __pdf?.Close();
        if (__pdf != null) return;

        BoardOn = false;
        __pdf = new PdfWindow(this, path);
        __pdf.Closed += OnPdfClosed;
        __pdf.Show();
        ApplyState();
    }

    void OnPdfClosed(object sender, EventArgs e)
    {
        if (sender != __pdf) return;

        __pdf = null;
        ApplyState();
    }

    public async void Quit()
    {
        __pdf?.Close();
        if (__pdf != null) return;

        if (HasUnsavedInk)
        {
            MessageBoxResult result = MessageBox.Show(this, "Save your drawings before quitting?", "Drawski",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes && !await Save()) return;
        }

        Application.Current.Shutdown();
    }

    #endregion
}
