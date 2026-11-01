using System.Drawing;
using System.Windows;
using Resonance.Core.Device;
using Resonance.Core.Transport;
using Forms = System.Windows.Forms;

using static Resonance.Core.Localization.Loc;

namespace Resonance.App;

public partial class App : Application
{
    private Forms.NotifyIcon? _tray;
    private MainWindow? _window;
    private MainViewModel? _vm;
    private Forms.ToolStripMenuItem? _ncItem, _ambientItem, _offItem;

    private readonly CancellationTokenSource _pipeCts = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Single instance: the headphones accept one control connection, so a second launch
        // only asks the running instance to show its window.
        if (await ControlPipe.TrySendAsync(["__show"], TimeSpan.FromSeconds(2)) != null) { Shutdown(); return; }

        _vm = new MainViewModel();
        _ = ControlPipe.ServeAsync(HandleCommandAsync, _pipeCts.Token);
        _window = new MainWindow { DataContext = _vm };
        CreateTray();
        _vm.DeviceStateChanged += UpdateTray;
        _vm.PropertyChanged += (_, a) => { if (a.PropertyName is nameof(MainViewModel.Status) or nameof(MainViewModel.IsConnected)) UpdateTray(); };

        // "--background" starts hidden in the tray (for the Windows startup entry).
        if (!e.Args.Contains("--background")) _window.Show();
        await _vm.ConnectLoopAsync();
    }

    /// <summary>Runs a command received from resonance-cli on the app's connection.</summary>
    private async Task<CommandResult> HandleCommandAsync(string[] args)
    {
        if (args.FirstOrDefault() == "__show") { await Dispatcher.InvokeAsync(ShowWindow); return CommandResult.Done("ok"); }
        if (args.FirstOrDefault() == "__ping") return CommandResult.Done("pong");
        if (_vm?.Device is not { } hp) return CommandResult.Waiting(T("cli.notconnected", _vm?.Status));
        return await CommandRunner.RunAsync(hp, args);
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(T("tray.open"), null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        _ncItem = new(T("nc.nc"), null, (_, _) => _vm!.NcMode = 0);
        _ambientItem = new(T("nc.ambient"), null, (_, _) => _vm!.NcMode = 1);
        _offItem = new(T("nc.off"), null, (_, _) => _vm!.NcMode = 2);
        menu.Items.AddRange([_ncItem, _ambientItem, _offItem]);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("tray.quit"), null, async (_, _) => await QuitAsync());

        _tray = new Forms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "Resonance",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += (_, a) =>
        {
            // Left click toggles noise mode, like the touch sensor; double click opens the window.
            if (a.Button == Forms.MouseButtons.Left && _vm!.IsConnected) _vm.CycleNoiseMode();
        };
        _tray.MouseDoubleClick += (_, _) => ShowWindow();
    }

    private void UpdateTray()
    {
        if (_tray is null || _vm is null) return;
        var text = _vm.IsConnected ? $"{_vm.DeviceName}\n{_vm.BatterySummary}" : $"Resonance\n{_vm.Status}";
        _tray.Text = text.Length > 127 ? text[..127] : text;
        _ncItem!.Checked = _vm.NcMode == 0;
        _ambientItem!.Checked = _vm.NcMode == 1;
        _offItem!.Checked = _vm.NcMode == 2;
        foreach (var item in new[] { _ncItem, _ambientItem, _offItem }) item.Enabled = _vm.IsConnected;
    }

    public void ShowWindow()
    {
        _window!.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public async Task QuitAsync()
    {
        _pipeCts.Cancel();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        if (_vm != null) await _vm.ShutdownAsync();
        Shutdown();
    }

    /// <summary>Uses the executable's icon; falls back to a drawn glyph.</summary>
    private static Icon CreateIcon()
    {
        if (Environment.ProcessPath is { } exe && Icon.ExtractAssociatedIcon(exe) is { } own) return own;
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Segoe Fluent Icons", 20, GraphicsUnit.Pixel);
            using var fallback = new Font("Segoe MDL2 Assets", 20, GraphicsUnit.Pixel);
            var f = font.Name == "Segoe Fluent Icons" ? font : fallback;
            // U+E7F6 = "Headphone" in Segoe Fluent Icons / MDL2 Assets.
            var color = Forms.SystemInformation.HighContrast ? Color.Black : Color.White;
            using var brush = new SolidBrush(color);
            g.DrawString("", f, brush, new RectangleF(0, 0, 32, 32),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
