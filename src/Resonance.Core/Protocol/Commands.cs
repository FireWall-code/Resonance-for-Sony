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

/// <summary>
/// Function types a model declares in CONNECT_RET_SUPPORT_FUNCTION (table 1). Each model lists the
/// variant it implements, which decides the inquired type and payload layout to use.
/// </summary>
public static class F1
{
    public const byte CodecIndicator = 0x12, UpscalingIndicator = 0x13,
        Battery = 0x20, LrBattery = 0x21, CradleBattery = 0x22, PowerOff = 0x23, AutoPowerOff = 0x24,
        AutoPowerOffWearing = 0x25, BatteryThreshold = 0x28, LrBatteryThreshold = 0x29, CradleBatteryThreshold = 0x2A,
        PresetEq = 0x50, PresetEqNonCustomizable = 0x52, PresetEqAndErrorCode = 0x57,
        NcOnOffAsmSeamless = 0x64, NcDualSingleAsmSeamless = 0x65, AsmSeamless = 0x67, NcAutoAsmSeamless = 0x68,
        AmbientControlModeSelect = 0x69, ModeNcDualSingleAsm = 0x6A, ModeNcDualAsm = 0x6B, ModeNcNcssAsm = 0x6C,
        ModeNcDualAsmNoiseAdaptive = 0x6D,
        PlaybackFirst = 0xA1, PlaybackLast = 0xA4,
        GeneralSetting1 = 0xD1, GeneralSetting4 = 0xD4,
        ConnectionMode = 0xE1, UpscalingAutoOff = 0xE2, ConnectionModeLdac = 0xE3, BgmMode = 0xE4, ConnectionModeClassicLe = 0xE7,
        VoiceContents = 0xE8, SoundLeakageReduction = 0xE9, BgmModeAndError = 0xEB, UpscalingWithReason = 0xED,
        PlaybackByWearing = 0xF1, AssignableSetting = 0xF3, VoiceAssistant = 0xF4, WearingStatusDetector = 0xF6,
        SpeakToChat = 0xFC, QuickAccess = 0xFD, HeadGesture = 0xFF;
}

/// <summary>Function types of table 2 (CONNECT_RET_SUPPORT_FUNCTION on DATA_MDR_NO2).</summary>
public static class F2
{
    public const byte CaringCharge = 0x22,
        PairingWithClassOfDeviceBt = 0x32, PairingWithClassOfDeviceLe = 0x33, SourceSwitch = 0x31,
        VoiceGuidanceFirst = 0x40, VoiceGuidanceVolume = 0x42, VoiceGuidanceVolume5Steps = 0x43, VoiceGuidanceLast = 0x45,
        SafeListeningHbs1 = 0x50, SafeListeningTws2 = 0x53;
}

/// <summary>
/// MDR protocol v1 (WH-1000XM3/XM4, WF-1000XM3/XM4…): function types, the common group (battery, codec,
/// upscaling, power off use fixed command codes) and inquired types that differ from v2.
/// </summary>
public static class V1
{
    // Function types (CONNECT_RET_SUPPORT_FUNCTION lists one byte per function)
    public const byte FnBattery = 0x11, FnUpscalingIndicator = 0x12, FnCodec = 0x13, FnLrBattery = 0x15,
        FnCradleBattery = 0x18, FnPowerOff = 0x21, FnPresetEq = 0x51, FnNc = 0x61, FnNcAndAmbient = 0x62,
        FnAmbient = 0x63, FnNcOptimizer = 0x81, FnPlayback = 0xA1, FnGeneralSetting3 = 0xD3,
        FnConnectionMode = 0xE1, FnUpscaling = 0xE2, FnControlByWearing = 0xF3, FnAutoPowerOff = 0xF4,
        FnSmartTalking = 0xF5;
    // Common group
    public const byte GetBattery = 0x10, RetBattery = 0x11, NtfyBattery = 0x13,
        GetUpscalingEffect = 0x14, RetUpscalingEffect = 0x15, NtfyUpscalingEffect = 0x17,
        GetAudioCodec = 0x18, RetAudioCodec = 0x19, NtfyAudioCodec = 0x1B, SetPowerOff = 0x22;
    public const byte BatterySingle = 0x00, BatteryLr = 0x01, BatteryCradle = 0x02;
    // Inquired types
    public const byte NcAsmNc = 0x01, NcAsmNcAndAmbient = 0x02, NcAsmAmbient = 0x03, NcDual = 0x02;
    public const byte PresetEq = 0x01;
    public const byte AudioConnectionMode = 0x01, AudioUpscaling = 0x02;
    public const byte SystemControlByWearing = 0x03, SystemAutoPowerOff = 0x04, SystemSmartTalking = 0x05;
    public const byte PlayVolume = 0x20;
    // General settings: 01 switch, 02 list
    public const byte GsSwitch = 0x01, GsList = 0x02;
}

/// <summary>Body colour reported in CONNECT_RET_DEVICE_INFO (series and colour); "-I" variants map to the base colour.</summary>
public enum ModelColor : byte
{
    Default = 0, Black = 1, White = 2, Silver = 3, Red = 4, Blue = 5, Pink = 6, Yellow = 7, Green = 8, Gray = 9,
    Gold = 10, Cream = 11, Orange = 12, Brown = 13, Violet = 14,
}

/// <summary>"Inquired type" values (byte 1 of a message).</summary>
public static class Inq
{
    // T1 connect
    public const byte SupportFunction = 0x00;
    // T1 common
    public const byte CommonConnectionStatus = 0x01, CommonAudioCodec = 0x02, CommonUpscalingEffect = 0x03;
    // T1 connect device info
    public const byte DeviceInfoModelName = 0x01, DeviceInfoFwVersion = 0x02, DeviceInfoSeriesColor = 0x03;
    // T1 power
    public const byte Battery = 0x00, LrBattery = 0x01, CradleBattery = 0x02, PowerOff = 0x03,
        AutoPowerOff = 0x04, AutoPowerOffWearing = 0x05, BatteryThreshold = 0x08, LrBatteryThreshold = 0x09,
        CradleBatteryThreshold = 0x0A, BatterySafeMode = 0x0B;
    // T1 eq
    public const byte PresetEq = 0x00, PresetEqNonCustomizable = 0x02, PresetEqAndErrorCode = 0x04;
    // T1 ncasm: one variant per model family (see NoiseVariant)
    public const byte NcOnOffAsmSeamless = 0x13, NcModeSwitchAsmSeamless = 0x14, NcAutoAsmSeamless = 0x15,
        ModeNcDualSingleAsm = 0x16, ModeNcDualAsm = 0x17, ModeNcNcssAsm = 0x18, NcAsmNoiseAdaptive = 0x19,
        AsmSeamless = 0x22, NcAmbToggle = 0x30;
    // T1 sense
    public const byte AdaptiveControl = 0x01;
    // T1 play
    public const byte PlaybackController = 0x01, MusicVolume = 0x20, CallVolume = 0x21;
    // T1 sar / auto play
    public const byte AutoPlay = 0x01;
    // T1 general settings (self-describing slots D1-D4; meaning depends on the model)
    public const byte GsSidetone = 0xD1, GsMultipoint = 0xD2, GsOneSideNc = 0xD4;
    // T1 audio
    public const byte ConnectionMode = 0x00, Upscaling = 0x01, ConnectionModeLdac = 0x02, BgmMode = 0x03,
        ConnectionModeClassicLe = 0x05, VoiceContents = 0x06, SoundLeakageReduction = 0x07, BgmModeAndError = 0x09;
    // T1 system
    public const byte PlaybackByWearing = 0x01, AssignableSettings = 0x03, VoiceAssistant = 0x04, WearingStatusDetector = 0x06,
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
