using Resonance.Core.Protocol;

namespace Resonance.Core.Device;

public sealed record BatteryInfo(int Level, ChargingStatus Status)
{
    public bool IsCharging => Status == ChargingStatus.Charging;
}

public sealed record NoiseControlState(
    bool Enabled, NcAsmMode Mode, bool VoiceFocus, int AmbientLevel, bool NoiseAdaptive, NoiseAdaptiveSensitivity Sensitivity);

public sealed record EqualizerState(EqPreset Preset, int[] Bands);

public sealed record EqualizerCapability(int BandCount, int Steps, IReadOnlyList<EqPreset> Presets, IReadOnlyList<int> FrequenciesHz)
{
    /// <summary>Step value of a flat band (0 dB).</summary>
    public int Center => (Steps - 1) / 2;

    /// <summary>Clear Bass + 5 bands (WH-1000XM4/XM5, WF-1000XM5…) when the first band has no frequency.</summary>
    public bool HasClearBass => BandCount == 6;

    /// <summary>
    /// Layout of models that do not describe their equalizer: 10 bands of ±6 dB, or Clear Bass and
    /// 5 bands of ±10 steps (frequency 0 marks Clear Bass).
    /// </summary>
    public static EqualizerCapability Default(int bands) => bands switch
    {
        6 => new(6, 21, StandardPresets, [0, 400, 1000, 2500, 6300, 16000]),
        5 => new(5, 21, StandardPresets, [400, 1000, 2500, 6300, 16000]),
        _ => new(bands, 13, StandardPresets, bands == 10 ? [31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000] : []),
    };

    private static readonly EqPreset[] StandardPresets =
    [
        EqPreset.Off, EqPreset.Bright, EqPreset.Excited, EqPreset.Mellow, EqPreset.Relaxed, EqPreset.Vocal,
        EqPreset.Treble, EqPreset.Bass, EqPreset.Speech, EqPreset.Custom, EqPreset.User1, EqPreset.User2,
    ];
}

/// <summary>Names of the self-described general settings Resonance has dedicated controls for.</summary>
public static class GeneralSettingNames
{
    public const string Sidetone = "SIDETONE_SETTING", Multipoint = "MULTIPOINT_SETTING",
        OneSideNoiseControl = "TWS_ONE_SIDE_USE_NCASM_SETTING";

    /// <summary>Slot meaning on the WF-1000XM6, for headphones that do not describe their settings.</summary>
    public static string Legacy(byte slot) => slot switch
    {
        Inq.GsSidetone => Sidetone,
        Inq.GsMultipoint => Multipoint,
        Inq.GsOneSideNc => OneSideNoiseControl,
        _ => $"SETTING_{slot:X2}",
    };
}

public sealed record SpeakToChatState(bool Enabled, DetectSensitivity Sensitivity, ModeOutTime ModeOutTime);

public sealed record NowPlaying(string Title, string Album, string Artist, PlaybackStatus Status);

public sealed record GestureBinding(TouchAction Action, TouchFunction Function);

/// <summary>Gesture bindings of one touch preset, as stored in the headphones.</summary>
public sealed record TouchCustomization(TouchPreset Preset, IReadOnlyList<GestureBinding> Bindings);

/// <summary>A gesture of a preset and the functions the headphones allow for it.</summary>
public sealed record GestureCapability(TouchPreset Preset, TouchAction Action, TouchFunction Default, IReadOnlyList<TouchFunction> Choices);

public enum FitTestStatus : byte { NotStarted = 0x00, Started = 0x01, Completed = 0x02, Failed = 0x03 }

public enum FitTestError : byte
{
    None = 0x00, LeftConnection = 0x01, RightConnection = 0x02, Unavailable = 0x03,
    LeftFitting = 0x04, RightFitting = 0x05, BothFitting = 0x06, Measuring = 0x07,
}

public sealed record FitTestState(FitTestStatus Status, FitTestError Error, bool? LeftGood, bool? RightGood);

public enum DeviceAction : byte { Disconnect = 0x00, Connect = 0x01, Forget = 0x02 }

/// <summary>Outcome of a connect / disconnect / forget request (PeripheralResult).</summary>
public sealed record DeviceActionResult(DeviceAction Action, string Address, bool Success, bool InProgress);

public sealed record PairedDevice(string Address, string Name, bool Connected, bool Playing);

/// <summary>
/// A model-specific setting the headphones describe themselves (GENERAL_SETTING D1-D4): its name is an
/// identifier such as "MULTIPOINT_SETTING", and a list setting carries its own choices.
/// </summary>
public sealed record GeneralSetting(
    byte Id, string Name, bool IsList, IReadOnlyList<GeneralSettingOption> Options, byte? Value, byte OnValue = OnOff.On)
{
    /// <summary>On/off value of a switch (v2 uses 0x00 for on, v1 0x01).</summary>
    public bool IsOn => Value == OnValue;
}

public sealed record GeneralSettingOption(byte Value, string Name);

/// <summary>Live sound level measured by the headphones (safe listening), in dB; null while nothing plays.</summary>
/// <summary>Shape of the headphones, for the illustration.</summary>
public enum HeadphoneForm { Earbuds, OverEar }

public sealed record SoundPressure(int? Decibels, SoundPressureReason Reason);

public enum SoundPressureReason : byte { NotPlaying = 0x00, InCall = 0x01, Detached = 0x02, Measured = 0xFE }

/// <summary>
/// Function types the model declares at connection (CONNECT_RET_SUPPORT_FUNCTION). Empty when the
/// headphones did not answer, in which case the WF-1000XM6 command set is assumed.
/// </summary>
public sealed class SupportedFunctions
{
    private readonly HashSet<byte> _t1 = [], _t2 = [];
    public bool Known => _t1.Count > 0;
    public IReadOnlyCollection<byte> Table1 => _t1;
    public IReadOnlyCollection<byte> Table2 => _t2;
    public bool Has(byte f1) => _t1.Contains(f1);
    public bool Has2(byte f2) => _t2.Contains(f2);
    public bool HasAny(byte first, byte last) => _t1.Any(f => f >= first && f <= last);
    public bool HasAny2(byte first, byte last) => _t2.Any(f => f >= first && f <= last);

    /// <summary>Reads "07 00 &lt;n&gt; (&lt;function&gt; &lt;priority&gt;)×n", or one byte per function in v1.</summary>
    internal void Load(bool table2, byte[] p, bool v1 = false)
    {
        var set = table2 ? _t2 : _t1;
        set.Clear();
        int stride = v1 ? 1 : 2;
        for (int n = 0, i = 3; n < p[2] && i < p.Length; n++, i += stride) set.Add(p[i]);
    }
}

/// <summary>Everything the app knows about the headphones. Null = not reported by the device.</summary>
public sealed class HeadphoneState
{
    public string? ModelName { get; internal set; }
    public string? FirmwareVersion { get; internal set; }
    /// <summary>ModelSeries byte (0x30 premium, 0x60 LinkBuds, 0x70 neckband…).</summary>
    public byte? Series { get; internal set; }
    public ModelColor? Color { get; internal set; }

    /// <summary>Earbuds or over-ear, from the model name, else from the batteries it reports.</summary>
    public HeadphoneForm Form => ModelName switch
    {
        { } n when n.StartsWith("WH-") || n.StartsWith("MDR-") || n.StartsWith("ULT WEAR") => HeadphoneForm.OverEar,
        { } n when n.StartsWith("WF-") || n.StartsWith("LinkBuds") || n.StartsWith("WI-") => HeadphoneForm.Earbuds,
        _ => Battery is not null && BatteryLeft is null ? HeadphoneForm.OverEar : HeadphoneForm.Earbuds,
    };
    public string? Address { get; internal set; }
    public AudioCodec? Codec { get; internal set; }
    public bool? DseeActive { get; internal set; }

    public SupportedFunctions Functions { get; } = new();

    /// <summary>MDR protocol version: 2 for current models, 1 for WH-1000XM3/XM4, WF-1000XM3/XM4…</summary>
    public int ProtocolVersion { get; internal set; } = 2;
    public bool IsV1 => ProtocolVersion == 1;

    /// <summary>Single battery of over-ear headphones.</summary>
    public BatteryInfo? Battery { get; internal set; }
    public BatteryInfo? BatteryLeft { get; internal set; }
    public BatteryInfo? BatteryRight { get; internal set; }
    public BatteryInfo? BatteryCase { get; internal set; }

    public NoiseControlState? NoiseControl { get; internal set; }
    public NcAmbCycle? NcAmbKeyCycle { get; internal set; }
    /// <summary>NC/ASM inquired type the model uses (Inq.NcOnOffAsmSeamless…NcAsmNoiseAdaptive, AsmSeamless).</summary>
    public byte? NoiseVariant { get; internal set; }
    public SoundPressure? SoundPressure { get; internal set; }

    public EqualizerCapability? EqualizerCapability { get; internal set; }
    public EqualizerState? Equalizer { get; internal set; }

    public bool? Dsee { get; internal set; }
    public PriorMode? ConnectionPriority { get; internal set; }
    public bool? ListeningModeBgm { get; internal set; }
    public RoomSize? BgmRoomSize { get; internal set; }

    public SpeakToChatState? SpeakToChat { get; internal set; }
    public AutoPowerOff? AutoPowerOff { get; internal set; }
    public IReadOnlyList<AutoPowerOff>? AutoPowerOffOptions { get; internal set; }
    public bool? PauseWhenRemoved { get; internal set; }
    public bool? VoiceBoost { get; internal set; }
    public bool? SoundLeakageReduction { get; internal set; }
    /// <summary>Self-described settings, by slot (D1-D4).</summary>
    public IReadOnlyDictionary<byte, GeneralSetting> GeneralSettings => _generalSettings;
    internal readonly SortedDictionary<byte, GeneralSetting> _generalSettings = new();
    public bool? Multipoint { get; internal set; }
    public bool? Sidetone { get; internal set; }
    public bool? OneSideNoiseControl { get; internal set; }
    public bool? HeadGestures { get; internal set; }
    public bool? CaringCharge { get; internal set; }
    public int? CaringChargeThreshold { get; internal set; }
    public bool? VoiceGuidance { get; internal set; }
    public int? VoiceGuidanceVolume { get; internal set; }
    public VoiceAssistant? VoiceAssistant { get; internal set; }
    public IReadOnlyList<VoiceAssistant>? VoiceAssistantOptions { get; internal set; }
    public FitTestState? FitTest { get; internal set; }
    /// <summary>Functions bound to quick access 1 (double tap) and 2 (triple tap); 0x00 none, 0x01 Spotify Tap.</summary>
    public IReadOnlyList<byte>? QuickAccess { get; internal set; }
    public DeviceActionResult? LastDeviceAction { get; internal set; }

    public TouchPreset? TouchLeft { get; internal set; }
    public TouchPreset? TouchRight { get; internal set; }
    public IReadOnlyList<TouchPreset>? TouchOptionsLeft { get; internal set; }
    public IReadOnlyList<TouchPreset>? TouchOptionsRight { get; internal set; }
    public IReadOnlyList<TouchCustomization>? TouchCustomizations { get; internal set; }
    public IReadOnlyList<GestureCapability>? GestureCapabilities { get; internal set; }

    public int? MusicVolume { get; internal set; }
    public int? MusicVolumeMax { get; internal set; }
    public NowPlaying? NowPlaying { get; internal set; }

    public IReadOnlyList<PairedDevice>? PairedDevices { get; internal set; }
    /// <summary>Playback fixed on the current device (null: not supported).</summary>
    public bool? PlaybackPinned { get; internal set; }
}
