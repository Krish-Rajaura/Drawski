using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Win32;

namespace Drawski;

public partial class PdfWindow : Window
{
    const double PAGE_GAP = 16, MIN_ZOOM = 0.25, MAX_ZOOM = 4, MAX_RENDER_WIDTH = 4000;
    const string INK_EXTENSION = ".drawski", INK_HEADER = "DRAWSKI-PDF-1";

    public readonly InkHistory History = new();

    readonly OverlayWindow _overlay;
    readonly string _pdfPath;
    readonly List<PageSlot> _pages = new();
    readonly DispatcherTimer __renderTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    PdfDocument __document;
    double __zoom = 1, __dpiScale = 1, __renderFactor = 1;

    string SidecarPath => _pdfPath + INK_EXTENSION;

    // Used when the pdf's folder isn't writable
    string FallbackPath
    {
        get
        {
            byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(_pdfPath.ToLowerInvariant()));
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Drawski", "ink");
            return Path.Combine(folder, Convert.ToHexString(hash) + INK_EXTENSION);
        }
    }

    public PdfWindow(OverlayWindow overlay, string pdfPath)
    {
        InitializeComponent();
        _overlay = overlay;
        _pdfPath = Path.GetFullPath(pdfPath);
        Title = $"{Path.GetFileName(_pdfPath)} - Drawski";

        _overlay.onStateChanged += OnToolChanged;
        History.onChanged += UpdateTitle;
        __renderTimer.Tick += OnRenderTimerTick;
        Loaded += OnLoaded;
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        __dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(_pdfPath);
            __document = await PdfDocument.LoadFromFileAsync(file);
            if (__document.PageCount > 0) __renderFactor = await MeasureRenderFactor();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Couldn't open the pdf:\n{exception.Message}", "Drawski", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        Dictionary<int, StrokeCollection> savedInk = LoadInk();
        double maxWidth = 0;
        for (uint i = 0; i < __document.PageCount; i++)
        {
            using PdfPage page = __document.GetPage(i);
            Size size = new(page.Size.Width, page.Size.Height);
            StrokeCollection strokes = savedInk.TryGetValue((int)i, out StrokeCollection saved) ? saved : new StrokeCollection();
            History.Track(strokes);

            PageSlot slot = new((int)i, size, strokes)
            {
                Host = new Border
                {
                    Width = size.Width,
                    Height = size.Height,
                    Margin = new Thickness(0, PAGE_GAP, 0, i == __document.PageCount - 1 ? PAGE_GAP : 0),
                    Background = Brushes.White,
                },
            };
            _pages.Add(slot);
            _pagesPanel.Children.Add(slot.Host);
            maxWidth = Math.Max(maxWidth, size.Width);
        }

        SetZoom((_scroller.ActualWidth - 48) / Math.Max(1, maxWidth));
        UpdateTitle();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!History.Dirty) return;

        MessageBoxResult result = MessageBox.Show(this, $"Save your drawings on {Path.GetFileName(_pdfPath)}?", "Drawski",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel || (result == MessageBoxResult.Yes && !SaveInk())) e.Cancel = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _overlay.onStateChanged -= OnToolChanged;
        __renderTimer.Stop();
        foreach (PageSlot slot in _pages) Release(slot);
        __document = null;
        base.OnClosed(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (_overlay.HandleKey(e.Key)) e.Handled = true;
    }

    void OnToolChanged()
    {
        foreach (PageSlot slot in _pages)
        {
            slot.Surface?.Apply();
            if (slot.Canvas != null) slot.Canvas.Visibility = _overlay.InkHidden ? Visibility.Hidden : Visibility.Visible;
        }
    }

    void UpdateTitle() => Title = $"{(History.Dirty ? "• " : "")}{Path.GetFileName(_pdfPath)} - Drawski";

    public void ClearInk() => History.Batch(() =>
    {
        foreach (PageSlot slot in _pages)
            if (slot.Strokes.Count > 0) slot.Strokes.Clear();
    });

    #region Zoom and page virtualization

    void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        e.Handled = true;
        Point mouse = e.GetPosition(_scroller);
        double oldZoom = __zoom, offsetX = _scroller.HorizontalOffset, offsetY = _scroller.VerticalOffset;
        SetZoom(__zoom * Math.Pow(1.1, e.Delta / 120.0));

        // Keep the spot under the cursor in place
        double ratio = __zoom / oldZoom;
        _scroller.UpdateLayout();
        _scroller.ScrollToHorizontalOffset((offsetX + mouse.X) * ratio - mouse.X);
        _scroller.ScrollToVerticalOffset((offsetY + mouse.Y) * ratio - mouse.Y);
    }

    void SetZoom(double zoom)
    {
        __zoom = Math.Clamp(zoom, MIN_ZOOM, MAX_ZOOM);
        _zoomTransform.ScaleX = _zoomTransform.ScaleY = __zoom;
        __renderTimer.Stop();
        __renderTimer.Start();
        UpdatePages();
    }

    void OnRenderTimerTick(object sender, EventArgs e)
    {
        __renderTimer.Stop();
        foreach (PageSlot slot in _pages)
            if (slot.Image != null) Render(slot);
    }

    void OnScrollChanged(object sender, ScrollChangedEventArgs e) => UpdatePages();

    // Only pages near the viewport get an image + canvas, the rest just keep their strokes
    void UpdatePages()
    {
        if (_pages.Count == 0) return;

        double viewTop = _scroller.VerticalOffset / __zoom;
        double viewHeight = Math.Max(1, _scroller.ViewportHeight / __zoom);
        double viewBottom = viewTop + viewHeight;
        double top = 0;
        int currentPage = 0;

        foreach (PageSlot slot in _pages)
        {
            top += PAGE_GAP;
            double bottom = top + slot.Size.Height;

            if (top < viewTop + viewHeight / 2) currentPage = slot.Index;
            if (bottom > viewTop - viewHeight / 2 && top < viewBottom + viewHeight / 2) Realize(slot);
            else if (bottom < viewTop - viewHeight * 1.5 || top > viewBottom + viewHeight * 1.5) Release(slot);

            top = bottom;
        }

        _status.Text = $"Page {currentPage + 1} / {_pages.Count}   {__zoom * 100:0}%";
    }

    void Realize(PageSlot slot)
    {
        if (slot.Image != null) return;

        slot.Image = new Image { Stretch = Stretch.Fill };
        slot.Canvas = new InkCanvas
        {
            Background = Brushes.Transparent,
            Strokes = slot.Strokes,
            Visibility = _overlay.InkHidden ? Visibility.Hidden : Visibility.Visible,
        };
        slot.Surface = new InkSurface(_overlay, slot.Canvas);

        Grid grid = new();
        grid.Children.Add(slot.Image);
        grid.Children.Add(slot.Canvas);
        slot.Host.Child = grid;
        Render(slot);
    }

    void Release(PageSlot slot)
    {
        if (slot.Image == null) return;

        // Canvas subscribes to the page strokes (which live forever), so unhook it or the whole page view leaks
        slot.Canvas.Strokes = new StrokeCollection();
        slot.Image.Source = null;
        slot.Host.Child = null;
        slot.Image = null;
        slot.Canvas = null;
        slot.Surface = null;
        slot.RenderedScale = 0;
    }

    double RenderScale(PageSlot slot) => Math.Min(__zoom * __dpiScale, MAX_RENDER_WIDTH / slot.Size.Width);

    async void Render(PageSlot slot)
    {
        double scale = RenderScale(slot);
        if (slot.Rendering || __document == null || Math.Abs(slot.RenderedScale - scale) < 0.01) return;

        slot.Rendering = true;
        BitmapSource bitmap = null;
        try
        {
            using PdfPage page = __document.GetPage((uint)slot.Index);
            bitmap = await RenderPage(page, slot.Size.Width * scale / __renderFactor);
        }
        catch { }

        slot.Rendering = false;
        if (bitmap == null || slot.Image == null) return;

        slot.Image.Source = bitmap;
        slot.RenderedScale = scale;

        // Zoom changed while this was rendering
        if (Math.Abs(scale - RenderScale(slot)) > 0.01) Render(slot);
    }

    // Bmp skips png compression, Bgr32 is drawn as-is so WPF doesn't keep its own converted copy
    async Task<BitmapSource> RenderPage(PdfPage page, double width)
    {
        using InMemoryRandomAccessStream stream = new();
        await page.RenderToStreamAsync(stream, new PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, width),
            BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId,
        });

        using Stream source = stream.AsStream();
        BitmapDecoder decoder = BitmapDecoder.Create(source, BitmapCreateOptions.None, BitmapCacheOption.None);
        FormatConvertedBitmap converted = new(decoder.Frames[0], PixelFormats.Bgr32, null, 0);
        CachedBitmap bitmap = new(converted, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        bitmap.Freeze();
        return bitmap;
    }

    // The pdf engine scales DestinationWidth by its own dpi on top of ours, measure it once so pages come out at real screen pixels
    async Task<double> MeasureRenderFactor()
    {
        using PdfPage page = __document.GetPage(0);
        BitmapSource probe = await RenderPage(page, 200);
        return probe.PixelWidth > 0 ? probe.PixelWidth / 200.0 : 1;
    }

    #endregion

    #region Export

    // Never touches the original, writes a copy with the ink as real pdf annotations
    public async void ExportPdf()
    {
        List<(int Index, Size Size, StrokeCollection Strokes)> inked = _pages
            .Select(slot => (slot.Index, slot.Size, Strokes: new StrokeCollection(slot.Strokes.Where(stroke => !InkSurface.IsLaser(stroke)).Select(stroke => stroke.Clone()))))
            .Where(page => page.Strokes.Count > 0)
            .ToList();
        if (inked.Count == 0)
        {
            MessageBox.Show(this, "Nothing to export yet, draw on the PDF first.", "Drawski", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveFileDialog dialog = new()
        {
            Title = "Export PDF with ink",
            InitialDirectory = Path.GetDirectoryName(_pdfPath),
            FileName = Path.GetFileNameWithoutExtension(_pdfPath) + " (annotated).pdf",
            Filter = "PDF (*.pdf)|*.pdf",
        };
        if (dialog.ShowDialog(this) != true) return;

        if (string.Equals(Path.GetFullPath(dialog.FileName), _pdfPath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Pick a different name. Drawski never overwrites the original PDF.", "Drawski", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _status.Text = "Exporting...";
        try
        {
            await Task.Run(() => new PdfInkExporter().Export(_pdfPath, dialog.FileName, inked));
        }
        catch (Exception exception)
        {
            UpdatePages();
            MessageBox.Show(this, $"Couldn't export:\n{exception.Message}", "Drawski", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        UpdatePages();
        MessageBoxResult result = MessageBox.Show(this, $"Exported to {Path.GetFileName(dialog.FileName)}.\n\nShow it in the folder?", "Drawski",
            MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes) Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
    }

    #endregion

    #region Ink file

    public bool SaveInk()
    {
        List<PageSlot> inked = _pages.Where(slot => slot.Strokes.Any(stroke => !InkSurface.IsLaser(stroke))).ToList();

        try
        {
            WriteInk(SidecarPath, inked);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FallbackPath));
                WriteInk(FallbackPath, inked);
            }
            catch (Exception fallbackException)
            {
                MessageBox.Show(this, $"Couldn't save:\n{fallbackException.Message}", "Drawski", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        History.Dirty = false;
        UpdateTitle();
        return true;
    }

    void WriteInk(string path, List<PageSlot> inked)
    {
        if (inked.Count == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        string tempPath = path + ".tmp";
        using (BinaryWriter writer = new(File.Create(tempPath)))
        {
            writer.Write(INK_HEADER);
            writer.Write(inked.Count);
            foreach (PageSlot slot in inked)
            {
                using MemoryStream buffer = new();
                new StrokeCollection(slot.Strokes.Where(stroke => !InkSurface.IsLaser(stroke))).Save(buffer);
                writer.Write(slot.Index);
                writer.Write((int)buffer.Length);
                writer.Write(buffer.ToArray());
            }
        }
        File.Move(tempPath, path, true);
    }

    Dictionary<int, StrokeCollection> LoadInk()
    {
        Dictionary<int, StrokeCollection> ink = new();
        string path = File.Exists(SidecarPath) ? SidecarPath : File.Exists(FallbackPath) ? FallbackPath : null;
        if (path == null) return ink;

        try
        {
            using BinaryReader reader = new(File.OpenRead(path));
            if (reader.ReadString() != INK_HEADER) return ink;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int index = reader.ReadInt32();
                byte[] bytes = reader.ReadBytes(reader.ReadInt32());
                ink[index] = new StrokeCollection(new MemoryStream(bytes));
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Found saved drawings but couldn't read them:\n{exception.Message}", "Drawski",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return ink;
    }

    #endregion

    class PageSlot
    {
        public readonly int Index;
        public readonly Size Size;
        public readonly StrokeCollection Strokes;
        public Border Host;
        public Image Image;
        public InkCanvas Canvas;
        public InkSurface Surface;
        public double RenderedScale;
        public bool Rendering;

        public PageSlot(int index, Size size, StrokeCollection strokes)
        {
            Index = index;
            Size = size;
            Strokes = strokes;
        }
    }
}
