using AutoNexusMobile.Services;

namespace AutoNexusMobile.Controls;

/// <summary>Part d'un graphique : libellé, valeur et couleur (celles de Dashboard.tsx).</summary>
public sealed record ChartSlice(string Label, int Value, Color Color);

/// <summary>Base des graphiques : redessine quand les données ou le thème changent.</summary>
public abstract class ChartView : GraphicsView, IDrawable
{
    public static readonly BindableProperty SlicesProperty = BindableProperty.Create(
        nameof(Slices), typeof(IReadOnlyList<ChartSlice>), typeof(ChartView), propertyChanged: (b, _, _) => ((ChartView)b).Invalidate());

    protected ChartView()
    {
        Drawable = this;
        Loaded += (_, _) =>
        {
            if (Microsoft.Maui.Controls.Application.Current is { } app)
                app.RequestedThemeChanged += OnThemeChanged;
        };
        Unloaded += (_, _) =>
        {
            if (Microsoft.Maui.Controls.Application.Current is { } app)
                app.RequestedThemeChanged -= OnThemeChanged;
        };
    }

    public IReadOnlyList<ChartSlice>? Slices
    {
        get => (IReadOnlyList<ChartSlice>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Invalidate();

    public abstract void Draw(ICanvas canvas, RectF dirtyRect);
}

/// <summary>Anneau de la répartition du parc (PieChart à rayon intérieur du web), total au centre.</summary>
public sealed class DonutChart : ChartView
{
    public void SetCenter(string value, string caption)
    {
        CenterValue = value;
        CenterCaption = caption;
        Invalidate();
    }

    public string CenterValue { get; private set; } = "";

    public string CenterCaption { get; private set; } = "";

    public override void Draw(ICanvas canvas, RectF rect)
    {
        var slices = Slices?.Where(s => s.Value > 0).ToList() ?? [];
        var total = slices.Sum(s => s.Value);
        var size = Math.Min(rect.Width, rect.Height);
        var thickness = size * 0.16f;
        var box = new RectF(rect.Center.X - size / 2 + thickness / 2, rect.Center.Y - size / 2 + thickness / 2, size - thickness, size - thickness);

        canvas.StrokeLineCap = LineCap.Butt;
        canvas.StrokeSize = thickness;

        if (total == 0)
        {
            canvas.StrokeColor = ThemeColors.Get("Track");
            canvas.DrawEllipse(box);
        }
        else
        {
            // Écart de 3° entre les parts (paddingAngle du web), sauf s'il n'y en a qu'une.
            var gap = slices.Count > 1 ? 3f : 0f;
            var start = 90f;
            foreach (var slice in slices)
            {
                var sweep = 360f * slice.Value / total;
                canvas.StrokeColor = slice.Color;
                canvas.DrawArc(box, start - gap / 2, start - sweep + gap / 2, true, false);
                start -= sweep;
            }
        }

        canvas.FontColor = ThemeColors.Get("Ink");
        canvas.Font = new Microsoft.Maui.Graphics.Font("InterBold");
        canvas.FontSize = size * 0.17f;
        canvas.DrawString(CenterValue, rect.X, rect.Center.Y - size * 0.15f, rect.Width, size * 0.2f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = ThemeColors.Get("Muted");
        canvas.Font = new Microsoft.Maui.Graphics.Font("Inter");
        canvas.FontSize = size * 0.075f;
        canvas.DrawString(CenterCaption, rect.X, rect.Center.Y + size * 0.05f, rect.Width, size * 0.1f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}

/// <summary>Barres des interventions par type (BarChart du web : coins arrondis, grille pointillée).</summary>
public sealed class BarChart : ChartView
{
    public override void Draw(ICanvas canvas, RectF rect)
    {
        var slices = Slices ?? [];
        if (slices.Count == 0)
            return;

        const float labelHeight = 22f;
        const float axisWidth = 26f;
        var plot = new RectF(rect.X + axisWidth, rect.Y + 8, rect.Width - axisWidth, rect.Height - labelHeight - 8);
        var max = Math.Max(1, slices.Max(s => s.Value));
        var step = NiceStep(max);
        var top = (int)Math.Ceiling(max / (double)step) * step;

        canvas.Font = new Microsoft.Maui.Graphics.Font("Inter");
        canvas.FontSize = 11;

        // Grille horizontale pointillée et graduations.
        canvas.StrokeSize = 1;
        canvas.StrokeDashPattern = [3, 3];
        for (var value = 0; value <= top; value += step)
        {
            var y = plot.Bottom - plot.Height * value / top;
            canvas.StrokeColor = ThemeColors.Get("LineSoft");
            canvas.DrawLine(plot.Left, y, plot.Right, y);
            canvas.FontColor = ThemeColors.Get("Muted");
            canvas.DrawString(value.ToString(), rect.X, y - 7, axisWidth - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        }
        canvas.StrokeDashPattern = null;

        var slot = plot.Width / slices.Count;
        var barWidth = Math.Min(28f, slot * 0.45f);
        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var height = plot.Height * slice.Value / top;
            var x = plot.Left + slot * i + (slot - barWidth) / 2;
            canvas.FillColor = slice.Color;
            if (height > 0)
                canvas.FillRoundedRectangle(x, plot.Bottom - height, barWidth, height, Math.Min(6, height / 2), Math.Min(6, height / 2), 0, 0);

            canvas.FontColor = ThemeColors.Get("Muted");
            canvas.DrawString(slice.Label, plot.Left + slot * i, plot.Bottom + 4, slot, labelHeight - 4, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    private static int NiceStep(int max) => max switch
    {
        <= 5 => 1,
        <= 12 => 3,
        <= 24 => 6,
        <= 50 => 10,
        _ => (int)Math.Ceiling(max / 4.0 / 10) * 10,
    };
}
