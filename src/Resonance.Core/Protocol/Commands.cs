namespace Resonance.Core.Protocol;

// Byte codes recovered from Sound Connect 13.3.0 (com.sony.songpal.tandemfamily.message.mdr.v2).
// Convention: every group has GET_CAPABILITY = base, RET = +1, GET_STATUS = +2, RET = +3,
// SET_STATUS = +4, NTFY_STATUS = +5, GET_PARAM = +6, RET = +7, SET_PARAM = +8, NTFY_PARAM = +9,
// GET_EXT = +A, RET_EXT = +B, SET_EXT = +C, NTFY_EXT = +D. Byte 1 is the "inquired type".

public static class T1
{
    public const byte ConnectGetProtocolInfo = 0x00, ConnectGetCapabilityInfo = 0x02,
        ConnectGetDeviceInfo = 0x04, ConnectGetSupportFunction = 0x06;
    public const byte Common = 0x10, Power = 0x20, Lea = 0x40, EqEbb = 0x50, NcAsm = 0x60, Sense = 0x70,
        Opt = 0x80, Alert = 0x90, Play = 0xA0, SarAutoPlay = 0xB0, GeneralSetting = 0xD0, Audio = 0xE0, System = 0xF0;
}

public static class T2
{
    public const byte Power = 0x20, Peripheral = 0x30, VoiceGuidance = 0x40, SafeListening = 0x50,
        Lea = 0x60, System = 0xF0;
}

public static class Op
{
    public const byte GetCapability = 0x0, RetCapability = 0x1, GetStatus = 0x2, RetStatus = 0x3,
        SetStatus = 0x4, NtfyStatus = 0x5, GetParam = 0x6, RetParam = 0x7, SetParam = 0x8, NtfyParam = 0x9,
        GetExt = 0xA, RetExt = 0xB, SetExt = 0xC, NtfyExt = 0xD;
}

/// <summary>"Inquired type" values used by the WF-1000XM6 (and siblings).</summary>
public static class Inq
{
    // T1 common
    public const byte CommonConnectionStatus = 0x01, CommonAudioCodec = 0x02, CommonUpscalingEffect = 0x03;
    // T1 connect device info
    public const byte DeviceInfoModelName = 0x01, DeviceInfoFwVersion = 0x02, DeviceInfoSeriesColor = 0x03;
    // T1 power
    public const byte Battery = 0x00, LrBattery = 0x01, CradleBattery = 0x02, PowerOff = 0x03,
        AutoPowerOff = 0x04, AutoPowerOffWearing = 0x05, BatterySafeMode = 0x0B;
    // T1 eq
    public const byte PresetEq = 0x00, PresetEqAndErrorCode = 0x04;
    // T1 ncasm
    public const byte NcAsmNoiseAdaptive = 0x19, NcAmbToggle = 0x30;
    // T1 sense
    public const byte AdaptiveControl = 0x01;
    // T1 play
    public const byte PlaybackController = 0x01, MusicVolume = 0x20, CallVolume = 0x21;
    // T1 sar / auto play
    public const byte AutoPlay = 0x01;
    // T1 general settings
    public const byte GsSidetone = 0xD1, GsMultipoint = 0xD2, GsOneSideNc = 0xD4;
    // T1 audio
    public const byte ConnectionModeLdac = 0x02, Upscaling = 0x01, BgmModeAndError = 0x09, ConnectionModeClassicLe = 0x05;
    // T1 system
    public const byte AssignableSettings = 0x03, VoiceAssistant = 0x04, WearingStatusDetector = 0x06,
        EarpieceSelection = 0x07, SpeakToChat = 0x0C, QuickAccess = 0x0D, HeadGesture = 0x0F;
    // T2 power
    public const byte CaringCharge = 0x01;
    // T2 peripheral
    public const byte PairedDevices = 0x02, SourceSwitch = 0x01, MusicHandOver = 0x03;
    // T2 voice guidance
    public const byte VoiceGuidanceOnOff = 0x01, VoiceGuidanceVolume = 0x20;
}

/// <summary>On/off as encoded by the protocol: 0x00 means ON, 0x01 means OFF.</summary>
public static class OnOff
{
    public const byte On = 0x00, Off = 0x01;
    public static byte From(bool on) => on ? On : Off;
    public static bool Is(byte b) => b == On;
}

public enum NcAsmMode : byte { NoiseCancelling = 0x00, Ambient = 0x01 }
public enum NoiseAdaptiveSensitivity : byte { Standard = 0x00, High = 0x01, Low = 0x02 }
public enum PriorMode : byte { SoundQuality = 0x00, ConnectionQuality = 0x01, LowLatency = 0x02 }
public enum DetectSensitivity : byte { Auto = 0x00, High = 0x01, Low = 0x02 }
public enum ModeOutTime : byte { Short = 0x00, Standard = 0x01, Long = 0x02, Never = 0x03 }
public enum RoomSize : byte { Small = 0x00, Middle = 0x01, Large = 0x02 }
public enum ChargingStatus : byte { NotCharging = 0x00, Charging = 0x01, Unknown = 0x02, Charged = 0x03 }
public enum PlaybackStatus : byte { Unsettled = 0x00, Playing = 0x01, Paused = 0x02, Stopped = 0x03 }
public enum PlaybackControl : byte { KeyOff = 0x00, Pause = 0x01, Next = 0x02, Previous = 0x03, Stop = 0x06, Play = 0x07 }

public enum AutoPowerOff : byte
{
    After5Min = 0x00, After30Min = 0x01, After60Min = 0x02, After180Min = 0x03, After15Min = 0x04,
    WhenRemovedFromEars = 0x10, Disabled = 0x11,
}

public enum AudioCodec : byte { Unsettled = 0x00, Sbc = 0x01, Aac = 0x02, Ldac = 0x10, AptX = 0x20, AptXHd = 0x21, Lc3 = 0x30, Other = 0xFF }

public enum EqPreset : byte
{
    Off = 0x00, Rock = 0x01, Pop = 0x02, Jazz = 0x03, Dance = 0x04, Edm = 0x05, RnbHipHop = 0x06, Acoustic = 0x07,
    Bright = 0x10, Excited = 0x11, Mellow = 0x12, Relaxed = 0x13, Vocal = 0x14, Treble = 0x15, Bass = 0x16, Speech = 0x17,
    Gaming = 0x20, Heavy = 0x30, Clear = 0x31, Hard = 0x32, Soft = 0x33,
    Custom = 0xA0, User1 = 0xA1, User2 = 0xA2, User3 = 0xA3, User4 = 0xA4, User5 = 0xA5,
}

/// <summary>What a touch sensor does (system "Preset").</summary>
public enum TouchPreset : byte
{
    AmbientSoundControl = 0x00, VolumeControl = 0x10, PlaybackControl = 0x20, TrackControl = 0x21,
    VoiceRecognition = 0x30, GoogleAssistant = 0x31, AmazonAlexa = 0x32, AmbientSoundControlQuickAccess = 0x35,
    QuickAccess = 0x36, AmbientSoundControlListeningMode = 0x47, Custom1 = 0x71, Custom2 = 0x72, NoFunction = 0xFF,
}

/// <summary>Modes cycled by the NC/Ambient key (system "Function").</summary>
public enum NcAmbCycle : byte { NcAmbientOff = 0x01, NcAmbient = 0x02, NcOff = 0x03, AmbientOff = 0x04 }

/// <summary>Gesture on a touch sensor (system "Action").</summary>
public enum TouchAction : byte
{
    SingleTap = 0x00, DoubleTap = 0x01, TripleTap = 0x02, RepeatTap = 0x03,
    TapAndHold = 0x10, DoubleTapAndHold = 0x11, LongPressThenActivate = 0x21, LongPressDuringActivate = 0x22,
}

/// <summary>What a gesture does (system "Function").</summary>
public enum TouchFunction : byte
{
    None = 0x00, NcAmbientOff = 0x01, NcAmbient = 0x02, NcOff = 0x03, AmbientOff = 0x04,
    QuickAttention = 0x10, NcOptimizer = 0x11,
    PlayPause = 0x20, NextTrack = 0x21, PreviousTrack = 0x22, VolumeUp = 0x23, VolumeDown = 0x24,
    VoiceRecognition = 0x30, GetNotification = 0x31, GoogleAssistant = 0x32, AmazonAlexa = 0x37,
    NextTrackStopGemini = 0x3A, PreviousTrackStopGemini = 0x3B,
    SpotifyTap = 0x42, QuickAccess1 = 0x43, QuickAccess2 = 0x44, MicMute = 0x70,
}

public enum VoiceAssistant : byte
{
    VoiceRecognition = 0x30, GoogleAssistant = 0x31, AmazonAlexa = 0x32, SonyVoiceAssistant = 0x34,
    EnabledInOtherDevice = 0x3F, None = 0xFF,
}

public enum VoiceGuidanceLanguage : byte
{
    Undefined = 0x00, English = 0x01, French = 0x02, German = 0x03, Spanish = 0x04, Italian = 0x05, Portuguese = 0x06,
    Dutch = 0x07, Swedish = 0x08, Finnish = 0x09, Russian = 0x0A, Japanese = 0x0B, BrazilianPortuguese = 0x0D,
    Korean = 0x0F, Turkish = 0x10, Chinese = 0xF0,
}
