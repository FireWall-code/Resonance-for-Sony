using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Resonance.App;

public enum TrayState { Disconnected, NoiseCancelling, Ambient, Off }

/// <summary>
/// Notification area icons, one per state, told apart by colour and shape: blue with filled ear cups for noise
/// cancelling, green with open ear cups for ambient sound, the white logo for off, and a crossed-out outline
/// when disconnected. At 100 % scaling these are only 16 pixels wide, so every edge is snapped to the pixel grid.
/// </summary>
internal static class TrayIcons
{
    private static readonly Dictionary<(TrayState, bool, int), Icon> Cache = [];

    public static Icon Get(TrayState state)
    {
        var key = (state, LightTaskbar(), IconSize());
        if (!Cache.TryGetValue(key, out var icon))
        {
            using var bmp = Draw(state, key.Item2, key.Item3);
            icon = ToIcon(bmp);
            Cache[key] = icon;
        }
        return icon;
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    private const int SmallIconWidth = 49; // SM_CXSMICON

    private static int IconSize() => Math.Max(16, GetSystemMetricsForDpi(SmallIconWidth, GetDpiForSystem()));

    private static bool LightTaskbar() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

    /// <summary>The icon as a bitmap (documentation and checks).</summary>
    public static Bitmap Draw(TrayState state, bool lightTaskbar, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half; // whole coordinates fall exactly on pixel edges

        var (tile, glyph) = state switch
        {
            TrayState.NoiseCancelling => (Color.FromArgb(37, 99, 235), Color.White),
            TrayState.Ambient => (Color.FromArgb(4, 130, 92), Color.White),
            TrayState.Off => (Color.FromArgb(248, 248, 248), Color.FromArgb(20, 20, 20)),
            _ => (Color.Transparent, lightTaskbar ? Color.FromArgb(60, 60, 60) : Color.FromArgb(205, 205, 205)),
        };
        // Sizes come from a 16-pixel design, rounded to whole pixels; the right side mirrors the left.
        int Px(float v) => (int)Math.Round(v * size / 16f, MidpointRounding.AwayFromZero);

        if (state != TrayState.Disconnected)
        {
            using var path = Rounded(0, 0, size, size, Px(3));
            using var fill = new SolidBrush(tile);
            g.FillPath(fill, path);
        }

        int stroke = Math.Max(1, Px(1.25f));
        int side = Px(2), cupWidth = Px(4), cupTop = Px(8), bottom = size - Px(2);
        using var brush = new SolidBrush(glyph);
        // A curve loses strength when smoothed: a slightly wider pen keeps it as solid as the straight edges.
        using var pen = new Pen(glyph, stroke + 0.4f);

        // Headband: half a circle from one ear cup to the other.
        float band = side + stroke / 2f, diameter = size - 2 * band;
        g.DrawArc(pen, band, Px(2) + stroke / 2f, diameter, diameter, 180, 180);
        float bandEnd = Px(2) + stroke / 2f + diameter / 2;
        if (bandEnd < cupTop)
        {
            g.FillRectangle(brush, side, bandEnd, stroke, cupTop - bandEnd);
            g.FillRectangle(brush, size - side - stroke, bandEnd, stroke, cupTop - bandEnd);
        }

        // Ear cups: filled, except ambient sound, which opens them.
        foreach (int x in new[] { side, size - side - cupWidth })
        {
            using var cup = Rounded(x, cupTop, cupWidth, bottom - cupTop, Math.Max(1, Px(1)) * 0.7f);
            g.FillPath(brush, cup);
            if (state == TrayState.Ambient)
            {
                using var open = new SolidBrush(tile);
                g.FillRectangle(open, x + stroke, cupTop + stroke, cupWidth - 2 * stroke, bottom - cupTop - 2 * stroke);
            }
        }

        if (state == TrayState.Disconnected) Slash(g, brush, size, stroke, Px(1));
        return bmp;
    }

    /// <summary>A pixel-exact diagonal, with a clear line beside it so it stands out from the headphones.</summary>
    private static void Slash(Graphics g, Brush brush, int size, int stroke, int margin)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.CompositingMode = CompositingMode.SourceCopy;
        using (var clear = new SolidBrush(Color.Transparent))
            for (int i = margin; i < size - margin; i++)
                g.FillRectangle(clear, i, i + stroke, stroke, stroke);
        g.CompositingMode = CompositingMode.SourceOver;
        for (int i = margin; i < size - margin - stroke + 1; i++)
            g.FillRectangle(brush, i, i, stroke, stroke);
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

    /// <summary>
    /// Wraps the pixels in an .ico image. Bitmap.GetHicon would reduce the transparency to a hard mask and
    /// leave jagged corners. Icons are cached for the life of the app.
    /// </summary>
    private static Icon ToIcon(Bitmap bmp)
    {
        int size = bmp.Width, stride = size * 4, mask = (size + 31) / 32 * 4 * size;
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // ICONDIR, ICONDIRENTRY
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)1);
            w.Write((byte)size); w.Write((byte)size); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32); w.Write((uint)(40 + stride * size + mask)); w.Write((uint)22);
            // BITMAPINFOHEADER: the height counts the colours and the mask.
            w.Write((uint)40); w.Write(size); w.Write(size * 2); w.Write((ushort)1); w.Write((ushort)32);
            w.Write((uint)0); w.Write((uint)(stride * size + mask)); w.Write(0); w.Write(0); w.Write((uint)0); w.Write((uint)0);
            // BGRA rows, bottom-up, then an empty mask: the alpha channel does the work.
            var data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var row = new byte[stride];
            for (int y = size - 1; y >= 0; y--)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, stride);
                w.Write(row);
            }
            bmp.UnlockBits(data);
            w.Write(new byte[mask]);
        }
        stream.Position = 0;
        return new Icon(stream);
    }
}
