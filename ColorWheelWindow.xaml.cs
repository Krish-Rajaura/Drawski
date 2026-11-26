using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Drawski;

public partial class ColorWheelWindow : Window
{
    public Color SelectedColor => _picker.SelectedColor;

    readonly Color _initialColor;

    public ColorWheelWindow(Color initialColor, IReadOnlyList<Color> savedColors)
    {
        InitializeComponent();
        _initialColor = initialColor;
        _oldPreview.Background = new SolidColorBrush(initialColor);
        _picker.onColorChanged += () => _newPreview.Background = new SolidColorBrush(_picker.SelectedColor);
        _picker.SetColor(initialColor);

        _savedSection.Visibility = savedColors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (Color color in savedColors)
        {
            Button swatch = new() { Style = (Style)FindResource("SmallSwatch"), Background = new SolidColorBrush(color), Tag = color };
            swatch.Click += (_, _) => _picker.SetColor((Color)swatch.Tag);
            _savedPanel.Children.Add(swatch);
        }

        Loaded += (_, _) => _picker.FocusHex();
    }

    void OnOldPreviewClick(object sender, MouseButtonEventArgs e) => _picker.SetColor(_initialColor);

    void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
