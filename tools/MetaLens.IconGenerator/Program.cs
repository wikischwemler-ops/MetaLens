using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: MetaLens.IconGenerator <output.ico>");
    return 1;
}

var outputPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var visual = new DrawingVisual();
using (var drawing = visual.RenderOpen())
{
    var background = new LinearGradientBrush(
        Color.FromRgb(24, 33, 60),
        Color.FromRgb(48, 53, 111),
        new Point(0, 0),
        new Point(1, 1));
    drawing.DrawRoundedRectangle(background, null, new Rect(4, 4, 248, 248), 54, 54);

    var page = new StreamGeometry();
    using (var context = page.Open())
    {
        context.BeginFigure(new Point(68, 45), isFilled: true, isClosed: true);
        context.LineTo(new Point(143, 45), isStroked: true, isSmoothJoin: false);
        context.LineTo(new Point(184, 86), isStroked: true, isSmoothJoin: false);
        context.LineTo(new Point(184, 196), isStroked: true, isSmoothJoin: false);
        context.LineTo(new Point(68, 196), isStroked: true, isSmoothJoin: false);
    }

    drawing.DrawGeometry(new SolidColorBrush(Color.FromRgb(244, 247, 255)), null, page);

    var foldPen = new Pen(new SolidColorBrush(Color.FromRgb(190, 202, 228)), 5);
    drawing.DrawLine(foldPen, new Point(144, 47), new Point(144, 87));
    drawing.DrawLine(foldPen, new Point(144, 87), new Point(182, 87));

    var detailPen = CreateRoundPen(Color.FromRgb(191, 201, 224), 6);
    drawing.DrawLine(detailPen, new Point(86, 158), new Point(128, 158));
    drawing.DrawLine(detailPen, new Point(86, 178), new Point(117, 178));

    var handleShadow = CreateRoundPen(Color.FromRgb(20, 27, 49), 23);
    drawing.DrawLine(handleShadow, new Point(155, 154), new Point(199, 198));
    var handle = CreateRoundPen(Color.FromRgb(111, 231, 220), 15);
    drawing.DrawLine(handle, new Point(155, 154), new Point(199, 198));

    var lensPen = new Pen(new SolidColorBrush(Color.FromRgb(111, 231, 220)), 13);
    drawing.DrawEllipse(
        new SolidColorBrush(Color.FromRgb(38, 56, 91)),
        lensPen,
        new Point(124, 122),
        45,
        45);

    var shine = CreateRoundPen(Color.FromRgb(225, 255, 251), 6);
    drawing.DrawLine(shine, new Point(107, 106), new Point(117, 98));
}

var bitmap = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
bitmap.Render(visual);

var pngEncoder = new PngBitmapEncoder();
pngEncoder.Frames.Add(BitmapFrame.Create(bitmap));
using var pngStream = new MemoryStream();
pngEncoder.Save(pngStream);
var png = pngStream.ToArray();

using var iconStream = File.Create(outputPath);
using var writer = new BinaryWriter(iconStream);
writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)1);
writer.Write((byte)0);
writer.Write((byte)0);
writer.Write((byte)0);
writer.Write((byte)0);
writer.Write((ushort)1);
writer.Write((ushort)32);
writer.Write((uint)png.Length);
writer.Write((uint)22);
writer.Write(png);

Console.WriteLine($"Created {outputPath}");
return 0;

static Pen CreateRoundPen(Color color, double thickness) =>
    new(new SolidColorBrush(color), thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    };
