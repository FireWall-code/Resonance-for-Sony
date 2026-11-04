using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Resonance.App;

public enum TrayState { Disconnected, NoiseCancelling, Ambient, Off }

/// <summary>
/// Notification area icons: the app logo itself, of which only the background colour changes with the state.
/// White (the logo as it is) for off, blue for noise cancelling, green for ambient sound, grey when disconnected.
/// </summary>
internal static class TrayIcons
{
    private static readonly Dictionary<(TrayState, int), Icon> Cache = [];

    public static Icon Get(TrayState state)
    {
        var key = (state, IconSize());
        if (!Cache.TryGetValue(key, out var icon))
        {
            using var bmp = Draw(state, key.Item2);
            icon = ToIcon(bmp);
            Cache[key] = icon;
        }
        return icon;
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    private const int SmallIconWidth = 49; // SM_CXSMICON

    private static int IconSize() => Math.Max(16, GetSystemMetricsForDpi(SmallIconWidth, GetDpiForSystem()));

    /// <summary>The icon as a bitmap (documentation and checks).</summary>
    public static Bitmap Draw(TrayState state, int size)
    {
        var bmp = LoadLogo(size);
        var background = state switch
        {
            TrayState.NoiseCancelling => Color.FromArgb(79, 155, 255),
            TrayState.Ambient => Color.FromArgb(47, 208, 127),
            TrayState.Disconnected => Color.FromArgb(140, 140, 140),
            _ => Color.White,
        };
        Tint(bmp, background);
        return bmp;
    }

    /// <summary>The logo, scaled to this size.</summary>
    private static Bitmap LoadLogo(int size)
    {
        using var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/resonance.ico")).Stream;
        using var file = new MemoryStream();
        resource.CopyTo(file);
        var ico = file.ToArray();

        var entries = Enumerable.Range(0, BitConverter.ToUInt16(ico, 4)).Select(i => 6 + 16 * i).Select(e => (
            Size: ico[e] == 0 ? 256 : (int)ico[e],
            Length: BitConverter.ToInt32(ico, e + 8),
            Offset: BitConverter.ToInt32(ico, e + 12))).ToList();
        // A large entry reduced stays regular, where the small ones have uneven lines.
        var entry = entries.Where(e => e.Size >= 4 * size).OrderBy(e => e.Size).FirstOrDefault();
        if (entry.Size == 0) entry = entries.MaxBy(e => e.Size);

        using var image = new MemoryStream(ico, entry.Offset, entry.Length);
        using var logo = new Bitmap(image);
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(logo, 0, 0, size, size);
        return bmp;
    }

    /// <summary>
    /// Multiplies by the colour: the white background takes it, the black headphones stay black. The levels are
    /// stretched first, because at 16 pixels the thin lines of the logo otherwise come out light grey.
    /// </summary>
    private static void Tint(Bitmap bmp, Color color)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var pixels = new byte[data.Stride * bmp.Height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // Logo pixels are shades of grey between its white background and its black headphones.
            float level = Math.Clamp((pixels[i + 1] / 255f - 0.35f) / 0.55f, 0, 1);
            pixels[i] = (byte)(color.B * level);
            pixels[i + 1] = (byte)(color.G * level);
            pixels[i + 2] = (byte)(color.R * level);
        }
        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bmp.UnlockBits(data);
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
