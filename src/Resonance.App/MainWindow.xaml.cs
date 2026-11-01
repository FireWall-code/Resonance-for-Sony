using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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
