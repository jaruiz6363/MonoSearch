using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MonoSearch.App.Views;

/// <summary>Draws a <see cref="LensLayout"/>: shells, ball, stop, image surface and rays coloured by field.</summary>
public sealed class LensView : Control
{
    public static readonly StyledProperty<LensLayout?> LayoutProperty =
        AvaloniaProperty.Register<LensView, LensLayout?>(nameof(Layout));

    public LensLayout? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    static LensView() => AffectsRender<LensView>(LayoutProperty);

    private static readonly IBrush Background = Brushes.White;
    private static readonly IBrush ShellFill = new SolidColorBrush(Color.Parse("#D6E8F7"));
    private static readonly IBrush BallFill = new SolidColorBrush(Color.Parse("#A8CBEA"));
    private static readonly Pen GlassPen = new(new SolidColorBrush(Color.Parse("#2F4F7A")), 1.2);
    private static readonly Pen StopPen = new(Brushes.Black, 3);
    private static readonly Pen ImagePen = new(new SolidColorBrush(Color.Parse("#333333")), 2.5);
    private static readonly Pen AxisPen = new(new SolidColorBrush(Color.Parse("#999999")), 0.8, new DashStyle(new double[] { 6, 4 }, 0));
    private static readonly Pen ScalePen = new(Brushes.Black, 1.5);
    // Field colours as OpticStudio's layout: blue, green, red.
    private static readonly Pen[] RayPens =
    {
        new(new SolidColorBrush(Color.Parse("#1F4FD8")), 0.9),
        new(new SolidColorBrush(Color.Parse("#1A9A3A")), 0.9),
        new(new SolidColorBrush(Color.Parse("#D62728")), 0.9),
    };

    public override void Render(DrawingContext ctx)
    {
        var b = Bounds;
        ctx.FillRectangle(Background, new Rect(b.Size));
        var L = Layout;
        if (L is null)
        {
            DrawText(ctx, "Select a design in the table to draw it.", new Point(12, 12), 13, Brushes.Gray);
            return;
        }

        // Fit everything drawn into the control, same scale on both axes.
        var all = L.Rays.SelectMany(r => r.Points).ToList();
        all.Add(new Pt(-L.R1, L.R1)); all.Add(new Pt(-L.R1, -L.R1)); all.Add(new Pt(L.R1, L.R1)); all.Add(new Pt(L.R1, -L.R1));
        all.AddRange(Arc(L.ImageRadius, 0, L.ImageAngle, 8));
        double zMin = all.Min(p => p.Z), zMax = all.Max(p => p.Z), yMin = all.Min(p => p.Y), yMax = all.Max(p => p.Y);
        const double pad = 18;
        double scale = Math.Min((b.Width - 2 * pad) / (zMax - zMin), (b.Height - 2 * pad - 20) / (yMax - yMin));
        if (!(scale > 0)) return;
        double ox = pad + ((b.Width - 2 * pad) - scale * (zMax - zMin)) / 2 - scale * zMin;
        double oy = pad + ((b.Height - 2 * pad - 20) - scale * (yMax - yMin)) / 2 + scale * yMax;
        Point P(Pt p) => new(ox + scale * p.Z, oy - scale * p.Y);

        ctx.DrawLine(AxisPen, P(new Pt(zMin, 0)), P(new Pt(zMax, 0)));

        // Shells: menisci about the front and rear vertices, each surface as wide as its rays need and the two
        // joined by a straight edge; or one complete spherical shell.
        if (L.FullShell)
        {
            ctx.DrawGeometry(ShellFill, GlassPen, Poly(Arc(L.R1, 0, Math.PI, 180).Select(P), true));
            ctx.DrawGeometry(Background, null, Poly(Arc(L.R2, 0, Math.PI, 180).Select(P), true));
        }
        else
        {
            foreach (double axis in new[] { Math.PI, 0.0 })
            {
                var outline = Arc(L.R1, axis, L.ShellAngle, 90).Concat(Arc(L.R2, axis, L.BallAngle, 90).Reverse());
                ctx.DrawGeometry(ShellFill, GlassPen, Poly(outline.Select(P), true));
            }
        }

        // Ball: a complete sphere, or front and rear caps joined by a cylindrical edge.
        var ball = L.FullBall
            ? Arc(L.R2, 0, Math.PI, 180)
            : Arc(L.R2, Math.PI, L.BallAngle, 90).Concat(Arc(L.R2, 0, L.BallAngle, 90));
        ctx.DrawGeometry(BallFill, GlassPen, Poly(ball.Select(P), true));

        // Stop: an opaque disc in the cemented plane at the centre, open within the stop semi-diameter.
        double rim = L.FullBall ? L.R2 : L.R2 * Math.Sin(L.BallAngle);
        if (rim > L.StopSemi)
        {
            ctx.DrawLine(StopPen, P(new Pt(0, L.StopSemi)), P(new Pt(0, rim)));
            ctx.DrawLine(StopPen, P(new Pt(0, -L.StopSemi)), P(new Pt(0, -rim)));
        }

        foreach (var ray in L.Rays)
            ctx.DrawGeometry(null, RayPens[Math.Min(ray.Field, RayPens.Length - 1)], Poly(ray.Points.Select(P), false));

        ctx.DrawGeometry(null, ImagePen, Poly(Arc(L.ImageRadius, 0, L.ImageAngle, 90).Select(P), false));

        // Scale bar and legend.
        double bar = NiceLength(0.2 * b.Width / scale);
        var s0 = new Point(pad, b.Height - pad);
        ctx.DrawLine(ScalePen, s0, new Point(pad + bar * scale, s0.Y));
        ctx.DrawLine(ScalePen, s0 - new Point(0, 4), s0 + new Point(0, 4));
        ctx.DrawLine(ScalePen, new Point(pad + bar * scale, s0.Y - 4), new Point(pad + bar * scale, s0.Y + 4));
        DrawText(ctx, $"{bar.ToString("G", CultureInfo.InvariantCulture)} mm", new Point(pad + bar * scale + 6, s0.Y - 9), 12, Brushes.Black);

        double x = b.Width - pad;
        for (int f = L.FieldAngles.Length - 1; f >= 0; f--)
        {
            var ft = Text($"{L.FieldAngles[f].ToString("0.##", CultureInfo.InvariantCulture)}°", 12, RayPens[Math.Min(f, RayPens.Length - 1)].Brush!);
            x -= ft.Width;
            ctx.DrawText(ft, new Point(x, b.Height - pad - 9));
            x -= 12;
        }
    }

    /// <summary>Points of a circle of radius r centred on the origin, within half-angle a of the direction
    /// <paramref name="axis"/> (0 = +z, pi = -z), from one side to the other.</summary>
    private static IEnumerable<Pt> Arc(double r, double axis, double a, int n)
    {
        for (int i = 0; i <= n; i++)
        {
            double t = axis - a + 2 * a * i / n;
            yield return new Pt(r * Math.Cos(t), r * Math.Sin(t));
        }
    }

    private static StreamGeometry Poly(IEnumerable<Point> pts, bool closed)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        bool first = true;
        foreach (var p in pts)
        {
            if (first) { c.BeginFigure(p, closed); first = false; }
            else c.LineTo(p);
        }
        if (!first) c.EndFigure(closed);
        return g;
    }

    private static double NiceLength(double v)
    {
        double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (double m in new[] { 5.0, 2.0, 1.0 }) if (m * p <= v) return m * p;
        return p;
    }

    private static FormattedText Text(string s, double size, IBrush brush) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);

    private static void DrawText(DrawingContext ctx, string s, Point at, double size, IBrush brush) =>
        ctx.DrawText(Text(s, size, brush), at);
}
