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
    public static readonly Color[] PALETTE =
    {
        Color.FromRgb(255, 59, 48), Color.FromRgb(255, 214, 10), Color.FromRgb(52, 199, 89),
        Color.FromRgb(10, 132, 255), Color.FromRgb(17, 17, 17), Color.FromRgb(255, 255, 255),
    };

    const int HOTKEY_TOGGLE = 1, HOTKEY_CLEAR = 2, HOTKEY_HIDE = 3;
    const double LASER_HOLD = 1.0, LASER_FADE = 0.6;
    static readonly double[] SIZES = { 2.5, 5, 10 };
    static readonly Brush HIT_BACKGROUND = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));
    static readonly Brush BOARD_BACKGROUND = new SolidColorBrush(Color.FromRgb(250, 250, 248));

    public Action onStateChanged;

    public Tool CurrentTool { get; private set; } = Tool.Mouse;
    public Color CurrentColor { get; private set; } = PALETTE[0];
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

        uint modifiers = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT;
        Native.RegisterHotKey(__hwnd, HOTKEY_TOGGLE, modifiers, (uint)KeyInterop.VirtualKeyFromKey(Key.D));
        Native.RegisterHotKey(__hwnd, HOTKEY_CLEAR, modifiers, (uint)KeyInterop.VirtualKeyFromKey(Key.C));
        Native.RegisterHotKey(__hwnd, HOTKEY_HIDE, modifiers, (uint)KeyInterop.VirtualKeyFromKey(Key.H));

        _toolbar.Owner = this;
        _toolbar.Show();
        ApplyState();

        if (_startupFile != null && Path.GetExtension(_startupFile).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(_startupFile))
            OpenPdf(_startupFile);
    }

    protected override void OnClosed(EventArgs e)
    {
        Native.UnregisterHotKey(__hwnd, HOTKEY_TOGGLE);
        Native.UnregisterHotKey(__hwnd, HOTKEY_CLEAR);
        Native.UnregisterHotKey(__hwnd, HOTKEY_HIDE);
        base.OnClosed(e);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_DISPLAYCHANGE || msg == Native.WM_DPICHANGED)
            Dispatcher.BeginInvoke(() => Native.CoverVirtualScreen(__hwnd));

        if (msg != Native.WM_HOTKEY) return IntPtr.Zero;

        switch (wParam.ToInt32())
        {
            case HOTKEY_TOGGLE: ToggleDrawMode(); break;
            case HOTKEY_CLEAR: Clear(); break;
            case HOTKEY_HIDE: ToggleInkHidden(); break;
        }
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
        if (HandleKey(e.Key)) e.Handled = true;
    }

    // Shared with the pdf window
    public bool HandleKey(Key key)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (key == Key.Z && shift) Redo();
            else if (key == Key.Z) Undo();
            else if (key == Key.Y) Redo();
            else if (key == Key.S) _ = Save();
            else if (key == Key.O) Open();
            else if (key == Key.E) ExportPdf();
            else return false;
            return true;
        }

        switch (key)
        {
            case Key.Escape: SetTool(Tool.Mouse); break;
            case Key.P: SetTool(Tool.Pen); break;
            case Key.H: SetTool(Tool.Highlighter); break;
            case Key.X: SetTool(Tool.Laser); break;
            case Key.L: SetTool(Tool.Line); break;
            case Key.A: SetTool(Tool.Arrow); break;
            case Key.R: SetTool(Tool.Rectangle); break;
            case Key.O: SetTool(Tool.Ellipse); break;
            case Key.E: SetTool(Tool.Eraser); break;
            case Key.B: ToggleBoard(); break;
            case Key.Delete: Clear(); break;
            case Key.OemOpenBrackets: SetSize(SizeIndex - 1); break;
            case Key.OemCloseBrackets: SetSize(SizeIndex + 1); break;
            case >= Key.D1 and <= Key.D6: SetColor(PALETTE[key - Key.D1]); break;
            default: return false;
        }
        return true;
    }

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
