using System.Windows;

namespace Resonance.App;

/// <summary>Quick settings above the notification area; closes as soon as it loses focus.</summary>
public partial class QuickPanel : Window
{
    private readonly Action _open;
    private bool _closing;

    /// <param name="open">Opens the main window.</param>
    public QuickPanel(MainViewModel vm, Action open)
    {
        InitializeComponent();
        DataContext = vm;
        _open = open;
        Loaded += (_, _) => Place();
        SizeChanged += (_, _) => Place();
        Deactivated += (_, _) => CloseOnce();
    }

    private void Place()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth;
        Top = area.Bottom - ActualHeight;
    }

    // Closing takes the focus away, which raises Deactivated: Close() must not run a second time.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    private void CloseOnce()
    {
        if (!_closing) Close();
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        CloseOnce();
        _open();
    }
}
