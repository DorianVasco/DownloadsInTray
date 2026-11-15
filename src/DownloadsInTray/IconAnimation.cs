using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace DownloadsInTray;

/// <summary>
/// Tray icon frames drawn from Assets\icon.svg: the arrow slides down into the tray bar and comes back
/// in from the top, while the bar stays put. Rendered once, so animating only swaps icons.
/// </summary>
internal static class IconAnimation
{
    private const string SvgResource = "icon.svg";
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // Paths starting below this line (in the SVG's 1080-unit space) form the tray bar; the rest is the arrow.
    private const double BarTop = 800;

    public static System.Drawing.Icon[] CreateFrames(int pixels, int count)
    {
        XDocument doc;
        using (Stream stream = typeof(IconAnimation).Assembly.GetManifestResourceStream(SvgResource)!)
        {
            doc = XDocument.Load(stream);
        }

        double[] viewBox = doc.Root!.Attribute("viewBox")!.Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(double.Parse).ToArray();
        Brush brush = GradientBrush(doc);

        var bar = new GeometryGroup();
        var arrow = new GeometryGroup();
        foreach (XElement path in doc.Descendants(Svg + "path"))
        {
            Geometry geometry = Geometry.Parse(path.Attribute("d")!.Value);
            (geometry.Bounds.Top >= BarTop ? bar : arrow).Children.Add(geometry);
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

    private static Brush GradientBrush(XDocument doc)
    {
        XElement gradient = doc.Descendants(Svg + "linearGradient").First();
        double Coordinate(string name) => double.Parse(gradient.Attribute(name)!.Value, System.Globalization.CultureInfo.InvariantCulture);
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(Coordinate("x1"), Coordinate("y1")),
            EndPoint = new Point(Coordinate("x2"), Coordinate("y2")),
        };
        foreach (XElement stop in gradient.Elements(Svg + "stop"))
        {
            var color = (Color)ColorConverter.ConvertFromString(stop.Attribute("stop-color")!.Value);
            brush.GradientStops.Add(new GradientStop(color, double.Parse(stop.Attribute("offset")!.Value, System.Globalization.CultureInfo.InvariantCulture)));
        }
        brush.Freeze();
        return brush;
    }

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
