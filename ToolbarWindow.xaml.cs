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

    public ToolbarWindow(OverlayWindow overlay)
    {
        InitializeComponent();
        _overlay = overlay;

        _toolButtons.Add(_mouseButton);
        _toolButtons.AddRange(_toolsGrid.Children.OfType<ToggleButton>());
        _sizeButtons.AddRange(_sizesGrid.Children.OfType<ToggleButton>());

        foreach (Color color in OverlayWindow.PALETTE)
        {
            RadioButton swatch = new() { Style = (Style)FindResource("Swatch"), Background = new SolidColorBrush(color), Tag = color };
            swatch.Click += OnColorClick;
            _colorsGrid.Children.Add(swatch);
            _colorButtons.Add(swatch);
        }

        _overlay.onStateChanged += Sync;
        Loaded += OnLoaded;
    }

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
        foreach (ToggleButton button in _colorButtons)
            button.IsChecked = (Color)button.Tag == _overlay.CurrentColor;
        foreach (ToggleButton button in _sizeButtons)
            button.IsChecked = int.Parse((string)button.Tag) == _overlay.SizeIndex;

        _boardButton.IsChecked = _overlay.BoardOn;
        _hideButton.IsChecked = _overlay.InkHidden;
        _hideButton.Content = _overlay.InkHidden ? "" : "";
    }

    void OnGripDown(object sender, MouseButtonEventArgs e) => DragMove();

    void OnToolClick(object sender, RoutedEventArgs e) => _overlay.SetTool(Enum.Parse<Tool>((string)((FrameworkElement)sender).Tag));

    void OnColorClick(object sender, RoutedEventArgs e) => _overlay.SetColor((Color)((FrameworkElement)sender).Tag);

    void OnSizeClick(object sender, RoutedEventArgs e) => _overlay.SetSize(int.Parse((string)((FrameworkElement)sender).Tag));

    void OnUndoClick(object sender, RoutedEventArgs e) => _overlay.Undo();

    void OnRedoClick(object sender, RoutedEventArgs e) => _overlay.Redo();

    void OnBoardClick(object sender, RoutedEventArgs e) => _overlay.ToggleBoard();

    void OnHideClick(object sender, RoutedEventArgs e) => _overlay.ToggleInkHidden();

    void OnSaveClick(object sender, RoutedEventArgs e) => _ = _overlay.Save();

    void OnOpenClick(object sender, RoutedEventArgs e) => _overlay.Open();

    void OnClearClick(object sender, RoutedEventArgs e) => _overlay.Clear();

    void OnCloseClick(object sender, RoutedEventArgs e) => _overlay.Quit();
}
