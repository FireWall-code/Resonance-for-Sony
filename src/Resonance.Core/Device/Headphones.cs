using System.Text;
using Resonance.Core.Protocol;
using Resonance.Core.Transport;

namespace Resonance.Core.Device;

/// <summary>Groups of settings read from the headphones, so a CLI command only reads what it needs.</summary>
[Flags]
public enum ReadGroup
{
    None = 0, Info = 1, Battery = 2, Noise = 4, Equalizer = 8, Audio = 16, Touch = 32,
    Settings = 64, Playback = 128, Devices = 256, All = 511,
}

/// <summary>Remembers the last headphones used, to skip the slow discovery next time.</summary>
public static class DeviceMemory
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resonance", "last-device.txt");

    public static ulong? Last =>
        File.Exists(FilePath) && ulong.TryParse(File.ReadAllText(FilePath).Trim(), System.Globalization.NumberStyles.HexNumber, null, out var a)
            ? a : null;

    public static void Remember(ulong address)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, address.ToString("X12"));
        }
        catch (IOException) { /* best effort */ }
    }
}

/// <summary>
/// High-level controller: reads every supported setting at connect time, keeps
/// <see cref="State"/> in sync with RET/NTFY messages and exposes typed setters.
/// </summary>
public sealed class Headphones : IAsyncDisposable
{
    private readonly MdrConnection _conn;
    public HeadphoneState State { get; } = new();

    /// <summary>Raised (on a background thread) after any state change.</summary>
    public event Action? StateChanged;
    public event Action<Exception?>? Disconnected;

    public string? DeviceName => _conn.DeviceName;
    public ulong Address => _conn.Address;

    private Headphones(MdrConnection conn)
    {
        _conn = conn;
        _conn.MessageReceived += OnMessage;
        _conn.Disconnected += e => Disconnected?.Invoke(e);
    }

    public event Action<string>? Trace
    {
        add => _conn.Trace += value;
        remove => _conn.Trace -= value;
    }

    /// <summary>Connects and reads <paramref name="read"/> (everything by default).</summary>
    public static async Task<Headphones> ConnectAsync(ulong address, ReadGroup read = ReadGroup.All, CancellationToken ct = default)
    {
        var hp = new Headphones(await MdrConnection.ConnectAsync(address, ct));
        DeviceMemory.Remember(address);
        await hp.EnsureAsync(read, ct);
        return hp;
    }

    // ---------------------------------------------------------------- reading

    private static readonly (DataType T, byte Cmd, byte Inq, ReadGroup Group)[] InitialQueries =
    {
        (DataType.DataMdrNo1, T1.ConnectGetProtocolInfo, 0x00, ReadGroup.Info),
        (DataType.DataMdrNo1, T1.ConnectGetDeviceInfo, Inq.DeviceInfoModelName, ReadGroup.Info),
        (DataType.DataMdrNo1, T1.ConnectGetDeviceInfo, Inq.DeviceInfoFwVersion, ReadGroup.Info),
        (DataType.DataMdrNo1, T1.ConnectGetCapabilityInfo, 0x00, ReadGroup.Info),
        (DataType.DataMdrNo1, T1.Common | Op.GetStatus, Inq.CommonAudioCodec, ReadGroup.Info),
        (DataType.DataMdrNo1, T1.Common | Op.GetStatus, Inq.CommonUpscalingEffect, ReadGroup.Audio),
        (DataType.DataMdrNo1, T1.Power | Op.GetStatus, Inq.LrBattery, ReadGroup.Battery),
        (DataType.DataMdrNo1, T1.Power | Op.GetStatus, Inq.CradleBattery, ReadGroup.Battery),
        (DataType.DataMdrNo1, T1.Power | Op.GetCapability, Inq.AutoPowerOffWearing, ReadGroup.Settings),
        (DataType.DataMdrNo1, T1.Power | Op.GetParam, Inq.AutoPowerOffWearing, ReadGroup.Settings),
        (DataType.DataMdrNo1, T1.NcAsm | Op.GetParam, Inq.NcAsmNoiseAdaptive, ReadGroup.Noise),
        (DataType.DataMdrNo1, T1.NcAsm | Op.GetParam, Inq.NcAmbToggle, ReadGroup.Noise),
        (DataType.DataMdrNo1, T1.EqEbb | Op.GetCapability, Inq.PresetEqAndErrorCode, ReadGroup.Equalizer),
        (DataType.DataMdrNo1, T1.EqEbb | Op.GetExt, Inq.PresetEqAndErrorCode, ReadGroup.Equalizer),
        (DataType.DataMdrNo1, T1.EqEbb | Op.GetParam, Inq.PresetEqAndErrorCode, ReadGroup.Equalizer),
        (DataType.DataMdrNo1, T1.Audio | Op.GetParam, Inq.Upscaling, ReadGroup.Audio),
        (DataType.DataMdrNo1, T1.Audio | Op.GetParam, Inq.ConnectionModeLdac, ReadGroup.Audio),
        (DataType.DataMdrNo1, T1.Audio | Op.GetParam, Inq.BgmModeAndError, ReadGroup.Audio),
        (DataType.DataMdrNo1, T1.System | Op.GetParam, Inq.SpeakToChat, ReadGroup.Noise),
        (DataType.DataMdrNo1, T1.System | Op.GetExt, Inq.SpeakToChat, ReadGroup.Noise),
        (DataType.DataMdrNo1, T1.System | Op.GetCapability, Inq.AssignableSettings, ReadGroup.Touch),
        (DataType.DataMdrNo1, T1.System | Op.GetParam, Inq.AssignableSettings, ReadGroup.Touch),
        (DataType.DataMdrNo1, T1.System | Op.GetExt, Inq.AssignableSettings, ReadGroup.Touch),
        (DataType.DataMdrNo1, T1.System | Op.GetParam, Inq.HeadGesture, ReadGroup.Touch),
        (DataType.DataMdrNo1, T1.System | Op.GetParam, Inq.QuickAccess, ReadGroup.Touch),
        (DataType.DataMdrNo1, T1.System | Op.GetCapability, Inq.VoiceAssistant, ReadGroup.Settings),
        (DataType.DataMdrNo1, T1.System | Op.GetParam, Inq.VoiceAssistant, ReadGroup.Settings),
        (DataType.DataMdrNo1, T1.GeneralSetting | Op.GetParam, Inq.GsMultipoint, ReadGroup.Settings),
        (DataType.DataMdrNo1, T1.GeneralSetting | Op.GetParam, Inq.GsSidetone, ReadGroup.Settings),
        (DataType.DataMdrNo1, T1.GeneralSetting | Op.GetParam, Inq.GsOneSideNc, ReadGroup.Settings),
        (DataType.DataMdrNo1, T1.Play | Op.GetCapability, Inq.PlaybackController, ReadGroup.Playback),
        (DataType.DataMdrNo1, T1.Play | Op.GetParam, Inq.PlaybackController, ReadGroup.Playback),
        (DataType.DataMdrNo1, T1.Play | Op.GetParam, Inq.MusicVolume, ReadGroup.Playback),
        (DataType.DataMdrNo2, T2.Power | Op.GetCapability, Inq.CaringCharge, ReadGroup.Settings),
        (DataType.DataMdrNo2, T2.Power | Op.GetParam, Inq.CaringCharge, ReadGroup.Settings),
        (DataType.DataMdrNo2, T2.VoiceGuidance | Op.GetParam, Inq.VoiceGuidanceOnOff, ReadGroup.Settings),
        (DataType.DataMdrNo2, T2.VoiceGuidance | Op.GetParam, Inq.VoiceGuidanceVolume, ReadGroup.Settings),
        (DataType.DataMdrNo2, T2.Peripheral | Op.GetParam, Inq.PairedDevices, ReadGroup.Devices),
    };

    private ReadGroup _loaded;

    /// <summary>Reads every setting (the app does this once at connection).</summary>
    public Task RefreshAsync(CancellationToken ct = default) => ReadAsync(ReadGroup.All, ct);

    /// <summary>Reads the given groups, skipping those already read on this connection.</summary>
    public Task EnsureAsync(ReadGroup groups, CancellationToken ct = default) => ReadAsync(groups & ~_loaded, ct);

    private async Task ReadAsync(ReadGroup groups, CancellationToken ct)
    {
        if (groups == ReadGroup.None) return;
        foreach (var (t, cmd, inq, group) in InitialQueries)
        {
            if ((group & groups) == 0) continue;
            try
            {
                await _conn.RequestAsync(t, [cmd, inq],
                    m => m.Table == t && m.Command == (byte)(cmd + 1), TimeSpan.FromMilliseconds(700), ct);
            }
            catch (TimeoutException) { /* not supported by this model */ }
        }
        _loaded |= groups;
        StateChanged?.Invoke();
    }

    private void OnMessage(MdrMessage m)
    {
        try
        {
            if (Parse(m)) StateChanged?.Invoke();
        }
        catch (IndexOutOfRangeException) { /* malformed or unexpected layout: ignore */ }
        catch (ArgumentOutOfRangeException) { }
    }

    private bool Parse(MdrMessage m)
    {
        var p = m.Payload;
        if (p.Length < 2) return false;
        byte group = (byte)(p[0] & 0xF0), op = (byte)(p[0] & 0x0F), inq = p[1];
        // RET and NTFY share the layout: normalise to the RET op code.
        if (op is Op.NtfyStatus or Op.NtfyParam or Op.NtfyExt) op -= 2;
        var s = State;

        if (m.Table == DataType.DataMdrNo1)
        {
            switch (p[0])
            {
                case 0x05: // CONNECT_RET_DEVICE_INFO
                    if (inq == Inq.DeviceInfoModelName) s.ModelName = Str(p, 2);
                    else if (inq == Inq.DeviceInfoFwVersion) s.FirmwareVersion = Str(p, 2);
                    return true;
                case 0x03: // CONNECT_RET_CAPABILITY_INFO: 03 00 <version> <len> "58:18:..."
                    s.Address = Str(p, 3);
                    return true;
            }
            switch (group, op, inq)
            {
                case (T1.Common, Op.RetStatus, Inq.CommonAudioCodec):
                    s.Codec = (AudioCodec)p[2]; return true;
                case (T1.Common, Op.RetStatus, Inq.CommonUpscalingEffect):
                    s.DseeActive = p.Length > 3 && p[3] == 0x01; return true;

                case (T1.Power, Op.RetStatus, Inq.LrBattery):
                    s.BatteryLeft = new(p[2], (ChargingStatus)p[3]);
                    s.BatteryRight = new(p[4], (ChargingStatus)p[5]);
                    return true;
                case (T1.Power, Op.RetStatus, 0x09): // LR battery with threshold
                    s.BatteryLeft = new(p[2], (ChargingStatus)p[3]);
                    s.BatteryRight = new(p[4], (ChargingStatus)p[5]);
                    return true;
                case (T1.Power, Op.RetStatus, Inq.CradleBattery):
                case (T1.Power, Op.RetStatus, 0x0A):
                    s.BatteryCase = new(p[2], (ChargingStatus)p[3]); return true;
                case (T1.Power, Op.RetCapability, Inq.AutoPowerOffWearing):
                    s.AutoPowerOffOptions = p.Skip(3).Take(p[2]).Select(b => (AutoPowerOff)b).ToList(); return true;
                case (T1.Power, Op.RetParam, Inq.AutoPowerOffWearing):
                    s.AutoPowerOff = (AutoPowerOff)p[2]; return true;

                case (T1.NcAsm, Op.RetParam, Inq.NcAsmNoiseAdaptive):
                    // [chg][on 01/off 00][mode][voice][level][adaptive on 01][sensitivity]
                    s.NoiseControl = new(p[3] == 0x01, (NcAsmMode)p[4], p[5] == 0x01, p[6], p[7] == 0x01,
                        (NoiseAdaptiveSensitivity)p[8]);
                    return true;
                case (T1.NcAsm, Op.RetParam, Inq.NcAmbToggle):
                    s.NcAmbKeyCycle = (NcAmbCycle)p[2]; return true;

                case (T1.EqEbb, Op.RetCapability, Inq.PresetEqAndErrorCode or Inq.PresetEq):
                {
                    // 51 04 <bands> <steps> <presetCount> (<presetId> <nameLen> <name>)*
                    int bands = p[2], steps = p[3], count = p[4], i = 5;
                    var presets = new List<EqPreset>();
                    for (int n = 0; n < count && i + 1 < p.Length; n++)
                    {
                        presets.Add((EqPreset)p[i]);
                        i += 2 + p[i + 1];
                    }
                    s.EqualizerCapability = new(bands, steps, presets, s.EqualizerCapability?.FrequenciesHz ?? []);
                    return true;
                }
                case (T1.EqEbb, Op.RetExt, Inq.PresetEqAndErrorCode or Inq.PresetEq):
                {
                    // 5B 04 <bands> (01 <freq u16 BE>)*
                    var freqs = new List<int>();
                    for (int i = 3; i + 2 < p.Length; i += 3) freqs.Add((p[i + 1] << 8) | p[i + 2]);
                    s.EqualizerCapability = (s.EqualizerCapability ?? new(p[2], 13, [], [])) with { FrequenciesHz = freqs };
                    return true;
                }
                case (T1.EqEbb, Op.RetParam, Inq.PresetEqAndErrorCode or Inq.PresetEq):
                    s.Equalizer = new((EqPreset)p[2], p.Skip(4).Take(p[3]).Select(b => (int)b).ToArray()); return true;

                case (T1.Audio, Op.RetParam, Inq.Upscaling):
                    s.Dsee = p[2] == 0x01; return true; // 00 off, 01 auto
                case (T1.Audio, Op.RetParam, Inq.ConnectionModeLdac):
                    s.ConnectionPriority = (PriorMode)p[2]; return true;
                case (T1.Audio, Op.RetParam, Inq.BgmModeAndError):
                    s.ListeningModeBgm = OnOff.Is(p[2]); s.BgmRoomSize = (RoomSize)p[3]; return true;

                case (T1.System, Op.RetParam, Inq.SpeakToChat):
                    s.SpeakToChat = new(OnOff.Is(p[2]), s.SpeakToChat?.Sensitivity ?? DetectSensitivity.Auto,
                        s.SpeakToChat?.ModeOutTime ?? ModeOutTime.Standard);
                    return true;
                case (T1.System, Op.RetExt, Inq.SpeakToChat):
                    s.SpeakToChat = new(s.SpeakToChat?.Enabled ?? false, (DetectSensitivity)p[2], (ModeOutTime)p[3]);
                    return true;
                case (T1.System, Op.RetCapability, Inq.AssignableSettings):
                    ParseTouchCapability(p); return true;
                case (T1.System, Op.RetExt, Inq.AssignableSettings):
                {
                    // FB 03 <count> (<preset> <n> (<action> <function>)×n)×count
                    var list = new List<TouchCustomization>();
                    int i = 3;
                    for (int c = 0; c < p[2]; c++)
                    {
                        var preset = (TouchPreset)p[i];
                        int n = p[i + 1];
                        var bindings = Enumerable.Range(0, n)
                            .Select(x => new GestureBinding((TouchAction)p[i + 2 + x * 2], (TouchFunction)p[i + 3 + x * 2])).ToList();
                        list.Add(new(preset, bindings));
                        i += 2 + n * 2;
                    }
                    s.TouchCustomizations = list;
                    return true;
                }
                case (T1.System, Op.RetParam, Inq.AssignableSettings):
                    if (p[2] >= 1) s.TouchLeft = (TouchPreset)p[3];
                    if (p[2] >= 2) s.TouchRight = (TouchPreset)p[4];
                    return true;
                case (T1.System, Op.RetParam, Inq.QuickAccess):
                    s.QuickAccess = p.Skip(3).Take(p[2]).ToList(); return true;
                case (T1.System, Op.RetParam, Inq.HeadGesture):
                    s.HeadGestures = OnOff.Is(p[2]); return true;
                case (T1.System, Op.RetParam, Inq.VoiceAssistant):
                    s.VoiceAssistant = (VoiceAssistant)p[2]; return true;
                case (T1.System, Op.RetCapability, Inq.VoiceAssistant):
                    // F1 04 <keyType> <n> <assistant>×n
                    s.VoiceAssistantOptions = p.Skip(4).Take(p[3]).Select(b => (VoiceAssistant)b).ToList(); return true;

                // Earpiece fitting test: progress (F9 06) and per-side result (FB/FD 06).
                case (T1.System, Op.RetParam, Inq.WearingStatusDetector) when p[0] == (T1.System | Op.NtfyParam):
                    s.FitTest = new((FitTestStatus)p[2], (FitTestError)p[3], s.FitTest?.LeftGood, s.FitTest?.RightGood);
                    return true;
                case (T1.System, Op.RetExt, Inq.WearingStatusDetector) when p[0] == (T1.System | Op.NtfyExt):
                    s.FitTest = new(s.FitTest?.Status ?? FitTestStatus.Completed, s.FitTest?.Error ?? FitTestError.None,
                        p[2] == 0x00, p[3] == 0x00);
                    return true;

                case (T1.GeneralSetting, Op.RetParam, Inq.GsMultipoint):
                    s.Multipoint = OnOff.Is(p[3]); return true;
                case (T1.GeneralSetting, Op.RetParam, Inq.GsSidetone):
                    s.Sidetone = OnOff.Is(p[3]); return true;
                case (T1.GeneralSetting, Op.RetParam, Inq.GsOneSideNc):
                    s.OneSideNoiseControl = OnOff.Is(p[3]); return true;

                case (T1.Play, Op.RetCapability, Inq.PlaybackController):
                    s.MusicVolumeMax = p[2] - 1; return true;
                case (T1.Play, Op.RetParam, Inq.PlaybackController):
                    s.NowPlaying = ParseNowPlaying(p); return true;
                case (T1.Play, Op.RetParam, Inq.MusicVolume):
                case (T1.Play, Op.RetParam, 0x30):
                    s.MusicVolume = p[2]; return true;
            }
        }
        else if (m.Table == DataType.DataMdrNo2)
        {
            switch (group, op, inq)
            {
                case (T2.Power, Op.RetCapability, Inq.CaringCharge):
                    s.CaringChargeThreshold = p[2]; return true;
                case (T2.Power, Op.RetParam, Inq.CaringCharge):
                    s.CaringCharge = OnOff.Is(p[2]); return true;
                case (T2.VoiceGuidance, Op.RetParam, Inq.VoiceGuidanceOnOff):
                    s.VoiceGuidance = OnOff.Is(p[2]); return true;
                case (T2.VoiceGuidance, Op.RetParam, Inq.VoiceGuidanceVolume):
                    s.VoiceGuidanceVolume = (sbyte)p[2]; return true;
                case (T2.Peripheral, Op.RetParam, Inq.PairedDevices or 0x00):
                    s.PairedDevices = ParsePairedDevices(p); return true;
                case (T2.Peripheral, Op.RetExt, Inq.PairedDevices or 0x00) when p.Length >= 21:
                {
                    // 3D 02 <action> <PeripheralResult> <MAC 17>; result low nibble: 0 success, 1 error, 2 in progress, 3 busy
                    byte result = p[3];
                    s.LastDeviceAction = new((DeviceAction)p[2], Encoding.ASCII.GetString(p, 4, 17),
                        (result & 0x0F) == 0x00, (result & 0x0F) == 0x02);
                    if ((result & 0x0F) != 0x02)
                        _ = _conn.SendAsync(DataType.DataMdrNo2, [T2.Peripheral | Op.GetParam, Inq.PairedDevices]);
                    return true;
                }
            }
        }
        return false;
    }

    private static string Str(byte[] p, int lenIndex) =>
        Encoding.UTF8.GetString(p, lenIndex + 1, Math.Min(p[lenIndex], p.Length - lenIndex - 1));

    private void ParseTouchCapability(byte[] p)
    {
        // F1 03 <keys> per key: <key><type><default><n> then n × (<preset><a><b> a×(act,fn) b×(act,fn,k,fn×k))
        // a-items are fixed (action, function); b-items are editable (action, default, k, choices×k).
        int keys = p[2], i = 3;
        var perKey = new List<TouchPreset>[keys];
        var gestures = new List<GestureCapability>();
        for (int k = 0; k < keys; k++)
        {
            int n = p[i + 3];
            i += 4;
            perKey[k] = new();
            for (int j = 0; j < n; j++)
            {
                var preset = (TouchPreset)p[i];
                perKey[k].Add(preset);
                int a = p[i + 1], b = p[i + 2];
                i += 3 + a * 2;
                for (int x = 0; x < b; x++)
                {
                    int count = p[i + 2];
                    if (gestures.All(g => g.Preset != preset || g.Action != (TouchAction)p[i]))
                        gestures.Add(new(preset, (TouchAction)p[i], (TouchFunction)p[i + 1],
                            p.Skip(i + 3).Take(count).Select(f => (TouchFunction)f).ToList()));
                    i += 3 + count;
                }
            }
        }
        if (keys > 0) State.TouchOptionsLeft = perKey[0];
        if (keys > 1) State.TouchOptionsRight = perKey[1];
        State.GestureCapabilities = gestures;
    }

    private static NowPlaying ParseNowPlaying(byte[] p)
    {
        // A7 01 (<nameStatus> <len> <utf8>)×3 <playbackStatus> <musicCall>
        int i = 2;
        string Next()
        {
            if (i + 1 >= p.Length) return "";
            byte status = p[i], len = p[i + 1];
            var text = status == 0x02 ? Encoding.UTF8.GetString(p, i + 2, Math.Min(len, p.Length - i - 2)) : "";
            i += 2 + len;
            return text;
        }
        var title = Next(); var album = Next(); var artist = Next();
        var status = i < p.Length ? (PlaybackStatus)p[i] : PlaybackStatus.Unsettled;
        return new NowPlaying(title, album, artist, status);
    }

    private static List<PairedDevice> ParsePairedDevices(byte[] p)
    {
        // 37 02 <count> (<"AA:BB:.." 17 bytes> <connection> <class of device ×3> <nameLen> <name>)*
        var list = new List<PairedDevice>();
        int count = p[2], i = 3;
        for (int n = 0; n < count && i + 22 <= p.Length; n++)
        {
            var mac = Encoding.ASCII.GetString(p, i, 17);
            byte conn = p[i + 17];
            int nameLen = p[i + 21];
            var name = Encoding.UTF8.GetString(p, i + 22, Math.Min(nameLen, p.Length - i - 22));
            list.Add(new PairedDevice(mac, name, conn != 0x00, conn == 0x02));
            i += 22 + nameLen;
        }
        return list;
    }

    // ---------------------------------------------------------------- writing

    private Task Set1(params byte[] payload) => _conn.SendAsync(DataType.DataMdrNo1, payload);
    private Task Set2(params byte[] payload) => _conn.SendAsync(DataType.DataMdrNo2, payload);

    public Task SetNoiseControlAsync(NoiseControlState nc) =>
        Set1(T1.NcAsm | Op.SetParam, Inq.NcAsmNoiseAdaptive, 0x01, (byte)(nc.Enabled ? 1 : 0), (byte)nc.Mode,
            (byte)(nc.VoiceFocus ? 1 : 0), (byte)Math.Clamp(nc.AmbientLevel, 0, 20), (byte)(nc.NoiseAdaptive ? 1 : 0),
            (byte)nc.Sensitivity);

    public Task SetNcAmbKeyCycleAsync(NcAmbCycle cycle) => Set1(T1.NcAsm | Op.SetParam, Inq.NcAmbToggle, (byte)cycle);

    public Task SetEqualizerAsync(EqPreset preset, int[] bands) =>
        Set1([T1.EqEbb | Op.SetParam, Inq.PresetEqAndErrorCode, (byte)preset, (byte)bands.Length,
              .. bands.Select(b => (byte)b)]);

    /// <summary>Selects a preset; the headphones answer with that preset's band values.</summary>
    public Task SetEqualizerPresetAsync(EqPreset preset) =>
        Set1(T1.EqEbb | Op.SetParam, Inq.PresetEqAndErrorCode, (byte)preset, 0x00);

    public Task SetDseeAsync(bool on) => Set1(T1.Audio | Op.SetParam, Inq.Upscaling, (byte)(on ? 1 : 0));
    public Task SetConnectionPriorityAsync(PriorMode mode) => Set1(T1.Audio | Op.SetParam, Inq.ConnectionModeLdac, (byte)mode);
    public Task SetListeningModeAsync(bool bgm, RoomSize size) =>
        Set1(T1.Audio | Op.SetParam, Inq.BgmModeAndError, OnOff.From(bgm), (byte)size);

    public Task SetSpeakToChatAsync(bool on) => Set1(T1.System | Op.SetParam, Inq.SpeakToChat, OnOff.From(on), OnOff.From(on));
    public Task SetSpeakToChatOptionsAsync(DetectSensitivity sensitivity, ModeOutTime time) =>
        Set1(T1.System | Op.SetExt, Inq.SpeakToChat, (byte)sensitivity, (byte)time);

    public Task SetTouchPresetsAsync(TouchPreset left, TouchPreset right) =>
        Set1(T1.System | Op.SetParam, Inq.AssignableSettings, 0x02, (byte)left, (byte)right);

    /// <summary>Rebinds one gesture of one preset, keeping every other stored binding.</summary>
    public Task SetGestureAsync(TouchPreset preset, TouchAction action, TouchFunction function)
    {
        var current = State.TouchCustomizations ?? throw new InvalidOperationException(Resonance.Core.Localization.Loc.T("err.touchnotread"));
        var updated = current.Select(c => c.Preset != preset ? c : c with
        {
            Bindings = c.Bindings.Select(b => b.Action == action ? b with { Function = function } : b).ToList(),
        });
        var payload = new List<byte> { T1.System | Op.SetExt, Inq.AssignableSettings, (byte)current.Count };
        foreach (var c in updated)
        {
            payload.Add((byte)c.Preset);
            payload.Add((byte)c.Bindings.Count);
            foreach (var b in c.Bindings) { payload.Add((byte)b.Action); payload.Add((byte)b.Function); }
        }
        return Set1([.. payload]);
    }

    /// <summary>Binds quick access 1 / 2 (F8 0D &lt;n&gt; &lt;function&gt;×n).</summary>
    public Task SetQuickAccessAsync(byte first, byte second) =>
        Set1(T1.System | Op.SetParam, Inq.QuickAccess, 0x02, first, second);

    public Task SetHeadGesturesAsync(bool on) => Set1(T1.System | Op.SetParam, Inq.HeadGesture, OnOff.From(on));
    public Task SetVoiceAssistantAsync(VoiceAssistant va) => Set1(T1.System | Op.SetParam, Inq.VoiceAssistant, (byte)va);

    public Task SetAutoPowerOffAsync(AutoPowerOff value) =>
        Set1(T1.Power | Op.SetParam, Inq.AutoPowerOffWearing, (byte)value, 0x00);

    public Task SetMultipointAsync(bool on) => Set1(T1.GeneralSetting | Op.SetParam, Inq.GsMultipoint, 0x00, OnOff.From(on));
    public Task SetSidetoneAsync(bool on) => Set1(T1.GeneralSetting | Op.SetParam, Inq.GsSidetone, 0x00, OnOff.From(on));
    public Task SetOneSideNoiseControlAsync(bool on) => Set1(T1.GeneralSetting | Op.SetParam, Inq.GsOneSideNc, 0x00, OnOff.From(on));

    public Task SetMusicVolumeAsync(int volume) => Set1(T1.Play | Op.SetParam, Inq.MusicVolume, (byte)volume);
    public Task SendPlaybackAsync(PlaybackControl control) =>
        Set1(T1.Play | Op.SetStatus, Inq.PlaybackController, 0x00, (byte)control);

    public Task SetCaringChargeAsync(bool on) => Set2(T2.Power | Op.SetParam, Inq.CaringCharge, OnOff.From(on));
    public Task SetVoiceGuidanceAsync(bool on) => Set2(T2.VoiceGuidance | Op.SetParam, Inq.VoiceGuidanceOnOff, OnOff.From(on));
    public Task SetVoiceGuidanceVolumeAsync(int volume) =>
        Set2(T2.VoiceGuidance | Op.SetParam, Inq.VoiceGuidanceVolume, unchecked((byte)(sbyte)volume), OnOff.Off);

    // ---------------------------------------------------------------- paired devices (T2 peripheral)

    /// <summary>Connects, disconnects or forgets a device remembered by the headphones.</summary>
    public Task SendDeviceActionAsync(DeviceAction action, string address) =>
        Set2([T2.Peripheral | Op.SetExt, Inq.PairedDevices, (byte)action, .. Encoding.ASCII.GetBytes(address.ToUpperInvariant())]);

    /// <summary>Makes <paramref name="address"/> the device whose audio is played.</summary>
    public Task SwitchAudioSourceAsync(string address) =>
        Set2([T2.Peripheral | Op.SetExt, Inq.SourceSwitch, .. Encoding.ASCII.GetBytes(address.ToUpperInvariant())]);

    /// <summary>Puts the headphones in pairing mode to add a new device.</summary>
    public Task EnterPairingModeAsync() =>
        Set2(T2.Peripheral | Op.SetStatus, Inq.PairedDevices, 0x01 /* inquiry scan */, 0x00 /* enable */);

    // ---------------------------------------------------------------- earpiece fitting test

    /// <summary>Enters test mode and starts measuring; results arrive in <see cref="HeadphoneState.FitTest"/>.</summary>
    public async Task StartFitTestAsync()
    {
        State.FitTest = new(FitTestStatus.Started, FitTestError.None, null, null);
        await Set1(T1.System | Op.SetStatus, Inq.WearingStatusDetector, 0x01 /* mode in */, 0x00);
        await Set1(T1.System | Op.SetParam, Inq.WearingStatusDetector, 0x00 /* start */, 0x00, 0xFF, 0xFF);
    }

    public async Task EndFitTestAsync()
    {
        await Set1(T1.System | Op.SetParam, Inq.WearingStatusDetector, 0x01 /* cancel */, 0x00, 0xFF, 0xFF);
        await Set1(T1.System | Op.SetStatus, Inq.WearingStatusDetector, 0x00 /* mode out */, 0x00);
    }

    /// <summary>Turns the headphones off (POWER_SET_STATUS / POWER_OFF / USER_POWER_OFF).</summary>
    public Task PowerOffAsync() => Set1(T1.Power | Op.SetStatus, Inq.PowerOff, 0x01);

    public Task SendRawAsync(bool table2, byte[] payload) =>
        _conn.SendAsync(table2 ? DataType.DataMdrNo2 : DataType.DataMdrNo1, payload);

    public ValueTask DisposeAsync() => _conn.DisposeAsync();
}
