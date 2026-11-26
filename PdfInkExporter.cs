using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Drawski;

// Writes strokes into a copy of the pdf as real /Ink annotations, so they show (and print) in any pdf viewer
public class PdfInkExporter
{
    const double HIGHLIGHTER_OPACITY = 0.5;

    public void Export(string sourcePath, string targetPath, List<(int Index, Size Size, StrokeCollection Strokes)> pages)
    {
        using PdfDocument document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);

        foreach ((int index, Size size, StrokeCollection strokes) in pages)
        {
            if (index >= document.PageCount) continue;

            PdfPage page = document.Pages[index];
            PageMapping mapping = new(page, size);
            foreach (Stroke stroke in strokes)
                AddAnnotation(document, page, mapping, stroke);
        }

        document.Save(targetPath);
    }

    void AddAnnotation(PdfDocument document, PdfPage page, PageMapping mapping, Stroke stroke)
    {
        DrawingAttributes attributes = stroke.DrawingAttributes;
        double opacity = attributes.IsHighlighter ? HIGHLIGHTER_OPACITY : attributes.Color.A / 255.0;
        double width = Math.Max(attributes.Width, attributes.Height) * mapping.Scale;

        // Outline of the stroke as WPF draws it (pressure, smoothing, highlighter tip), used for the appearance
        PathGeometry outline = stroke.GetGeometry().GetFlattenedPathGeometry(0.1, ToleranceType.Absolute);
        List<List<Point>> figures = outline.Figures.Select(figure => FigurePoints(figure).Select(mapping.ToPdf).ToList()).Where(points => points.Count > 1).ToList();
        if (figures.Count == 0) return;

        List<Point> centerLine = stroke.StylusPoints.Select(point => mapping.ToPdf(new Point(point.X, point.Y))).ToList();
        IEnumerable<Point> allPoints = figures.SelectMany(points => points).Concat(centerLine);
        PdfRectangle rect = new(
            new PdfSharp.Drawing.XPoint(allPoints.Min(p => p.X) - 1, allPoints.Min(p => p.Y) - 1),
            new PdfSharp.Drawing.XPoint(allPoints.Max(p => p.X) + 1, allPoints.Max(p => p.Y) + 1));

        PdfArray inkLine = new(document);
        foreach (Point point in centerLine)
        {
            inkLine.Elements.Add(new PdfReal(Round(point.X)));
            inkLine.Elements.Add(new PdfReal(Round(point.Y)));
        }

        Color color = attributes.Color;
        PdfDictionary appearance = CreateAppearance(document, rect, figures, outline.FillRule, color, opacity, attributes.IsHighlighter);

        PdfDictionary border = new(document);
        border.Elements["/W"] = new PdfReal(Round(width));

        PdfDictionary appearances = new(document);
        appearances.Elements["/N"] = appearance.Reference;

        PdfDictionary annotation = new(document);
        annotation.Elements["/Type"] = new PdfName("/Annot");
        annotation.Elements["/Subtype"] = new PdfName("/Ink");
        annotation.Elements["/Rect"] = rect;
        annotation.Elements["/InkList"] = new PdfArray(document, inkLine);
        annotation.Elements["/C"] = new PdfArray(document, new PdfReal(color.R / 255.0), new PdfReal(color.G / 255.0), new PdfReal(color.B / 255.0));
        annotation.Elements["/CA"] = new PdfReal(opacity);
        annotation.Elements["/BS"] = border;
        annotation.Elements["/F"] = new PdfInteger(4);
        annotation.Elements["/T"] = new PdfString("Drawski");
        annotation.Elements["/NM"] = new PdfString(Guid.NewGuid().ToString());
        annotation.Elements["/M"] = new PdfDate(DateTime.Now);
        annotation.Elements["/P"] = page.Reference;
        annotation.Elements["/AP"] = appearances;
        document.Internals.AddObject(annotation);

        PdfArray annotations = page.Elements.GetArray("/Annots");
        if (annotations == null)
        {
            annotations = new PdfArray(document);
            page.Elements["/Annots"] = annotations;
        }
        annotations.Elements.Add(annotation.Reference);
    }

    PdfDictionary CreateAppearance(PdfDocument document, PdfRectangle rect, List<List<Point>> figures, FillRule fillRule, Color color, double opacity, bool isHighlighter)
    {
        StringBuilder content = new();
        content.Append("/GS0 gs ");
        content.Append($"{Number(color.R / 255.0)} {Number(color.G / 255.0)} {Number(color.B / 255.0)} rg\n");
        foreach (List<Point> points in figures)
        {
            content.Append($"{Number(points[0].X)} {Number(points[0].Y)} m\n");
            for (int i = 1; i < points.Count; i++)
                content.Append($"{Number(points[i].X)} {Number(points[i].Y)} l\n");
            content.Append("h\n");
        }
        content.Append(fillRule == FillRule.EvenOdd ? "f*" : "f");

        PdfDictionary state = new(document);
        state.Elements["/Type"] = new PdfName("/ExtGState");
        state.Elements["/CA"] = new PdfReal(opacity);
        state.Elements["/ca"] = new PdfReal(opacity);
        if (isHighlighter) state.Elements["/BM"] = new PdfName("/Multiply");

        PdfDictionary states = new(document);
        states.Elements["/GS0"] = state;
        PdfDictionary resources = new(document);
        resources.Elements["/ExtGState"] = states;

        PdfDictionary form = new(document);
        form.Elements["/Type"] = new PdfName("/XObject");
        form.Elements["/Subtype"] = new PdfName("/Form");
        form.Elements["/BBox"] = rect;
        form.Elements["/Resources"] = resources;
        form.CreateStream(Encoding.ASCII.GetBytes(content.ToString()));
        document.Internals.AddObject(form);
        return form;
    }

    IEnumerable<Point> FigurePoints(PathFigure figure)
    {
        yield return figure.StartPoint;
        foreach (PathSegment segment in figure.Segments)
        {
            if (segment is PolyLineSegment polyLine)
                foreach (Point point in polyLine.Points) yield return point;
            else if (segment is LineSegment line)
                yield return line.Point;
        }
    }

    double Round(double value) => Math.Round(value, 2);

    string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    // Drawski ink is in the viewer's page space (DIPs, top-left origin, already rotated), pdf is in points from the bottom-left of the unrotated crop box
    class PageMapping
    {
        public readonly double Scale;

        readonly double _left, _bottom, _width, _height, _scaleX, _scaleY;
        readonly int _rotation;

        public PageMapping(PdfPage page, Size viewSize)
        {
            PdfRectangle box = page.Elements.ContainsKey("/CropBox") ? page.CropBox : page.MediaBox;
            _left = Math.Min(box.X1, box.X2);
            _bottom = Math.Min(box.Y1, box.Y2);
            _width = Math.Abs(box.X2 - box.X1);
            _height = Math.Abs(box.Y2 - box.Y1);
            _rotation = ((page.Rotate % 360) + 360) % 360;

            bool sideways = _rotation is 90 or 270;
            _scaleX = (sideways ? _height : _width) / viewSize.Width;
            _scaleY = (sideways ? _width : _height) / viewSize.Height;
            Scale = (_scaleX + _scaleY) / 2;
        }

        public Point ToPdf(Point point)
        {
            double x = point.X * _scaleX, y = point.Y * _scaleY;
            return _rotation switch
            {
                90 => new Point(_left + y, _bottom + x),
                180 => new Point(_left + _width - x, _bottom + y),
                270 => new Point(_left + _width - y, _bottom + _height - x),
                _ => new Point(_left + x, _bottom + _height - y),
            };
        }
    }
}
