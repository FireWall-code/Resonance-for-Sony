using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;

namespace Resonance.App;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageSound is null) return; // fired during InitializeComponent
        var pages = new FrameworkElement[] { PageSound, PageEq, PageQuality, PageTouch, PageSettings, PageDevices, PageHotkeys };
        for (int i = 0; i < pages.Length; i++)
            pages[i].Visibility = i == Nav.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- hotkey recorder

    private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HotkeyRow row) row.IsRecording = true;
    }

    private void HotkeyBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HotkeyRow row) row.IsRecording = false;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not HotkeyRow row) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        switch (key)
        {
            case Key.Escape:
                Keyboard.ClearFocus();
                return;
            case Key.Delete or Key.Back when Keyboard.Modifiers == ModifierKeys.None:
                row.Record(null);
                Keyboard.ClearFocus();
                return;
            case Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin or Key.ImeProcessed or Key.DeadCharProcessed:
                return; // wait for the actual key
        }
        var mods = Keyboard.Modifiers;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= ModifierKeys.Windows;
        var hotkey = new Hotkey(mods, key);
        if (!hotkey.IsValid) return; // a plain letter alone would hijack normal typing
        row.Record(hotkey);
        Keyboard.ClearFocus(); // leaving the box re-registers the hotkeys
    }

    // ---------------------------------------------------------------- documentation screenshots

    private static readonly string[] PageNames = ["sound", "equalizer", "quality", "touch", "settings", "devices", "shortcuts"];

    /// <summary>
    /// Renders every page to PNG files in <paramref name="folder"/> without showing the window
    /// (no focus change, so it can run while a game or another app is in front).
    /// </summary>
    public string RenderPages(string folder)
    {
        Directory.CreateDirectory(folder);
        var root = (FrameworkElement)Content;
        var size = new Size(Width, Height - 32);
        const double scale = 1.5;
        var background = TryFindResource("SolidBackgroundFillColorBaseBrush") as Brush ?? Brushes.White;
        int previous = Nav.SelectedIndex;
        // Neutral sample data instead of the user's own media title and device names.
        using var _ = ((MainViewModel)DataContext).DocumentationPreview();
        for (int i = 0; i < PageNames.Length; i++)
        {
            Nav.SelectedIndex = i;
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            var backdrop = new DrawingVisual();
            using (var dc = backdrop.RenderOpen()) dc.DrawRectangle(background, null, new Rect(size));
            var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(backdrop);
            bitmap.Render(root);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(folder, PageNames[i] + ".png"));
            png.Save(file);
        }
        Nav.SelectedIndex = previous;
        return $"{PageNames.Length} pages rendered to {folder}";
    }

    // Closing the window keeps Resonance running in the notification area.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

/// <summary>Binds a RadioButton to an int property: checked when value == parameter.</summary>
public sealed class IndexConverter : IValueConverter
{
    public static readonly IndexConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int i && i.ToString(CultureInfo.InvariantCulture) == (string)parameter;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? int.Parse((string)parameter, CultureInfo.InvariantCulture) : Binding.DoNothing;
}
