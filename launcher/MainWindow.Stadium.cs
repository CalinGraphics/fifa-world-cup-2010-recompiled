using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Fifawc10Launcher;

// The backdrop: a stadium at night, drawn from shapes so nothing here is game or FIFA artwork.
// Roof rim with floodlights, a line of nations' bunting, the stands, two firework bursts
// and slow confetti.
public partial class MainWindow
{
    static readonly Random Rng = new(2010);

    // Roof rim: a quadratic curve across the window, highest in the middle.
    const double RimY = 318, RimRise = 70, RimDepth = 46;

    static Point Rim(double t, double w, double offset = 0)
    {
        // P0 (-60, RimY), P1 (w/2, RimY - 2*RimRise), P2 (w+60, RimY)
        double x0 = -60, x2 = w + 60, x1 = w / 2;
        double y0 = RimY + offset, y1 = RimY - 2 * RimRise + offset, y2 = RimY + offset;
        double u = 1 - t;
        return new Point(u * u * x0 + 2 * u * t * x1 + t * t * x2, u * u * y0 + 2 * u * t * y1 + t * t * y2);
    }

    void BuildStadium()
    {
        double w = Root.ActualWidth, h = Root.ActualHeight;
        Stadium.Children.Clear();

        // Floodlight haze over the pitch side
        Add(new Ellipse
        {
            Width = w * 1.3, Height = 420,
            Fill = new RadialGradientBrush(Color.FromArgb(0x2C, 0xD8, 0xE6, 0xF0), Colors.Transparent),
        }, -w * 0.15, RimY - 120);

        // Fireworks, faint, high in the sky
        Firework(w * 0.24, 120, 46);
        Firework(w * 0.50, 70, 34);

        // Stands below the rim: tiers of crowd, darker as they come forward
        var stands = new PathGeometry();
        var fig = new PathFigure { StartPoint = Rim(0, w, RimDepth) };
        fig.Segments.Add(new QuadraticBezierSegment(new Point(w / 2, RimY - 2 * RimRise + RimDepth), Rim(1, w, RimDepth), true));
        fig.Segments.Add(new LineSegment(new Point(w + 60, h), true));
        fig.Segments.Add(new LineSegment(new Point(-60, h), true));
        fig.IsClosed = true;
        stands.Figures.Add(fig);
        Stadium.Children.Add(new Path
        {
            Data = stands,
            Fill = new LinearGradientBrush(Color.FromRgb(0x1A, 0x1E, 0x22), Color.FromRgb(0x07, 0x08, 0x09), 90),
        });
        var crowd = new Path { Data = stands, Fill = CrowdBrush(), Opacity = 0.9 };
        Stadium.Children.Add(crowd);
        // tier edges
        for (int i = 1; i <= 4; i++)
        {
            double off = RimDepth + i * 62;
            var tier = new PathGeometry();
            var tf = new PathFigure { StartPoint = Rim(0, w, off) };
            tf.Segments.Add(new QuadraticBezierSegment(new Point(w / 2, RimY - 2 * RimRise + off), Rim(1, w, off), true));
            tier.Figures.Add(tf);
            Stadium.Children.Add(new Path
            {
                Data = tier, Stroke = new SolidColorBrush(Color.FromArgb((byte)(40 - i * 6), 255, 255, 255)), StrokeThickness = 1,
            });
        }

        // Roof underside
        var roof = new PathGeometry();
        var rf = new PathFigure { StartPoint = Rim(0, w) };
        rf.Segments.Add(new QuadraticBezierSegment(new Point(w / 2, RimY - 2 * RimRise), Rim(1, w), true));
        rf.Segments.Add(new LineSegment(Rim(1, w, RimDepth), true));
        rf.Segments.Add(new QuadraticBezierSegment(new Point(w / 2, RimY - 2 * RimRise + RimDepth), Rim(0, w, RimDepth), true));
        rf.IsClosed = true;
        roof.Figures.Add(rf);
        Stadium.Children.Add(new Path
        {
            Data = roof,
            Fill = new LinearGradientBrush(Color.FromRgb(0x4A, 0x50, 0x55), Color.FromRgb(0x1C, 0x20, 0x23), 90),
        });
        // roof struts
        for (double t = 0.02; t < 1; t += 0.035)
        {
            var a = Rim(t, w); var b = Rim(t, w, RimDepth);
            Stadium.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), StrokeThickness = 2 });
        }

        // Floodlights along the rim: a hot core and a soft bloom
        for (double t = 0.03; t < 1; t += 0.072)
        {
            var p = Rim(t, w, 4);
            Add(new Ellipse
            {
                Width = 120, Height = 46,
                Fill = new RadialGradientBrush(Color.FromArgb(0x70, 0xF4, 0xF8, 0xFF), Colors.Transparent),
            }, p.X - 60, p.Y - 20);
            Add(new Rectangle { Width = 34, Height = 7, Fill = Brushes.White, RadiusX = 2, RadiusY = 2 }, p.X - 17, p.Y - 3);
        }

        // Bunting: a string under the rim with nations' flags hanging from it
        var str = new PathGeometry();
        var sf = new PathFigure { StartPoint = Rim(0, w, RimDepth + 8) };
        sf.Segments.Add(new QuadraticBezierSegment(new Point(w / 2, RimY - 2 * RimRise + RimDepth + 8), Rim(1, w, RimDepth + 8), true));
        str.Figures.Add(sf);
        Stadium.Children.Add(new Path { Data = str, Stroke = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), StrokeThickness = 1 });
        int k = 0;
        for (double t = 0.05; t < 0.98; t += 0.058, k++)
        {
            var p = Rim(t, w, RimDepth + 8);
            Stadium.Children.Add(Flag(Flags[k % Flags.Length], p.X - 13, p.Y + 2, (Rng.NextDouble() - 0.5) * 8));
        }

        // Gold light behind the title
        Add(new Ellipse
        {
            Width = 760, Height = 420,
            Fill = new RadialGradientBrush(Color.FromArgb(0x30, 0xF2, 0xB6, 0x32), Colors.Transparent),
        }, -180, h - 360);

        StartConfetti(w, h);
    }

    void Add(UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        Stadium.Children.Add(e);
    }

    void Firework(double cx, double cy, double r)
    {
        var g = new Canvas { Opacity = 0.55 };
        for (int i = 0; i < 18; i++)
        {
            double a = i * Math.PI * 2 / 18 + Rng.NextDouble() * 0.2;
            double len = r * (0.6 + Rng.NextDouble() * 0.4);
            var c = i % 3 == 0 ? Color.FromRgb(0xFF, 0xD2, 0x7A) : Color.FromRgb(0xFF, 0x86, 0x86);
            g.Children.Add(new Line
            {
                X1 = cx + Math.Cos(a) * len * 0.35, Y1 = cy + Math.Sin(a) * len * 0.35,
                X2 = cx + Math.Cos(a) * len, Y2 = cy + Math.Sin(a) * len,
                Stroke = new LinearGradientBrush(Color.FromArgb(0, c.R, c.G, c.B), c,
                    new Point(0.5 - Math.Cos(a) / 2, 0.5 - Math.Sin(a) / 2), new Point(0.5 + Math.Cos(a) / 2, 0.5 + Math.Sin(a) / 2)),
                StrokeThickness = 1.4,
            });
        }
        g.Effect = new BlurEffect { Radius = 0.8 };
        Stadium.Children.Add(g);
        if (!Motion) return;
        g.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 0.6, TimeSpan.FromSeconds(2.4 + Rng.NextDouble()))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
        });
    }

    // Simplified three-band flags (and a few others), enough to read as a tournament's bunting.
    static readonly Color[][] Flags =
    {
        new[] { C("#009739"), C("#FEDD00"), C("#009739") },          // green-yellow
        new[] { C("#AA151B"), C("#F1BF00"), C("#AA151B") },          // red-yellow-red
        new[] { C("#000000"), C("#DD0000"), C("#FFCE00") },          // black-red-gold
        new[] { C("#74ACDF"), C("#FFFFFF"), C("#74ACDF") },          // sky-white-sky
        new[] { C("#AE1C28"), C("#FFFFFF"), C("#21468B") },          // red-white-blue
        new[] { C("#CE1126"), C("#FCD116"), C("#006B3F") },          // red-gold-green
        new[] { C("#007A4D"), C("#FFB612"), C("#DE3831") },          // green-gold-red
        new[] { C("#FFFFFF"), C("#CE1124"), C("#FFFFFF") },          // white-red-white
        new[] { C("#0055A4"), C("#FFFFFF"), C("#EF4135") },          // blue-white-red (vertical)
        new[] { C("#006847"), C("#FFFFFF"), C("#CE1126") },          // green-white-red (vertical)
    };

    static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    static UIElement Flag(Color[] bands, double x, double y, double angle)
    {
        bool vertical = bands[0] == C("#0055A4") || bands[0] == C("#006847");
        var g = new Grid { Width = 26, Height = 17, RenderTransform = new RotateTransform(angle, 13, 0) };
        for (int i = 0; i < 3; i++)
        {
            var r = new Rectangle { Fill = new SolidColorBrush(bands[i]) };
            if (vertical) { g.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetColumn(r, i); }
            else { g.RowDefinitions.Add(new RowDefinition()); Grid.SetRow(r, i); }
            g.Children.Add(r);
        }
        g.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(110, 0, 0, 0), 90) });
        Canvas.SetLeft(g, x);
        Canvas.SetTop(g, y);
        return g;
    }

    // A tile of tiny specks in shirt colours, tiled across the stands.
    static Brush CrowdBrush()
    {
        var group = new DrawingGroup();
        var rnd = new Random(7);
        Color[] shirts = { C("#E9E4D8"), C("#F2C230"), C("#2F8F4E"), C("#C8352B"), C("#3B5BA8"), C("#F08A24") };
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 96, 64))));
        for (int i = 0; i < 120; i++)
        {
            var c = shirts[rnd.Next(shirts.Length)];
            byte a = (byte)rnd.Next(18, 70);
            double x = rnd.NextDouble() * 96, y = rnd.NextDouble() * 64;
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)), null,
                new RectangleGeometry(new Rect(x, y, 1.8, 2.4))));
        }
        return new DrawingBrush(group)
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 96, 64), ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
    }

    static bool Motion => SystemParameters.ClientAreaAnimation;

    void StartConfetti(double w, double h)
    {
        Confetti.Children.Clear();
        Color[] colors = { C("#F2B632"), C("#F2B632"), C("#E2382B"), C("#F3EFE6"), C("#36A852") };
        for (int i = 0; i < 70; i++)
        {
            var c = colors[Rng.Next(colors.Length)];
            var piece = new Rectangle
            {
                Width = 3 + Rng.NextDouble() * 4, Height = 5 + Rng.NextDouble() * 6,
                Fill = new SolidColorBrush(c), Opacity = 0.35 + Rng.NextDouble() * 0.5,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var spin = new RotateTransform(Rng.Next(360));
            piece.RenderTransform = spin;
            double x = Rng.NextDouble() * w, y = Rng.NextDouble() * h;
            Canvas.SetLeft(piece, x);
            Canvas.SetTop(piece, y);
            Confetti.Children.Add(piece);
            if (!Motion) continue;
            double secs = 14 + Rng.NextDouble() * 16;
            var fall = new DoubleAnimation(-20, h + 20, TimeSpan.FromSeconds(secs))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(-secs * (y / h)),
            };
            piece.BeginAnimation(Canvas.TopProperty, fall);
            piece.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(x - 14, x + 14, TimeSpan.FromSeconds(2 + Rng.NextDouble() * 3))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
            spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, Rng.Next(2) == 0 ? 360 : -360, TimeSpan.FromSeconds(3 + Rng.NextDouble() * 4))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            });
        }
    }
}
