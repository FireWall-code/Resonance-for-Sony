using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Resonance.Core.Device;
using Resonance.Core.Protocol;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace Resonance.App;

/// <summary>The connected headphones: a drawing in their body colour, or the user's own picture.</summary>
public partial class DeviceArt : UserControl
{
    public static readonly DependencyProperty FormProperty = DependencyProperty.Register(
        nameof(Form), typeof(HeadphoneForm), typeof(DeviceArt), new(HeadphoneForm.Earbuds, (d, _) => ((DeviceArt)d).Update()));
    public static readonly DependencyProperty BodyColorProperty = DependencyProperty.Register(
        nameof(BodyColor), typeof(ModelColor), typeof(DeviceArt), new(ModelColor.Default, (d, _) => ((DeviceArt)d).Update()));
    public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(
        nameof(ImagePath), typeof(string), typeof(DeviceArt), new(null, (d, _) => ((DeviceArt)d).Update()));

    public static readonly DependencyProperty BodyBrushProperty = DependencyProperty.Register(nameof(BodyBrush), typeof(Brush), typeof(DeviceArt));
    public static readonly DependencyProperty SheenBrushProperty = DependencyProperty.Register(nameof(SheenBrush), typeof(Brush), typeof(DeviceArt));
    public static readonly DependencyProperty TipBrushProperty = DependencyProperty.Register(nameof(TipBrush), typeof(Brush), typeof(DeviceArt));
    public static readonly DependencyProperty EdgeBrushProperty = DependencyProperty.Register(nameof(EdgeBrush), typeof(Brush), typeof(DeviceArt));
    public static readonly DependencyProperty RimBrushProperty = DependencyProperty.Register(nameof(RimBrush), typeof(Brush), typeof(DeviceArt));
    public static readonly DependencyProperty GlowBrushProperty = DependencyProperty.Register(nameof(GlowBrush), typeof(Brush), typeof(DeviceArt));

    public DeviceArt()
    {
        InitializeComponent();
        Update();
    }

    public HeadphoneForm Form { get => (HeadphoneForm)GetValue(FormProperty); set => SetValue(FormProperty, value); }
    public ModelColor BodyColor { get => (ModelColor)GetValue(BodyColorProperty); set => SetValue(BodyColorProperty, value); }
    /// <summary>A picture file that replaces the drawing (null or missing = drawing).</summary>
    public string? ImagePath { get => (string?)GetValue(ImagePathProperty); set => SetValue(ImagePathProperty, value); }

    public Brush BodyBrush { get => (Brush)GetValue(BodyBrushProperty); private set => SetValue(BodyBrushProperty, value); }
    public Brush SheenBrush { get => (Brush)GetValue(SheenBrushProperty); private set => SetValue(SheenBrushProperty, value); }
    public Brush TipBrush { get => (Brush)GetValue(TipBrushProperty); private set => SetValue(TipBrushProperty, value); }
    public Brush EdgeBrush { get => (Brush)GetValue(EdgeBrushProperty); private set => SetValue(EdgeBrushProperty, value); }
    /// <summary>Outline: light on dark finishes, dark on light ones.</summary>
    public Brush RimBrush { get => (Brush)GetValue(RimBrushProperty); private set => SetValue(RimBrushProperty, value); }
    public Brush GlowBrush { get => (Brush)GetValue(GlowBrushProperty); private set => SetValue(GlowBrushProperty, value); }

    /// <summary>Approximate finishes of Sony's colours.</summary>
    internal static Color Tint(ModelColor c) => c switch
    {
        ModelColor.Black => Color.FromRgb(0x2A, 0x2A, 0x2E),
        ModelColor.White => Color.FromRgb(0xEE, 0xEB, 0xE6),
        ModelColor.Silver => Color.FromRgb(0xCB, 0xCD, 0xD2),
        ModelColor.Red => Color.FromRgb(0xB0, 0x3A, 0x3A),
        ModelColor.Blue => Color.FromRgb(0x3E, 0x58, 0x80),
        ModelColor.Pink => Color.FromRgb(0xE6, 0xB6, 0xBA),
        ModelColor.Yellow => Color.FromRgb(0xE4, 0xC6, 0x55),
        ModelColor.Green => Color.FromRgb(0x6F, 0x8E, 0x74),
        ModelColor.Gray => Color.FromRgb(0x8C, 0x8F, 0x95),
        ModelColor.Gold => Color.FromRgb(0xC8, 0xA8, 0x70),
        ModelColor.Cream => Color.FromRgb(0xE8, 0xDF, 0xCC),
        ModelColor.Orange => Color.FromRgb(0xDD, 0x7B, 0x42),
        ModelColor.Brown => Color.FromRgb(0x7A, 0x5B, 0x46),
        ModelColor.Violet => Color.FromRgb(0x70, 0x5C, 0x8C),
        _ => Color.FromRgb(0x3A, 0x3A, 0x3F),
    };

    private void Update()
    {
        if (Photo is null) return; // before InitializeComponent
        var photo = LoadPhoto(ImagePath);
        Photo.Source = photo;
        Photo.Visibility = photo is null ? Visibility.Collapsed : Visibility.Visible;
        Drawing.Visibility = photo is null ? Visibility.Visible : Visibility.Collapsed;
        Earbuds.Visibility = Form == HeadphoneForm.Earbuds ? Visibility.Visible : Visibility.Collapsed;
        OverEar.Visibility = Form == HeadphoneForm.OverEar ? Visibility.Visible : Visibility.Collapsed;

        var c = Tint(BodyColor);
        bool light = c.R * 0.3 + c.G * 0.59 + c.B * 0.11 > 150;
        BodyBrush = Frozen(new LinearGradientBrush(Shade(c, 1.18), Shade(c, 0.78), new Point(0.2, 0), new Point(0.8, 1)));
        SheenBrush = Frozen(new LinearGradientBrush(Color.FromArgb(light ? (byte)0x70 : (byte)0x38, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(1, 1)));
        TipBrush = Frozen(new SolidColorBrush(light ? Shade(c, 0.72) : Shade(c, 0.55)));
        EdgeBrush = Frozen(new SolidColorBrush(Color.FromArgb(light ? (byte)0x40 : (byte)0x50, 0, 0, 0)));
        RimBrush = Frozen(new SolidColorBrush(light ? Color.FromArgb(0x30, 0, 0, 0) : Color.FromArgb(0x50, 255, 255, 255)));
        var glow = new RadialGradientBrush(Color.FromArgb(light ? (byte)0x00 : (byte)0x2A, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));
        GlowBrush = Frozen(glow);
    }

    private static BitmapImage? LoadPhoto(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // do not keep the file locked
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception) { return null; }
    }

    private static Color Shade(Color c, double f) =>
        Color.FromRgb((byte)Math.Clamp(c.R * f, 0, 255), (byte)Math.Clamp(c.G * f, 0, 255), (byte)Math.Clamp(c.B * f, 0, 255));

    private static T Frozen<T>(T brush) where T : Freezable { brush.Freeze(); return brush; }
}
