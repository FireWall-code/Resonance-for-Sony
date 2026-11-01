using Resonance.Core.Protocol;

namespace Resonance.Core.Device;

public sealed record BatteryInfo(int Level, ChargingStatus Status)
{
    public bool IsCharging => Status == ChargingStatus.Charging;
}

public sealed record NoiseControlState(
    bool Enabled, NcAsmMode Mode, bool VoiceFocus, int AmbientLevel, bool NoiseAdaptive, NoiseAdaptiveSensitivity Sensitivity);

public sealed record EqualizerState(EqPreset Preset, int[] Bands);

public sealed record EqualizerCapability(int BandCount, int Steps, IReadOnlyList<EqPreset> Presets, IReadOnlyList<int> FrequenciesHz);

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

/// <summary>Everything the app knows about the headphones. Null = not reported by the device.</summary>
public sealed class HeadphoneState
{
    public string? ModelName { get; internal set; }
    public string? FirmwareVersion { get; internal set; }
    public string? Address { get; internal set; }
    public AudioCodec? Codec { get; internal set; }
    public bool? DseeActive { get; internal set; }

    public BatteryInfo? BatteryLeft { get; internal set; }
    public BatteryInfo? BatteryRight { get; internal set; }
    public BatteryInfo? BatteryCase { get; internal set; }

    public NoiseControlState? NoiseControl { get; internal set; }
    public NcAmbCycle? NcAmbKeyCycle { get; internal set; }

    public EqualizerCapability? EqualizerCapability { get; internal set; }
    public EqualizerState? Equalizer { get; internal set; }

    public bool? Dsee { get; internal set; }
    public PriorMode? ConnectionPriority { get; internal set; }
    public bool? ListeningModeBgm { get; internal set; }
    public RoomSize? BgmRoomSize { get; internal set; }

    public SpeakToChatState? SpeakToChat { get; internal set; }
    public AutoPowerOff? AutoPowerOff { get; internal set; }
    public IReadOnlyList<AutoPowerOff>? AutoPowerOffOptions { get; internal set; }
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
}
