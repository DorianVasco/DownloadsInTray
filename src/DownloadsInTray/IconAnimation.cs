using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DownloadsInTray;

/// <summary>
/// Tray icon frames drawn from Assets\icon.svg: the arrow slides down into the tray bar and comes back
/// in from the top, while the bar stays put. Rendered once, so animating only swaps icons.
/// </summary>
internal static class IconAnimation
{

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // Paths starting below this share of the icon's height form the tray bar; the rest is the arrow.
    private const double BarTopShare = 0.7;

    /// <summary>Draws <paramref name="count"/> frames from an embedded SVG; the first shows the icon at rest.</summary>
    public static System.Drawing.Icon[] CreateFrames(string svgResource, int pixels, int count)
    {
        XDocument doc;
        using (Stream stream = typeof(IconAnimation).Assembly.GetManifestResourceStream(svgResource)!)
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
        {
            doc = XDocument.Load(reader);
        }

        double[] viewBox = doc.Root!.Attribute("viewBox")!.Value
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToArray();
        Brush brush = Fill(doc);

        var bar = new GeometryGroup();
        var arrow = new GeometryGroup();
        foreach (XElement path in doc.Descendants(Svg + "path"))
        {
            Geometry geometry = Geometry.Parse(path.Attribute("d")!.Value);
            (geometry.Bounds.Top >= viewBox[1] + viewBox[3] * BarTopShare ? bar : arrow).Children.Add(geometry);
        }

        // The arrow loops through the space above the bar, entering from the top as it leaves at the bottom.
        Rect arrowBounds = arrow.Bounds;
        double gap = bar.Bounds.Top - arrowBounds.Bottom;
        double period = arrowBounds.Height + gap;
        var clip = new RectangleGeometry(new Rect(viewBox[0], viewBox[1], viewBox[2], bar.Bounds.Top - gap / 2 - viewBox[1]));

        var frames = new System.Drawing.Icon[count];
        for (int i = 0; i < count; i++)
        {
            double offset = period * i / count;
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                double scale = pixels / Math.Max(viewBox[2], viewBox[3]);
                context.PushTransform(new ScaleTransform(scale, scale));
                context.PushTransform(new TranslateTransform(-viewBox[0], -viewBox[1]));
                context.DrawGeometry(brush, null, bar);
                context.PushClip(clip);
                foreach (double shift in new[] { offset, offset - period })
                {
                    context.PushTransform(new TranslateTransform(0, shift));
                    context.DrawGeometry(brush, null, arrow);
                    context.Pop();
                }
                context.Pop();
            }
            frames[i] = ToIcon(visual, pixels);
        }
        return frames;
    }

    // The icon's shapes share one gradient, so the first one is used for all of them. Handles the forms
    // design tools export: plain x1/y1/x2/y2, or unit coordinates moved by a gradientTransform matrix,
    // and stop colors as attributes or inside a style.
    private static Brush Fill(XDocument doc)
    {
        // Single-color drawings, such as the monochrome icons, fill their paths with a plain color.
        XElement? gradient = doc.Descendants(Svg + "linearGradient").FirstOrDefault();
        if (gradient == null)
        {
            var solid = new SolidColorBrush((Color)ColorConverter.ConvertFromString(doc.Descendants(Svg + "path").First().Attribute("fill")!.Value));
            solid.Freeze();
            return solid;
        }

        Point start = new(Number(gradient, "x1"), Number(gradient, "y1"));
        Point end = new(Number(gradient, "x2"), Number(gradient, "y2"));
        if (gradient.Attribute("gradientTransform")?.Value is string transform)
        {
            double[] m = Regex.Matches(transform, @"-?[\d.]+(?:e-?\d+)?").Select(match => Parse(match.Value)).ToArray();
            var matrix = new Matrix(m[0], m[1], m[2], m[3], m[4], m[5]);
            start = matrix.Transform(start);
            end = matrix.Transform(end);
        }

        var brush = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = start, EndPoint = end };
        foreach (XElement stop in gradient.Elements(Svg + "stop"))
        {
            brush.GradientStops.Add(new GradientStop(StopColor(stop), Number(stop, "offset")));
        }
        brush.Freeze();
        return brush;
    }

    private static Color StopColor(XElement stop)
    {
        string value = stop.Attribute("stop-color")?.Value
            ?? Regex.Match(stop.Attribute("style")?.Value ?? string.Empty, @"stop-color:\s*([^;]+)").Groups[1].Value;
        Match rgb = Regex.Match(value, @"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
        return rgb.Success
            ? Color.FromRgb(byte.Parse(rgb.Groups[1].Value), byte.Parse(rgb.Groups[2].Value), byte.Parse(rgb.Groups[3].Value))
            : (Color)ColorConverter.ConvertFromString(value.Trim());
    }

    private static double Number(XElement element, string attribute) => Parse(element.Attribute(attribute)?.Value ?? "0");

    private static double Parse(string value) => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static System.Drawing.Icon ToIcon(Visual visual, int pixels)
    {
        var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        png.Position = 0;

        using var drawing = new System.Drawing.Bitmap(png);
        IntPtr handle = drawing.GetHicon();
        try
        {
            using var borrowed = System.Drawing.Icon.FromHandle(handle);
            return (System.Drawing.Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
