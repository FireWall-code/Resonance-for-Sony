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
    private Forms.ToolStripMenuItem? _ncItem, _ambientItem, _offItem, _shareItem, _reconnectItem;

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
        _vm.Connected += () => { if (!_vm.TakeQuietConnect() && _vm.ShowConnectionCard) ShowConnectionCard(force: false); };
        _vm.CardRequested += () => ShowConnectionCard(force: true);
        // In the tray, give back the memory the window and the card used.
        _window.IsVisibleChanged += (_, _) => { if (!_window.IsVisible) ScheduleTrim(); };
        _vm.Connected += ScheduleTrim;
        _vm.PropertyChanged += (_, a) =>
        {
            if (a.PropertyName is nameof(MainViewModel.Status) or nameof(MainViewModel.IsConnected) or nameof(MainViewModel.IsPaused)
                or nameof(MainViewModel.IsIdle) or nameof(MainViewModel.NcMode) or nameof(MainViewModel.IsConnecting))
                UpdateTray();
        };
        _window.Activated += (_, _) => _vm.WakeIfIdle();

        // "--background" starts hidden in the tray (for the Windows startup entry).
        if (!e.Args.Contains("--background")) _window.Show();
        // "--replay <folder>" shows a recorded session instead of connecting (other models, bug reports).
        int replay = Array.IndexOf(e.Args, "--replay");
        if (replay >= 0 && replay + 1 < e.Args.Length) { _vm.ShowRecording(e.Args[replay + 1]); return; }
        await _vm.ConnectAsync();
    }

    /// <summary>Runs a command received from resonance-cli on the app's connection.</summary>
    private async Task<CommandResult> HandleCommandAsync(string[] args)
    {
        if (args.FirstOrDefault() == "__show") { await Dispatcher.InvokeAsync(ShowWindow); return CommandResult.Done("ok"); }
        if (args.FirstOrDefault() == "__ping") return CommandResult.Done("pong");
        // Writes the notification area icons as images: resonance-cli __icons <folder>
        if (args.FirstOrDefault() == "__icons")
        {
            var folder = args.ElementAtOrDefault(1) ?? ".";
            foreach (var state in Enum.GetValues<TrayState>())
                foreach (var dark in new[] { false, true })
                    foreach (var size in new[] { 16, 20, 24, 32 })
                    {
                        using var bmp = TrayIcons.Draw(state, !dark, size);
                        bmp.Save(System.IO.Path.Combine(folder, $"{state}-{(dark ? "dark" : "light")}-{size}.png"));
                    }
            return CommandResult.Done(folder);
        }
        // Toggles the quick panel, as a click on the icon does.
        if (args.FirstOrDefault() == "__panel") { await Dispatcher.InvokeAsync(ToggleQuickPanel); return CommandResult.Done("ok"); }
        // Shows the connection card now (resonance-cli __card), as the Preview button does.
        if (args.FirstOrDefault() == "__card") { await Dispatcher.InvokeAsync(() => ShowConnectionCard(force: true)); return CommandResult.Done("ok"); }
        // Documentation screenshots, rendered off-screen: resonance-cli __screenshot <folder> [light|dark]
        if (args.FirstOrDefault() == "__screenshot")
            return CommandResult.Done(await Dispatcher.InvokeAsync(() => _window!.RenderPages(args.ElementAtOrDefault(1) ?? ".", args.ElementAtOrDefault(2))));
        if (_vm is null) return CommandResult.Waiting(T("cli.notconnected", ""));
        // resonance-cli release / resume: hand the headphones to the phone app and take them back.
        switch (args.FirstOrDefault()?.ToLowerInvariant())
        {
            case "release" or "liberer" or "libérer":
                await Dispatcher.InvokeAsync(_vm.PauseAsync).Task.Unwrap();
                return CommandResult.Done(T("status.paused"));
            case "resume" or "reprendre":
                await Dispatcher.InvokeAsync(() => { _ = _vm.ResumeAsync(); });
                return await _vm.EnsureConnectedAsync()
                    ? CommandResult.Done(T("status.connected")) : CommandResult.Fail(T("cli.notconnected", _vm.Status));
        }
        if (_vm.IsPaused) return CommandResult.Fail(T("cli.paused"));
        // Released while idle: take the headphones back for this command.
        if (_vm.Device is null && !await _vm.EnsureConnectedAsync())
            return CommandResult.Fail(T("cli.notconnected", _vm.Status));
        _vm.Touch();
        return await CommandRunner.RunAsync(_vm.Device!, args);
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(T("tray.open"), null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        _ncItem = new(T("nc.nc"), null, async (_, _) => await SetModeAsync(0));
        _ambientItem = new(T("nc.ambient"), null, async (_, _) => await SetModeAsync(1));
        _offItem = new(T("nc.off"), null, async (_, _) => await SetModeAsync(2));
        menu.Items.AddRange([_ncItem, _ambientItem, _offItem]);
        menu.Items.Add(new Forms.ToolStripSeparator());
        _shareItem = new(T("share.pause"), null, async (_, _) =>
        {
            if (_vm!.IsPaused) await _vm.ResumeAsync(); else await _vm.PauseAsync();
        });
        menu.Items.Add(_shareItem);
        _reconnectItem = new(T("action.reconnect"), null, async (_, _) => await _vm!.ConnectAsync(discover: true));
        menu.Items.Add(_reconnectItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("tray.quit"), null, async (_, _) => await QuitAsync());

        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIcons.Get(TrayState.Disconnected),
            Text = "Resonance",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += (_, a) => { if (a.Button == Forms.MouseButtons.Left) ToggleQuickPanel(); };
    }

    private void UpdateTray()
    {
        if (_tray is null || _vm is null) return;
        var text = _vm.IsConnected ? $"{_vm.DeviceName}\n{_vm.ModeLabel}\n{_vm.BatterySummary}" : $"Resonance\n{_vm.Status}";
        _tray.Text = text.Length > 127 ? text[..127] : text;
        _ncItem!.Checked = _vm.NcMode == 0;
        _ambientItem!.Checked = _vm.NcMode == 1;
        _offItem!.Checked = _vm.NcMode == 2;
        // Released while idle, the noise items still work: they take the headphones back.
        foreach (var item in new[] { _ncItem, _ambientItem, _offItem }) item.Enabled = _vm.IsConnected || _vm.IsIdle;
        _shareItem!.Text = _vm.IsPaused ? T("share.resume") : T("share.pause");
        _reconnectItem!.Visible = _vm.CanReconnect;
        _tray.Icon = TrayIcons.Get(_vm.TrayState);
    }

    private DateTime _lastCard = DateTime.MinValue;
    private ConnectionCard? _card;

    /// <summary>
    /// The animated card above the notification area. Reconnections within two minutes (a connection
    /// priority change, a short drop) do not show it again.
    /// </summary>
    private void ShowConnectionCard(bool force)
    {
        if (_vm is null || (!force && (_panel is not null || DateTime.UtcNow - _lastCard < TimeSpan.FromMinutes(2)))) return;
        _lastCard = DateTime.UtcNow;
        _card?.Close();
        _card = _vm.CreateCard(ShowWindow);
        _card.Closed += (_, _) => { _card = null; ScheduleTrim(); };
        _card.Show();
    }

    private async Task SetModeAsync(int mode)
    {
        if (_vm is null || (!_vm.IsConnected && !await _vm.EnsureConnectedAsync())) return;
        _vm.NcMode = mode;
    }

    [System.Runtime.InteropServices.DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr process);

    private System.Windows.Threading.DispatcherTimer? _trim;

    /// <summary>
    /// A few seconds after the window or the card is gone, collects unused objects and hands the unused
    /// pages back to Windows, so Resonance stays small while it waits in the notification area.
    /// </summary>
    private void ScheduleTrim()
    {
        _trim ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _trim.Stop();
        _trim.Tick -= OnTrim;
        _trim.Tick += OnTrim;
        _trim.Start();
    }

    private void OnTrim(object? sender, EventArgs e)
    {
        _trim!.Stop();
        if (_window is { IsVisible: true } || _card is not null || _panel is not null) return;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        EmptyWorkingSet(self.Handle);
    }

    private QuickPanel? _panel;
    private DateTime _panelClosed;

    /// <summary>Opens the quick panel, or closes it: clicking the icon first takes the focus away and closes it.</summary>
    private void ToggleQuickPanel()
    {
        if (_vm is null) return;
        if (_panel is not null) { _panel.Close(); return; }
        if (DateTime.UtcNow - _panelClosed < TimeSpan.FromMilliseconds(300)) return;
        _vm.WakeIfIdle();
        _panel = new QuickPanel(_vm, ShowWindow);
        _panel.Closed += (_, _) => { _panel = null; _panelClosed = DateTime.UtcNow; ScheduleTrim(); };
        _panel.Show();
        _panel.Activate();
    }

    public void ShowWindow()
    {
        // Opening the window means Resonance is in use: take the headphones back if it released them.
        _vm?.WakeIfIdle();
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

}
