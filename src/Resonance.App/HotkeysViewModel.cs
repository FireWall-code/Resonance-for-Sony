using System.Windows.Input;

using static Resonance.Core.Localization.Loc;

namespace Resonance.App;

/// <summary>One action of the "Keyboard shortcuts" page and its key combination.</summary>
public sealed class HotkeyRow(HotkeysViewModel owner, string action, string label, string description) : ObservableObject
{
    private Hotkey? _hotkey;
    private string _status = "";
    private bool _hasError, _isRecording;

    public string Action { get; } = action;
    public string Label { get; } = label;
    public string Description { get; } = description;

    public Hotkey? Hotkey { get => _hotkey; private set { if (SetField(ref _hotkey, value)) Raise(nameof(Display)); } }
    public string Display => IsRecording ? T("hk.recording") : Hotkey?.ToString() ?? T("hk.none");
    public string Status { get => _status; internal set => SetField(ref _status, value); }
    public bool HasError { get => _hasError; internal set => SetField(ref _hasError, value); }
    public bool IsRecording
    {
        get => _isRecording;
        set { if (SetField(ref _isRecording, value)) { Raise(nameof(Display)); owner.OnRecordingChanged(value); } }
    }

    public ICommand ClearCommand => new RelayCommand(() => { owner.Assign(this, null); return Task.CompletedTask; });

    internal void Load(Hotkey? hotkey) => Hotkey = hotkey;

    /// <summary>Called by the recorder box with the pressed combination (null clears).</summary>
    public void Record(Hotkey? hotkey) => owner.Assign(this, hotkey);
}

/// <summary>Global hotkeys for the noise control modes, saved in settings.json.</summary>
public sealed class HotkeysViewModel : ObservableObject, IDisposable
{
    private readonly HotkeyService _service = new();
    private readonly AppSettings _settings;

    public IReadOnlyList<HotkeyRow> Rows { get; }

    /// <summary>Raised with the action name ("nc", "ambient", "off", "toggle").</summary>
    public event Action<string>? Triggered;

    public HotkeysViewModel(AppSettings settings)
    {
        _settings = settings;
        Rows =
        [
            new(this, "nc", T("nc.nc"), T("hk.nc.desc")),
            new(this, "ambient", T("nc.ambient"), T("hk.ambient.desc")),
            new(this, "off", T("nc.off"), T("hk.off.desc")),
            new(this, "toggle", T("hk.toggle"), T("hk.toggle.desc")),
        ];
        foreach (var row in Rows)
            if (settings.Hotkeys.TryGetValue(row.Action, out var e)) row.Load(new Hotkey(e.Modifiers, e.Key));
        _service.Pressed += action => Triggered?.Invoke(action);
        RegisterAll();
    }

    internal void Assign(HotkeyRow row, Hotkey? hotkey)
    {
        // The same combination cannot drive two actions: move it.
        if (hotkey != null)
            foreach (var other in Rows.Where(r => r != row && r.Hotkey == hotkey)) other.Load(null);
        row.Load(hotkey);
        _settings.Hotkeys = Rows.Where(r => r.Hotkey != null)
            .ToDictionary(r => r.Action, r => new AppSettings.HotkeyEntry(r.Hotkey!.Modifiers, r.Hotkey.Key));
        _settings.Save();
        RegisterAll();
    }

    /// <summary>While a combination is being typed, hotkeys are released so the keys reach the recorder.</summary>
    internal void OnRecordingChanged(bool recording)
    {
        if (recording) _service.UnregisterAll();
        else RegisterAll();
    }

    private void RegisterAll()
    {
        _service.UnregisterAll();
        foreach (var row in Rows)
        {
            if (row.Hotkey is not { } hk) { row.Status = ""; row.HasError = false; continue; }
            bool ok = _service.Register(row.Action, hk);
            row.HasError = !ok;
            row.Status = ok ? T("hk.active") : T("hk.conflict");
        }
    }

    public void Dispose() => _service.Dispose();
}
