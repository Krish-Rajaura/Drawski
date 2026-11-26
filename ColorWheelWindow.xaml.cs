using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Drawski;

public partial class ColorWheelWindow : Window
{
    const double WHEEL_SIZE = 240, MARKER_HALF = 9;

    public Color SelectedColor { get; private set; }

    readonly Color _initialColor;

    double __hue, __saturation, __value = 1;
    bool __updatingHex;
    UIElement __dragging;

    public ColorWheelWindow(Color initialColor)
    {
        InitializeComponent();
        _initialColor = initialColor;
        _oldPreview.Background = new SolidColorBrush(initialColor);
        (__hue, __saturation, __value) = ToHsv(initialColor);

        Loaded += (_, _) =>
        {
            DrawWheel();
            UpdateColor(true);
            _hexBox.Focus();
            _hexBox.SelectAll();
        };
    }

    // Hue goes round the wheel, saturation goes out from the centre, the brightness bar darkens the whole wheel
    void DrawWheel()
    {
        double dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int size = (int)Math.Round(WHEEL_SIZE * dpiScale);
        double radius = size / 2.0;
        byte[] pixels = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x + 0.5 - radius, dy = y + 0.5 - radius;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double alpha = Math.Clamp(radius - distance, 0, 1);
                if (alpha <= 0) continue;

                double hue = (Math.Atan2(-dy, dx) * 180 / Math.PI + 360) % 360;
                Color color = FromHsv(hue, Math.Min(distance / radius, 1), __value);
                int i = (y * size + x) * 4;
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = (byte)(alpha * 255);
            }
        }

        _wheel.Source = BitmapSource.Create(size, size, 96 * dpiScale, 96 * dpiScale, PixelFormats.Bgra32, null, pixels, size * 4);
    }

    void UpdateColor(bool updateHex)
    {
        SelectedColor = FromHsv(__hue, __saturation, __value);
        _newPreview.Background = new SolidColorBrush(SelectedColor);
        _valueBar.Background = new LinearGradientBrush(Colors.Black, FromHsv(__hue, __saturation, 1), 0);

        double radius = WHEEL_SIZE / 2, angle = __hue * Math.PI / 180;
        Canvas.SetLeft(_wheelMarker, radius + Math.Cos(angle) * __saturation * radius - MARKER_HALF);
        Canvas.SetTop(_wheelMarker, radius - Math.Sin(angle) * __saturation * radius - MARKER_HALF);
        Canvas.SetLeft(_valueMarker, __value * (WHEEL_SIZE - 2 * MARKER_HALF));

        if (!updateHex) return;

        __updatingHex = true;
        _hexBox.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        __updatingHex = false;
    }

    void SetColor(Color color)
    {
        (__hue, __saturation, __value) = ToHsv(color);
        DrawWheel();
        UpdateColor(true);
    }

    void OnWheelDown(object sender, MouseButtonEventArgs e)
    {
        __dragging = _wheelHost;
        _wheelHost.CaptureMouse();
        PickFromWheel(e.GetPosition(_wheelHost));
    }

    void OnWheelMove(object sender, MouseEventArgs e)
    {
        if (__dragging == _wheelHost) PickFromWheel(e.GetPosition(_wheelHost));
    }

    void OnValueDown(object sender, MouseButtonEventArgs e)
    {
        __dragging = _valueHost;
        _valueHost.CaptureMouse();
        PickValue(e.GetPosition(_valueHost));
    }

    void OnValueMove(object sender, MouseEventArgs e)
    {
        if (__dragging == _valueHost) PickValue(e.GetPosition(_valueHost));
    }

    void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        __dragging?.ReleaseMouseCapture();
        __dragging = null;
    }

    void PickFromWheel(Point point)
    {
        double radius = WHEEL_SIZE / 2, dx = point.X - radius, dy = point.Y - radius;
        __hue = (Math.Atan2(-dy, dx) * 180 / Math.PI + 360) % 360;
        __saturation = Math.Min(Math.Sqrt(dx * dx + dy * dy) / radius, 1);
        UpdateColor(true);
    }

    void PickValue(Point point)
    {
        __value = Math.Clamp((point.X - MARKER_HALF) / (WHEEL_SIZE - 2 * MARKER_HALF), 0, 1);
        DrawWheel();
        UpdateColor(true);
    }

    void OnHexChanged(object sender, TextChangedEventArgs e)
    {
        if (__updatingHex) return;

        string text = _hexBox.Text.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (text.Length != 7) return;

        try
        {
            (__hue, __saturation, __value) = ToHsv((Color)ColorConverter.ConvertFromString(text));
        }
        catch (FormatException)
        {
            return;
        }
        DrawWheel();
        UpdateColor(false);
    }

    void OnOldPreviewClick(object sender, MouseButtonEventArgs e) => SetColor(_initialColor);

    void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;

    Color FromHsv(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - chroma;
        (double r, double g, double b) = (int)(hue / 60) switch
        {
            0 => (chroma, x, 0d),
            1 => (x, chroma, 0d),
            2 => (0d, chroma, x),
            3 => (0d, x, chroma),
            4 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    (double hue, double saturation, double value) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;

        double hue = 0;
        if (delta > 0 && max == r) hue = 60 * ((g - b) / delta % 6);
        else if (delta > 0 && max == g) hue = 60 * ((b - r) / delta + 2);
        else if (delta > 0) hue = 60 * ((r - g) / delta + 4);
        if (hue < 0) hue += 360;

        return (hue, max == 0 ? 0 : delta / max, max);
    }
}
