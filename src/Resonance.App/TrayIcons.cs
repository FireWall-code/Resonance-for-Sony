using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Resonance.App;

public enum TrayState { Disconnected, NoiseCancelling, Ambient, Off }

/// <summary>
/// Notification area icons, one per state, told apart by colour and shape: blue with filled ear cups for noise
/// cancelling, green with open ear cups for ambient sound, the white logo for off, and a crossed-out outline
/// when disconnected.
/// </summary>
internal static class TrayIcons
{
    private static readonly Dictionary<(TrayState, bool, int), Icon> Cache = [];

    public static Icon Get(TrayState state)
    {
        int size = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
        var key = (state, LightTaskbar(), size);
        if (!Cache.TryGetValue(key, out var icon))
        {
            using var bmp = Draw(state, key.Item2, size);
            // Cached for the life of the app, so the icon handle never needs releasing.
            icon = Icon.FromHandle(bmp.GetHicon());
            Cache[key] = icon;
        }
        return icon;
    }

    private static bool LightTaskbar() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

    /// <summary>The icon as a bitmap (documentation and checks).</summary>
    public static Bitmap Draw(TrayState state, bool lightTaskbar, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = size;
        var (tile, glyph) = state switch
        {
            TrayState.NoiseCancelling => (Color.FromArgb(37, 99, 235), Color.White),
            TrayState.Ambient => (Color.FromArgb(5, 150, 105), Color.White),
            TrayState.Off => (Color.FromArgb(245, 245, 245), Color.Black),
            _ => (Color.Transparent, lightTaskbar ? Color.FromArgb(70, 70, 70) : Color.FromArgb(190, 190, 190)),
        };
        if (state != TrayState.Disconnected)
        {
            using var path = Rounded(0, 0, s - 1, s - 1, s * 0.22f);
            using var fill = new SolidBrush(tile);
            g.FillPath(fill, path);
        }

        float stroke = Math.Max(1.4f, s * 0.085f);
        using var pen = new Pen(glyph, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        // Headband, then the two ear cups: filled when noise cancelling seals the ears.
        g.DrawArc(pen, s * 0.24f, s * 0.2f, s * 0.52f, s * 0.56f, 180, 180);
        var left = new RectangleF(s * 0.2f, s * 0.5f, s * 0.17f, s * 0.29f);
        var right = new RectangleF(s * 0.63f, s * 0.5f, s * 0.17f, s * 0.29f);
        foreach (var cup in new[] { left, right })
        {
            using var path = Rounded(cup.X, cup.Y, cup.Width, cup.Height, s * 0.05f);
            if (state == TrayState.NoiseCancelling)
            {
                using var fill = new SolidBrush(glyph);
                g.FillPath(fill, path);
            }
            g.DrawPath(pen, path);
        }
        if (state == TrayState.Disconnected)
        {
            // The slash, with a clear gap around it so it stays readable at 16 px.
            g.CompositingMode = CompositingMode.SourceCopy;
            using var gap = new Pen(Color.Transparent, stroke * 1.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(gap, s * 0.1f, s * 0.1f, s * 0.9f, s * 0.9f);
            g.CompositingMode = CompositingMode.SourceOver;
            g.DrawLine(pen, s * 0.12f, s * 0.12f, s * 0.88f, s * 0.88f);
        }
        return bmp;
    }

    private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = 2 * r;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
