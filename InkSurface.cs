using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Drawski;

// Tool behaviour for one InkCanvas (the overlay, or a single pdf page)
public class InkSurface
{
    static readonly Guid LASER_ID = new("7d0f3c2a-5b1e-4c8e-9a51-3f2b8e6d4c10");

    readonly OverlayWindow _overlay;
    readonly InkCanvas _canvas;
    readonly Polyline _preview = new()
    {
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
    };

    Point __shapeStart;
    bool __shapeDragging;

    Tool CurrentTool => _overlay.CurrentTool;
    bool IsShapeTool => CurrentTool is Tool.Line or Tool.Arrow or Tool.Rectangle or Tool.Ellipse;

    public InkSurface(OverlayWindow overlay, InkCanvas canvas)
    {
        _overlay = overlay;
        _canvas = canvas;
        _canvas.Children.Add(_preview);

        _canvas.StrokeCollected += OnStrokeCollected;
        _canvas.PreviewMouseLeftButtonDown += OnShapeDown;
        _canvas.PreviewMouseMove += OnShapeMove;
        _canvas.PreviewMouseLeftButtonUp += OnShapeUp;
        Apply();
    }

    public static bool IsLaser(Stroke stroke) => stroke.DrawingAttributes.ContainsPropertyData(LASER_ID);

    public void Apply()
    {
        _canvas.DefaultDrawingAttributes = BuildAttributes();
        _canvas.EditingMode = CurrentTool switch
        {
            Tool.Pen or Tool.Highlighter or Tool.Laser => InkCanvasEditingMode.Ink,
            Tool.Eraser => InkCanvasEditingMode.EraseByStroke,
            _ => InkCanvasEditingMode.None,
        };
        _canvas.UseCustomCursor = IsShapeTool || CurrentTool == Tool.Mouse;
        _canvas.Cursor = IsShapeTool ? Cursors.Cross : CurrentTool == Tool.Mouse ? Cursors.Arrow : null;
    }

    DrawingAttributes BuildAttributes()
    {
        double size = _overlay.CurrentSize;
        DrawingAttributes attributes = new()
        {
            Color = _overlay.CurrentColor,
            Width = size,
            Height = size,
            FitToCurve = !IsShapeTool,
            IgnorePressure = IsShapeTool,
        };

        if (CurrentTool == Tool.Highlighter)
        {
            attributes.IsHighlighter = true;
            attributes.IgnorePressure = true;
            attributes.StylusTip = StylusTip.Rectangle;
            attributes.Width = size * 2;
            attributes.Height = size * 5;
        }
        if (CurrentTool == Tool.Laser) attributes.AddPropertyData(LASER_ID, true);
        return attributes;
    }

    void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (IsLaser(e.Stroke)) _overlay.TrackLaser(e.Stroke, _canvas.Strokes);
    }

    void OnShapeDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsShapeTool) return;

        __shapeStart = e.GetPosition(_canvas);
        __shapeDragging = true;
        _canvas.CaptureMouse();
        _preview.Stroke = new SolidColorBrush(_overlay.CurrentColor);
        _preview.StrokeThickness = _overlay.CurrentSize;
        _preview.Points = new PointCollection();
        _preview.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    void OnShapeMove(object sender, MouseEventArgs e)
    {
        if (!__shapeDragging) return;

        _preview.Points = new PointCollection(ShapePoints(__shapeStart, e.GetPosition(_canvas)));
    }

    void OnShapeUp(object sender, MouseButtonEventArgs e)
    {
        if (!__shapeDragging) return;

        __shapeDragging = false;
        _canvas.ReleaseMouseCapture();
        _preview.Visibility = Visibility.Collapsed;

        Point end = e.GetPosition(_canvas);
        if ((end - __shapeStart).Length < 4) return;

        StylusPointCollection points = new();
        foreach (Point point in ShapePoints(__shapeStart, end))
            points.Add(new StylusPoint(point.X, point.Y));
        _canvas.Strokes.Add(new Stroke(points, BuildAttributes()));
    }

    List<Point> ShapePoints(Point start, Point end)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) end = Constrain(start, end);

        return CurrentTool switch
        {
            Tool.Line => new List<Point> { start, end },
            Tool.Arrow => ArrowPoints(start, end),
            Tool.Rectangle => new List<Point> { start, new(end.X, start.Y), end, new(start.X, end.Y), start },
            _ => EllipsePoints(start, end),
        };
    }

    // Shift = square/circle, or 45 degree snapping for lines
    Point Constrain(Point start, Point end)
    {
        Vector delta = end - start;
        if (CurrentTool is Tool.Rectangle or Tool.Ellipse)
        {
            double side = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
            return new Point(start.X + side * Math.Sign(delta.X), start.Y + side * Math.Sign(delta.Y));
        }

        double step = Math.PI / 4;
        double angle = Math.Round(Math.Atan2(delta.Y, delta.X) / step) * step;
        return start + new Vector(Math.Cos(angle), Math.Sin(angle)) * delta.Length;
    }

    List<Point> ArrowPoints(Point start, Point end)
    {
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double head = Math.Min(Math.Max(14, _overlay.CurrentSize * 4), (end - start).Length * 0.5);
        Point left = end - new Vector(Math.Cos(angle - 0.5), Math.Sin(angle - 0.5)) * head;
        Point right = end - new Vector(Math.Cos(angle + 0.5), Math.Sin(angle + 0.5)) * head;
        return new List<Point> { start, end, left, end, right };
    }

    List<Point> EllipsePoints(Point start, Point end)
    {
        Point center = new((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        double radiusX = Math.Abs(end.X - start.X) / 2, radiusY = Math.Abs(end.Y - start.Y) / 2;
        List<Point> points = new();
        for (int i = 0; i <= 72; i++)
        {
            double t = i * Math.PI * 2 / 72;
            points.Add(new Point(center.X + radiusX * Math.Cos(t), center.Y + radiusY * Math.Sin(t)));
        }
        return points;
    }
}
