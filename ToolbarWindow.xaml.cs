using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Drawski;

public partial class ToolbarWindow : Window
{
    readonly OverlayWindow _overlay;
    readonly List<ToggleButton> _toolButtons = new(), _colorButtons = new(), _sizeButtons = new();
    // Tooltip label + the shortcuts it shows, refreshed whenever keys are rebound
    readonly List<(FrameworkElement element, string label, Shortcut[] shortcuts)> _tooltips = new();

    public ToolbarWindow(OverlayWindow overlay)
    {
        InitializeComponent();
        _overlay = overlay;

        _toolButtons.Add(_mouseButton);
        _toolButtons.AddRange(_toolsGrid.Children.OfType<ToggleButton>());
        _sizeButtons.AddRange(_sizesGrid.Children.OfType<ToggleButton>());

        for (int i = 0; i < _overlay.Settings.Palette.Length; i++)
        {
            RadioButton swatch = new() { Style = (Style)FindResource("Swatch"), Tag = i };
            swatch.Click += OnColorClick;
            _colorsGrid.Children.Add(swatch);
            _colorButtons.Add(swatch);
            AddTooltip(swatch, $"Colour {i + 1}", Shortcut.Color1 + i);
        }

        AddTooltip(_mouseButton, (string)_mouseButton.ToolTip, Shortcut.Mouse, Shortcut.ToggleDraw);
        foreach (ToggleButton button in _toolButtons.Skip(1))
            AddTooltip(button, (string)button.ToolTip, Enum.Parse<Shortcut>((string)button.Tag));
        AddTooltip(_thinButton, (string)_thinButton.ToolTip, Shortcut.SizeDown);
        AddTooltip(_thickButton, (string)_thickButton.ToolTip, Shortcut.SizeUp);
        AddTooltip(_undoButton, (string)_undoButton.ToolTip, Shortcut.Undo);
        AddTooltip(_redoButton, (string)_redoButton.ToolTip, Shortcut.Redo);
        AddTooltip(_boardButton, (string)_boardButton.ToolTip, Shortcut.Whiteboard);
        AddTooltip(_hideButton, (string)_hideButton.ToolTip, Shortcut.HideInk);
        AddTooltip(_saveButton, (string)_saveButton.ToolTip, Shortcut.Save);
        AddTooltip(_openButton, (string)_openButton.ToolTip, Shortcut.Open);
        AddTooltip(_exportButton, (string)_exportButton.ToolTip, Shortcut.Export);
        AddTooltip(_clearButton, (string)_clearButton.ToolTip, Shortcut.Clear, Shortcut.ClearAll);

        _overlay.onStateChanged += Sync;
        Loaded += OnLoaded;
    }

    void AddTooltip(FrameworkElement element, string label, params Shortcut[] shortcuts) => _tooltips.Add((element, label, shortcuts));

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Clicking the toolbar shouldn't steal focus from the pdf/app underneath
        Native.SetExStyle(new WindowInteropHelper(this).Handle, Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW, true);
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 16;
        Top = workArea.Top + (workArea.Height - ActualHeight) / 2;
        Sync();
    }

    void Sync()
    {
        foreach (ToggleButton button in _toolButtons)
            button.IsChecked = (string)button.Tag == _overlay.CurrentTool.ToString();
        Color[] palette = _overlay.Settings.Palette;
        foreach (ToggleButton button in _colorButtons)
        {
            Color color = palette[(int)button.Tag];
            button.Background = new SolidColorBrush(color);
            button.IsChecked = color == _overlay.CurrentColor;
        }
        bool customColor = !palette.Contains(_overlay.CurrentColor);
        _customColorButton.IsChecked = customColor;
        _customColorButton.Background = new SolidColorBrush(_overlay.CurrentColor);
        foreach (ToggleButton button in _sizeButtons)
            button.IsChecked = int.Parse((string)button.Tag) == _overlay.SizeIndex;

        _boardButton.IsChecked = _overlay.BoardOn;
        _hideButton.IsChecked = _overlay.InkHidden;
        _hideButton.Content = _overlay.InkHidden ? "" : "";
        _exportButton.Visibility = _overlay.PdfOpen ? Visibility.Visible : Visibility.Collapsed;

        foreach ((FrameworkElement element, string label, Shortcut[] shortcuts) in _tooltips)
        {
            string[] keys = shortcuts.Select(id => _overlay.Settings.Keys[id]).Where(key => !key.IsNone).Select(key => key.Display).ToArray();
            element.ToolTip = keys.Length == 0 ? label : $"{label} ({string.Join(" / ", keys)})";
        }
    }

    void OnGripDown(object sender, MouseButtonEventArgs e) => DragMove();

    void OnToolClick(object sender, RoutedEventArgs e) => _overlay.SetTool(Enum.Parse<Tool>((string)((FrameworkElement)sender).Tag));

    void OnColorClick(object sender, RoutedEventArgs e) => _overlay.SetColor(_overlay.Settings.Palette[(int)((FrameworkElement)sender).Tag]);

    void OnCustomColorClick(object sender, RoutedEventArgs e)
    {
        _overlay.PickCustomColor();
        Sync();
    }

    void OnSizeClick(object sender, RoutedEventArgs e) => _overlay.SetSize(int.Parse((string)((FrameworkElement)sender).Tag));

    void OnUndoClick(object sender, RoutedEventArgs e) => _overlay.Undo();

    void OnRedoClick(object sender, RoutedEventArgs e) => _overlay.Redo();

    void OnBoardClick(object sender, RoutedEventArgs e) => _overlay.ToggleBoard();

    void OnHideClick(object sender, RoutedEventArgs e) => _overlay.ToggleInkHidden();

    void OnSaveClick(object sender, RoutedEventArgs e) => _ = _overlay.Save();

    void OnOpenClick(object sender, RoutedEventArgs e) => _overlay.Open();

    void OnExportClick(object sender, RoutedEventArgs e) => _overlay.ExportPdf();

    void OnClearClick(object sender, RoutedEventArgs e) => _overlay.Clear();

    void OnSettingsClick(object sender, RoutedEventArgs e) => _overlay.OpenSettings();

    void OnCloseClick(object sender, RoutedEventArgs e) => _overlay.Quit();
}
