using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Resonance.Core.Device;
using Resonance.Core.Protocol;
using Resonance.Core.Transport;
using TouchAction = Resonance.Core.Protocol.TouchAction;

using static Resonance.Core.Localization.Loc;

namespace Resonance.App;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

public sealed class RelayCommand(Func<Task> run) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public async void Execute(object? parameter) => await run();
}

/// <summary>Runs the last queued action after a quiet period (sliders).</summary>
public sealed class Debouncer(TimeSpan delay)
{
    private CancellationTokenSource? _cts;
    public void Run(Func<Task> action)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        Task.Delay(delay, cts.Token).ContinueWith(async t => { if (!t.IsCanceled) await action(); }, TaskScheduler.Default);
    }
}

public sealed class BandViewModel(MainViewModel owner, int index, string label) : ObservableObject
{
    private int _value;
    public string Label { get; } = label;
    /// <summary>Gain in dB (−6…+6); the protocol stores steps 0…12.</summary>
    public int Value
    {
        get => _value;
        set { if (SetField(ref _value, value)) owner.OnBandChanged(index); }
    }
    internal void Load(int steps) { _value = steps - 6; Raise(nameof(Value)); }
}

public sealed class GestureViewModel(MainViewModel owner, GestureCapability cap) : ObservableObject
{
    private Option<TouchFunction>? _selected;
    public string Label { get; } = Labels.Of(cap.Action);
    public TouchPreset Preset => cap.Preset;
    public TouchAction Action => cap.Action;
    public IReadOnlyList<Option<TouchFunction>> Choices { get; } =
        cap.Choices.Select(f => new Option<TouchFunction>(f, Labels.Of(f))).ToList();
    public Option<TouchFunction>? Selected
    {
        get => _selected;
        set { if (SetField(ref _selected, value) && value != null) owner.OnGestureChanged(this, value.Value); }
    }
    internal void Load(TouchFunction f) { _selected = Choices.FirstOrDefault(c => c.Value == f); Raise(nameof(Selected)); }
}

public sealed class MainViewModel : ObservableObject
{
    private readonly Dispatcher _ui = Application.Current.Dispatcher;
    private readonly Debouncer _ambientDebounce = new(TimeSpan.FromMilliseconds(200));
    private readonly Debouncer _eqDebounce = new(TimeSpan.FromMilliseconds(250));
    private readonly Debouncer _volumeDebounce = new(TimeSpan.FromMilliseconds(150));
    private Headphones? _hp;
    private bool _loading;   // true while copying device state into properties: suppresses writes
    private bool _closing;

    public HotkeysViewModel Hotkeys { get; }
    private readonly AppSettings _settings;

    public IReadOnlyList<Option<string>> Languages { get; }
    private Option<string> _selectedLanguage;
    /// <summary>UI language; saved to settings.json and applied at the next start.</summary>
    public Option<string> SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !SetField(ref _selectedLanguage, value)) return;
            _settings.Language = value.Value;
            _settings.Save();
            Status = T("settings.language.caption");
        }
    }

    public MainViewModel()
    {
        Bands = new();
        _settings = AppSettings.Load();
        Hotkeys = new HotkeysViewModel(_settings);
        Languages = [new("auto", T("lang.auto")), .. Resonance.Core.Localization.Loc.Available.Select(l => new Option<string>(l.Code, l.Name))];
        _selectedLanguage = Languages.FirstOrDefault(l => l.Value == _settings.Language) ?? Languages[0];
        Hotkeys.Triggered += OnHotkey;
        PlayPauseCommand = new RelayCommand(() => Send(hp => hp.SendPlaybackAsync(
            _hp?.State.NowPlaying?.Status == PlaybackStatus.Playing ? PlaybackControl.Pause : PlaybackControl.Play)));
        NextCommand = new RelayCommand(() => Send(hp => hp.SendPlaybackAsync(PlaybackControl.Next)));
        PreviousCommand = new RelayCommand(() => Send(hp => hp.SendPlaybackAsync(PlaybackControl.Previous)));
        PowerOffCommand = new RelayCommand(PowerOffAsync);
        ReconnectCommand = new RelayCommand(() => ConnectLoopAsync());
    }

    public event Action? DeviceStateChanged;

    /// <summary>The connected headphones, or null while searching / reconnecting.</summary>
    public Headphones? Device => _hp;

    // ------------------------------------------------------------ connection

    private string _status = T("status.searching");
    private bool _isConnected;
    public string Status { get => _status; private set => SetField(ref _status, value); }
    public bool IsConnected { get => _isConnected; private set => SetField(ref _isConnected, value); }

    private int _connecting;
    public async Task ConnectLoopAsync()
    {
        if (Interlocked.Exchange(ref _connecting, 1) == 1) return;
        try
        {
            while (!_closing && _hp is null)
            {
                try
                {
                    // Last headphones first: discovery takes several seconds.
                    Headphones? hp = null;
                    if (DeviceMemory.Last is { } last)
                    {
                        Status = T("status.connecting");
                        try { hp = await Headphones.ConnectAsync(last); } catch (Exception) { hp = null; }
                    }
                    var found = hp is null ? await MdrConnection.FindPairedAsync() : [];
                    if (hp is null && found.Count == 0) { Status = T("status.nopaired"); }
                    else
                    {
                        if (hp is null)
                        {
                            Status = T("status.connectingto", found[0].Name);
                            hp = await Headphones.ConnectAsync(found[0].Address);
                        }
                        hp.StateChanged += () => _ui.BeginInvoke(Sync);
                        hp.Disconnected += _ => _ui.BeginInvoke(OnDisconnected);
                        _hp = hp;
                        IsConnected = true;
                        Status = T("status.connected");
                        _ui.Invoke(Sync);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Status = T("status.retry", ex.Message.Split('\n')[0].Trim());
                }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
        finally { Interlocked.Exchange(ref _connecting, 0); }
    }

    private async void OnDisconnected()
    {
        var hp = _hp;
        _hp = null;
        IsConnected = false;
        Status = T("status.lost");
        if (hp != null) await hp.DisposeAsync();
        await Task.Delay(1500);
        await ConnectLoopAsync();
    }

    public async Task ShutdownAsync()
    {
        _closing = true;
        Hotkeys.Dispose();
        if (_hp != null) await _hp.DisposeAsync();
    }

    private async Task Send(Func<Headphones, Task> action)
    {
        if (_loading || _hp is not { } hp) return;
        try { await action(hp); }
        catch (Exception ex) { _ui.Invoke(() => Status = T("status.rejected", ex.Message)); }
    }

    // ------------------------------------------------------------ device → UI

    private void Sync()
    {
        if (_hp is null) return;
        var s = _hp.State;
        _loading = true;
        try
        {
            DeviceName = s.ModelName ?? _hp.DeviceName ?? T("headphones");
            Firmware = s.FirmwareVersion is { } fw ? T("firmware", fw) : "";
            Codec = s.Codec is { } c ? Labels.Of(c) : "";
            BatteryLeft = s.BatteryLeft; BatteryRight = s.BatteryRight; BatteryCase = s.BatteryCase;
            Raise(nameof(BatterySummary));

            if (s.NoiseControl is { } nc)
            {
                NcMode = !nc.Enabled ? 2 : nc.Mode == NcAsmMode.NoiseCancelling ? 0 : 1;
                AmbientLevel = nc.AmbientLevel;
                VoiceFocus = nc.VoiceFocus;
                AutoAmbient = nc.NoiseAdaptive;
                SelectedAutoAmbientSensitivity = Labels.AutoAmbientSensitivities.FirstOrDefault(o => o.Value == nc.Sensitivity);
            }
            if (s.VoiceAssistantOptions is { } vao && VoiceAssistantOptions.Count != vao.Count)
                VoiceAssistantOptions = vao.Select(v => new Option<VoiceAssistant>(v, Labels.Of(v))).ToList();
            SelectedVoiceAssistant = VoiceAssistantOptions.FirstOrDefault(o => o.Value == s.VoiceAssistant);
            VoiceGuidanceVolume = s.VoiceGuidanceVolume ?? 0;
            FitTestText = s.FitTest is { } ft ? Labels.Of(ft) : T("fit.hint");
            IsFitTestRunning = s.FitTest?.Status == FitTestStatus.Started;
            if (s.LastDeviceAction is { } da && da != _lastAction)
            {
                _lastAction = da;
                var name = s.PairedDevices?.FirstOrDefault(d => d.Address == da.Address)?.Name ?? da.Address;
                var verb = T(da.Action switch { DeviceAction.Connect => "device.action.connect", DeviceAction.Disconnect => "device.action.disconnect", _ => "device.action.forget" }, name);
                Status = $"{verb} : {(da.InProgress ? T("device.inprogress") : da.Success ? T("device.ok") : T("device.fail"))}";
            }

            if (s.EqualizerCapability is { } cap && EqPresets.Count != cap.Presets.Count)
            {
                EqPresets = cap.Presets.Select(p => new Option<EqPreset>(p, Labels.Of(p))).ToList();
                Bands.Clear();
                for (int i = 0; i < cap.BandCount; i++)
                {
                    int hz = i < cap.FrequenciesHz.Count ? cap.FrequenciesHz[i] : 0;
                    Bands.Add(new BandViewModel(this, i, hz >= 1000 ? $"{hz / 1000}k" : $"{hz}"));
                }
            }
            if (s.Equalizer is { } eq)
            {
                SelectedEqPreset = EqPresets.FirstOrDefault(o => o.Value == eq.Preset);
                for (int i = 0; i < Math.Min(eq.Bands.Length, Bands.Count); i++) Bands[i].Load(eq.Bands[i]);
            }

            Dsee = s.Dsee ?? false;
            SelectedPriority = Labels.Priorities.FirstOrDefault(o => o.Value == s.ConnectionPriority);
            Bgm = s.ListeningModeBgm ?? false;
            SelectedRoomSize = Labels.RoomSizes.FirstOrDefault(o => o.Value == s.BgmRoomSize);

            if (s.SpeakToChat is { } stc)
            {
                SpeakToChat = stc.Enabled;
                SelectedSensitivity = Labels.Sensitivities.FirstOrDefault(o => o.Value == stc.Sensitivity);
                SelectedModeOutTime = Labels.ModeOutTimes.FirstOrDefault(o => o.Value == stc.ModeOutTime);
            }
            HeadGestures = s.HeadGestures ?? false;
            if (s.AutoPowerOffOptions is { } apo && AutoPowerOffOptions.Count != apo.Count)
                AutoPowerOffOptions = apo.Select(a => new Option<AutoPowerOff>(a, Labels.Of(a))).ToList();
            SelectedAutoPowerOff = AutoPowerOffOptions.FirstOrDefault(o => o.Value == s.AutoPowerOff);
            Multipoint = s.Multipoint ?? false;
            Sidetone = s.Sidetone ?? false;
            OneSideNc = s.OneSideNoiseControl ?? false;
            CaringCharge = s.CaringCharge ?? false;
            CaringChargeLabel = T("battery.caring.caption", s.CaringChargeThreshold ?? 80);
            VoiceGuidance = s.VoiceGuidance ?? false;

            if (s.TouchOptionsLeft is { } tl && TouchLeftOptions.Count != tl.Count)
                TouchLeftOptions = tl.Select(p => new Option<TouchPreset>(p, Labels.Of(p))).ToList();
            if (s.TouchOptionsRight is { } tr && TouchRightOptions.Count != tr.Count)
                TouchRightOptions = tr.Select(p => new Option<TouchPreset>(p, Labels.Of(p))).ToList();
            SelectedTouchLeft = TouchLeftOptions.FirstOrDefault(o => o.Value == s.TouchLeft);
            SelectedTouchRight = TouchRightOptions.FirstOrDefault(o => o.Value == s.TouchRight);
            if (s.QuickAccess is { Count: 2 } qa)
            {
                SelectedQuickAccess1 = QuickAccessOptions.FirstOrDefault(o => o.Value == qa[0]);
                SelectedQuickAccess2 = QuickAccessOptions.FirstOrDefault(o => o.Value == qa[1]);
            }
            HasQuickAccess = s.QuickAccess is { Count: 2 };
            LeftGestures = BuildGestures(s, s.TouchLeft);
            RightGestures = BuildGestures(s, s.TouchRight);

            if (s.NowPlaying is { } np)
            {
                TrackTitle = string.IsNullOrEmpty(np.Title) ? T("track.none") : np.Title;
                TrackArtist = string.Join(" — ", new[] { np.Artist, np.Album }.Where(x => !string.IsNullOrEmpty(x)));
                IsPlaying = np.Status == PlaybackStatus.Playing;
            }
            VolumeMax = s.MusicVolumeMax ?? 30;
            Volume = s.MusicVolume ?? 0;

            PairedDevices = s.PairedDevices?.Select(d => new PairedDeviceRow(this, d.Name, d.Address, d.Connected, d.Playing)).ToList() ?? [];
        }
        finally { _loading = false; }
        DeviceStateChanged?.Invoke();
    }

    private IReadOnlyList<GestureViewModel> BuildGestures(HeadphoneState s, TouchPreset? preset)
    {
        if (preset is null || s.GestureCapabilities is null) return [];
        var stored = s.TouchCustomizations?.FirstOrDefault(c => c.Preset == preset);
        var list = s.GestureCapabilities.Where(g => g.Preset == preset && g.Choices.Count > 1)
            .Select(g => new GestureViewModel(this, g)).ToList();
        foreach (var g in list)
            g.Load(stored?.Bindings.FirstOrDefault(b => b.Action == g.Action)?.Function
                   ?? s.GestureCapabilities.First(c => c.Preset == g.Preset && c.Action == g.Action).Default);
        return list;
    }

    // ------------------------------------------------------------ header

    private string _deviceName = "Resonance", _firmware = "", _codec = "";
    private BatteryInfo? _batteryLeft, _batteryRight, _batteryCase;
    public string DeviceName { get => _deviceName; private set => SetField(ref _deviceName, value); }
    public string Firmware { get => _firmware; private set => SetField(ref _firmware, value); }
    public string Codec { get => _codec; private set => SetField(ref _codec, value); }
    public BatteryInfo? BatteryLeft { get => _batteryLeft; private set => SetField(ref _batteryLeft, value); }
    public BatteryInfo? BatteryRight { get => _batteryRight; private set => SetField(ref _batteryRight, value); }
    public BatteryInfo? BatteryCase { get => _batteryCase; private set => SetField(ref _batteryCase, value); }
    public string BatterySummary =>
        IsConnected ? T("battery.summary", BatteryLeft?.Level, BatteryRight?.Level, BatteryCase?.Level) : Status;

    // ------------------------------------------------------------ now playing

    private string _trackTitle = T("track.none"), _trackArtist = "";
    private bool _isPlaying;
    private int _volume, _volumeMax = 30;
    public string TrackTitle { get => _trackTitle; private set => SetField(ref _trackTitle, value); }
    public string TrackArtist { get => _trackArtist; private set => SetField(ref _trackArtist, value); }
    public bool IsPlaying { get => _isPlaying; private set => SetField(ref _isPlaying, value); }
    public int VolumeMax { get => _volumeMax; private set => SetField(ref _volumeMax, value); }
    public int Volume
    {
        get => _volume;
        set { if (SetField(ref _volume, value) && !_loading) _volumeDebounce.Run(() => Send(hp => hp.SetMusicVolumeAsync(value))); }
    }
    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }

    // ------------------------------------------------------------ noise control

    private int _ncMode;
    private int _ambientLevel = 20;
    private bool _voiceFocus;
    /// <summary>0 = noise cancelling, 1 = ambient sound, 2 = off.</summary>
    public int NcMode
    {
        get => _ncMode;
        set { if (SetField(ref _ncMode, value)) { Raise(nameof(IsAmbient)); PushNoiseControl(); } }
    }
    public bool IsAmbient => _ncMode == 1;
    public int AmbientLevel
    {
        get => _ambientLevel;
        set { if (SetField(ref _ambientLevel, value) && !_loading) _ambientDebounce.Run(() => _ui.InvokeAsync(PushNoiseControl).Task); }
    }
    public bool VoiceFocus { get => _voiceFocus; set { if (SetField(ref _voiceFocus, value)) PushNoiseControl(); } }

    private bool _autoAmbient;
    private Option<NoiseAdaptiveSensitivity>? _selectedAutoAmbientSensitivity;
    /// <summary>Ambient level follows the surroundings ("noise adaptive"); only in ambient mode.</summary>
    public bool AutoAmbient
    {
        get => _autoAmbient;
        set { if (SetField(ref _autoAmbient, value)) { Raise(nameof(IsManualAmbient)); PushNoiseControl(); } }
    }
    public bool IsManualAmbient => !_autoAmbient;
    public IReadOnlyList<Option<NoiseAdaptiveSensitivity>> AutoAmbientSensitivities => Labels.AutoAmbientSensitivities;
    public Option<NoiseAdaptiveSensitivity>? SelectedAutoAmbientSensitivity
    {
        get => _selectedAutoAmbientSensitivity;
        set { if (SetField(ref _selectedAutoAmbientSensitivity, value)) PushNoiseControl(); }
    }

    private void PushNoiseControl()
    {
        if (_loading || _hp?.State.NoiseControl is not { } nc) return;
        var next = nc with
        {
            Enabled = _ncMode != 2,
            Mode = _ncMode == 0 ? NcAsmMode.NoiseCancelling : NcAsmMode.Ambient,
            AmbientLevel = _ambientLevel,
            VoiceFocus = _voiceFocus,
            NoiseAdaptive = _autoAmbient,
            Sensitivity = _selectedAutoAmbientSensitivity?.Value ?? nc.Sensitivity,
        };
        _ = Send(hp => hp.SetNoiseControlAsync(next));
    }

    private void OnHotkey(string action)
    {
        if (!IsConnected) { Status = T("status.hotkeyignored"); return; }
        NcMode = action switch { "nc" => 0, "ambient" => 1, "off" => 2, _ => (NcMode + 1) % 3 };
    }

    /// <summary>Tray shortcut: cycles noise cancelling → ambient → off.</summary>
    public void CycleNoiseMode() => NcMode = (NcMode + 1) % 3;

    // ------------------------------------------------------------ equalizer

    private IReadOnlyList<Option<EqPreset>> _eqPresets = [];
    private Option<EqPreset>? _selectedEqPreset;
    public IReadOnlyList<Option<EqPreset>> EqPresets { get => _eqPresets; private set => SetField(ref _eqPresets, value); }
    public ObservableCollection<BandViewModel> Bands { get; }
    public Option<EqPreset>? SelectedEqPreset
    {
        get => _selectedEqPreset;
        set
        {
            if (SetField(ref _selectedEqPreset, value) && value != null)
                _ = Send(hp => hp.SetEqualizerPresetAsync(value.Value));
        }
    }

    internal void OnBandChanged(int index)
    {
        if (_loading) return;
        // Editing a band switches to the custom preset, like the phone app.
        var preset = _selectedEqPreset?.Value is EqPreset.Custom or EqPreset.User1 or EqPreset.User2 or EqPreset.User3
            ? _selectedEqPreset.Value : EqPreset.Custom;
        var steps = Bands.Select(b => b.Value + 6).ToArray();
        _eqDebounce.Run(() => Send(hp => hp.SetEqualizerAsync(preset, steps)));
    }

    // ------------------------------------------------------------ sound quality

    private bool _dsee, _bgm;
    private Option<PriorMode>? _selectedPriority;
    private Option<RoomSize>? _selectedRoomSize;
    public bool Dsee { get => _dsee; set { if (SetField(ref _dsee, value)) _ = Send(hp => hp.SetDseeAsync(value)); } }
    public IReadOnlyList<Option<PriorMode>> Priorities => Labels.Priorities;
    public Option<PriorMode>? SelectedPriority
    {
        get => _selectedPriority;
        set
        {
            if (SetField(ref _selectedPriority, value) && value != null)
                _ = Send(hp => hp.SetConnectionPriorityAsync(value.Value));
        }
    }
    public bool Bgm { get => _bgm; set { if (SetField(ref _bgm, value)) PushBgm(); } }
    public IReadOnlyList<Option<RoomSize>> RoomSizes => Labels.RoomSizes;
    public Option<RoomSize>? SelectedRoomSize { get => _selectedRoomSize; set { if (SetField(ref _selectedRoomSize, value)) PushBgm(); } }
    private void PushBgm() => _ = Send(hp => hp.SetListeningModeAsync(_bgm, _selectedRoomSize?.Value ?? RoomSize.Small));

    // ------------------------------------------------------------ touch

    private IReadOnlyList<Option<TouchPreset>> _touchLeftOptions = [], _touchRightOptions = [];
    private Option<TouchPreset>? _selectedTouchLeft, _selectedTouchRight;
    private IReadOnlyList<GestureViewModel> _leftGestures = [], _rightGestures = [];
    public IReadOnlyList<Option<TouchPreset>> TouchLeftOptions { get => _touchLeftOptions; private set => SetField(ref _touchLeftOptions, value); }
    public IReadOnlyList<Option<TouchPreset>> TouchRightOptions { get => _touchRightOptions; private set => SetField(ref _touchRightOptions, value); }
    public Option<TouchPreset>? SelectedTouchLeft { get => _selectedTouchLeft; set { if (SetField(ref _selectedTouchLeft, value)) PushTouch(); } }
    public Option<TouchPreset>? SelectedTouchRight { get => _selectedTouchRight; set { if (SetField(ref _selectedTouchRight, value)) PushTouch(); } }
    public IReadOnlyList<GestureViewModel> LeftGestures { get => _leftGestures; private set => SetField(ref _leftGestures, value); }
    public IReadOnlyList<GestureViewModel> RightGestures { get => _rightGestures; private set => SetField(ref _rightGestures, value); }

    private void PushTouch()
    {
        if (_selectedTouchLeft is { } l && _selectedTouchRight is { } r)
            _ = Send(hp => hp.SetTouchPresetsAsync(l.Value, r.Value));
    }

    private Option<byte>? _selectedQuickAccess1, _selectedQuickAccess2;
    private bool _hasQuickAccess;
    public static IReadOnlyList<Option<byte>> QuickAccessOptions { get; } = [new(0x00, T("qa.none")), new(0x01, T("qa.spotify"))];
    public bool HasQuickAccess { get => _hasQuickAccess; private set => SetField(ref _hasQuickAccess, value); }
    public Option<byte>? SelectedQuickAccess1 { get => _selectedQuickAccess1; set { if (SetField(ref _selectedQuickAccess1, value)) PushQuickAccess(); } }
    public Option<byte>? SelectedQuickAccess2 { get => _selectedQuickAccess2; set { if (SetField(ref _selectedQuickAccess2, value)) PushQuickAccess(); } }
    private void PushQuickAccess()
    {
        if (_selectedQuickAccess1 is { } a && _selectedQuickAccess2 is { } b)
            _ = Send(hp => hp.SetQuickAccessAsync(a.Value, b.Value));
    }

    internal void OnGestureChanged(GestureViewModel g, TouchFunction f) =>
        _ = Send(hp => hp.SetGestureAsync(g.Preset, g.Action, f));

    // ------------------------------------------------------------ system

    private bool _speakToChat, _headGestures, _multipoint, _sidetone, _oneSideNc, _caringCharge, _voiceGuidance;
    private Option<DetectSensitivity>? _selectedSensitivity;
    private Option<ModeOutTime>? _selectedModeOutTime;
    private IReadOnlyList<Option<AutoPowerOff>> _autoPowerOffOptions = [];
    private Option<AutoPowerOff>? _selectedAutoPowerOff;
    private string _caringChargeLabel = "";

    public bool SpeakToChat { get => _speakToChat; set { if (SetField(ref _speakToChat, value)) _ = Send(hp => hp.SetSpeakToChatAsync(value)); } }
    public IReadOnlyList<Option<DetectSensitivity>> Sensitivities => Labels.Sensitivities;
    public IReadOnlyList<Option<ModeOutTime>> ModeOutTimes => Labels.ModeOutTimes;
    public Option<DetectSensitivity>? SelectedSensitivity { get => _selectedSensitivity; set { if (SetField(ref _selectedSensitivity, value)) PushStcOptions(); } }
    public Option<ModeOutTime>? SelectedModeOutTime { get => _selectedModeOutTime; set { if (SetField(ref _selectedModeOutTime, value)) PushStcOptions(); } }
    private void PushStcOptions()
    {
        if (_selectedSensitivity is { } s && _selectedModeOutTime is { } t)
            _ = Send(hp => hp.SetSpeakToChatOptionsAsync(s.Value, t.Value));
    }

    public bool HeadGestures { get => _headGestures; set { if (SetField(ref _headGestures, value)) _ = Send(hp => hp.SetHeadGesturesAsync(value)); } }
    public IReadOnlyList<Option<AutoPowerOff>> AutoPowerOffOptions { get => _autoPowerOffOptions; private set => SetField(ref _autoPowerOffOptions, value); }
    public Option<AutoPowerOff>? SelectedAutoPowerOff
    {
        get => _selectedAutoPowerOff;
        set { if (SetField(ref _selectedAutoPowerOff, value) && value != null) _ = Send(hp => hp.SetAutoPowerOffAsync(value.Value)); }
    }
    public bool Multipoint { get => _multipoint; set { if (SetField(ref _multipoint, value)) _ = Send(hp => hp.SetMultipointAsync(value)); } }
    public bool Sidetone { get => _sidetone; set { if (SetField(ref _sidetone, value)) _ = Send(hp => hp.SetSidetoneAsync(value)); } }
    public bool OneSideNc { get => _oneSideNc; set { if (SetField(ref _oneSideNc, value)) _ = Send(hp => hp.SetOneSideNoiseControlAsync(value)); } }
    public bool CaringCharge { get => _caringCharge; set { if (SetField(ref _caringCharge, value)) _ = Send(hp => hp.SetCaringChargeAsync(value)); } }
    public string CaringChargeLabel { get => _caringChargeLabel; private set => SetField(ref _caringChargeLabel, value); }
    public bool VoiceGuidance { get => _voiceGuidance; set { if (SetField(ref _voiceGuidance, value)) _ = Send(hp => hp.SetVoiceGuidanceAsync(value)); } }

    public ICommand PowerOffCommand { get; }
    public ICommand ReconnectCommand { get; }

    private async Task PowerOffAsync()
    {
        var answer = MessageBox.Show(T("poweroff.confirm"), "Resonance", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) await Send(hp => hp.PowerOffAsync());
    }

    private IReadOnlyList<Option<VoiceAssistant>> _voiceAssistantOptions = [];
    private Option<VoiceAssistant>? _selectedVoiceAssistant;
    private int _voiceGuidanceVolume;
    public IReadOnlyList<Option<VoiceAssistant>> VoiceAssistantOptions { get => _voiceAssistantOptions; private set => SetField(ref _voiceAssistantOptions, value); }
    public Option<VoiceAssistant>? SelectedVoiceAssistant
    {
        get => _selectedVoiceAssistant;
        set { if (SetField(ref _selectedVoiceAssistant, value) && value != null) _ = Send(hp => hp.SetVoiceAssistantAsync(value.Value)); }
    }
    public int VoiceGuidanceVolume
    {
        get => _voiceGuidanceVolume;
        set { if (SetField(ref _voiceGuidanceVolume, value) && !_loading) _volumeDebounce.Run(() => Send(hp => hp.SetVoiceGuidanceVolumeAsync(value))); }
    }

    // ------------------------------------------------------------ earpiece fitting test

    private string _fitTestText = "";
    private bool _isFitTestRunning;
    public string FitTestText { get => _fitTestText; private set => SetField(ref _fitTestText, value); }
    public bool IsFitTestRunning { get => _isFitTestRunning; private set => SetField(ref _isFitTestRunning, value); }
    public ICommand FitTestCommand => new RelayCommand(async () =>
    {
        if (_hp is null) return;
        if (IsFitTestRunning) { await Send(hp => hp.EndFitTestAsync()); return; }
        IsFitTestRunning = true;
        FitTestText = Labels.Of(new FitTestState(FitTestStatus.Started, FitTestError.None, null, null));
        await Send(hp => hp.StartFitTestAsync());
        // Leave test mode once the headphones report a final result (or after 40 s).
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline && _hp?.State.FitTest is { Status: FitTestStatus.Started }) await Task.Delay(500);
        await Send(hp => hp.EndFitTestAsync());
        IsFitTestRunning = false;
    });

    // ------------------------------------------------------------ devices

    private DeviceActionResult? _lastAction;
    private IReadOnlyList<PairedDeviceRow> _pairedDevices = [];
    public IReadOnlyList<PairedDeviceRow> PairedDevices { get => _pairedDevices; private set => SetField(ref _pairedDevices, value); }
    public ICommand PairingModeCommand => new RelayCommand(async () =>
    {
        if (MessageBox.Show(T("pairing.confirm"),
                "Resonance", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Send(hp => hp.EnterPairingModeAsync());
        Status = T("status.pairing");
    });

    internal Task DeviceActionAsync(PairedDeviceRow row, DeviceAction action)
    {
        if (action == DeviceAction.Forget && MessageBox.Show(T("forget.confirm", row.Name),
                "Resonance", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return Task.CompletedTask;
        return Send(hp => hp.SendDeviceActionAsync(action, row.Address));
    }

    internal Task SwitchSourceAsync(PairedDeviceRow row) => Send(hp => hp.SwitchAudioSourceAsync(row.Address));
}

public sealed class PairedDeviceRow(MainViewModel owner, string name, string address, bool connected, bool playing)
{
    public string Name { get; } = name;
    public string Address { get; } = address;
    public string State { get; } = playing ? T("device.state.playing") : connected ? T("device.state.connected") : T("device.state.off");
    public bool IsConnected { get; } = connected;
    public bool CanPlayFrom { get; } = connected && !playing;
    public string ConnectLabel => IsConnected ? T("devices.disconnect") : T("devices.connect");
    public ICommand ConnectCommand => new RelayCommand(() => owner.DeviceActionAsync(this, IsConnected ? DeviceAction.Disconnect : DeviceAction.Connect));
    public ICommand ForgetCommand => new RelayCommand(() => owner.DeviceActionAsync(this, DeviceAction.Forget));
    public ICommand PlayFromCommand => new RelayCommand(() => owner.SwitchSourceAsync(this));
}
