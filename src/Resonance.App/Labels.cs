using Resonance.Core.Device;
using Resonance.Core.Protocol;
using static Resonance.Core.Localization.Loc;

namespace Resonance.App;

public sealed record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Display names for every protocol value shown in the UI (see Localization/*.json).</summary>
public static class Labels
{
    public static string Of(EqPreset p) => p switch
    {
        EqPreset.Off => T("eq.off"), EqPreset.Heavy => T("eq.heavy"), EqPreset.Clear => T("eq.clear"), EqPreset.Hard => T("eq.hard"),
        EqPreset.Soft => T("eq.soft"), EqPreset.Gaming => T("eq.gaming"), EqPreset.Custom => T("eq.custom"),
        EqPreset.User1 => T("eq.user1"), EqPreset.User2 => T("eq.user2"), EqPreset.User3 => T("eq.user3"),
        EqPreset.Bright => T("eq.bright"), EqPreset.Excited => T("eq.excited"), EqPreset.Mellow => T("eq.mellow"),
        EqPreset.Relaxed => T("eq.relaxed"), EqPreset.Vocal => T("eq.vocal"), EqPreset.Treble => T("eq.treble"),
        EqPreset.Bass => T("eq.bass"), EqPreset.Speech => T("eq.speech"), EqPreset.Rock => T("eq.rock"), EqPreset.Pop => T("eq.pop"),
        EqPreset.Jazz => T("eq.jazz"), EqPreset.Dance => T("eq.dance"), EqPreset.Edm => T("eq.edm"),
        EqPreset.RnbHipHop => T("eq.rnb"), EqPreset.Acoustic => T("eq.acoustic"),
        _ => p.ToString(),
    };

    public static string Of(TouchPreset p) => p switch
    {
        TouchPreset.AmbientSoundControlQuickAccess or TouchPreset.AmbientSoundControl => T("tp.ambient"),
        TouchPreset.PlaybackControl => T("tp.playback"),
        TouchPreset.VolumeControl => T("tp.volume"),
        TouchPreset.TrackControl => T("tp.track"),
        TouchPreset.Custom1 or TouchPreset.Custom2 => T("tp.custom"),
        TouchPreset.GoogleAssistant => T("tp.google"),
        TouchPreset.AmazonAlexa => T("tp.alexa"),
        TouchPreset.VoiceRecognition => T("tp.assistant"),
        TouchPreset.QuickAccess => T("tp.quickaccess"),
        TouchPreset.NoFunction => T("tp.none"),
        _ => p.ToString(),
    };

    public static string Of(TouchAction a) => a switch
    {
        TouchAction.SingleTap => T("ta.single"),
        TouchAction.DoubleTap => T("ta.double"),
        TouchAction.TripleTap => T("ta.triple"),
        TouchAction.RepeatTap => T("ta.repeat"),
        TouchAction.TapAndHold => T("ta.hold"),
        TouchAction.DoubleTapAndHold => T("ta.doublehold"),
        TouchAction.LongPressThenActivate or TouchAction.LongPressDuringActivate => T("ta.long"),
        _ => a.ToString(),
    };

    public static string Of(TouchFunction f) => f switch
    {
        TouchFunction.None => T("tf.none"),
        TouchFunction.NcAmbientOff => T("tf.ncambientoff"),
        TouchFunction.NcAmbient => T("tf.ncambient"),
        TouchFunction.NcOff => T("tf.ncoff"),
        TouchFunction.AmbientOff => T("tf.ambientoff"),
        TouchFunction.QuickAttention => T("tf.quickattention"),
        TouchFunction.PlayPause => T("tf.playpause"),
        TouchFunction.NextTrack => T("tf.next"),
        TouchFunction.PreviousTrack => T("tf.prev"),
        TouchFunction.VolumeUp => T("tf.volup"),
        TouchFunction.VolumeDown => T("tf.voldown"),
        TouchFunction.VoiceRecognition => T("tf.assistant"),
        TouchFunction.QuickAccess1 => T("tf.qa1"),
        TouchFunction.QuickAccess2 => T("tf.qa2"),
        TouchFunction.MicMute => T("tf.micmute"),
        TouchFunction.NextTrackStopGemini => T("tf.nextgemini"),
        TouchFunction.PreviousTrackStopGemini => T("tf.prevgemini"),
        _ => f.ToString(),
    };

    public static string Of(AutoPowerOff a) => a switch
    {
        AutoPowerOff.WhenRemovedFromEars => T("apo.removed"),
        AutoPowerOff.Disabled => T("apo.never"),
        AutoPowerOff.After5Min => T("apo.5"), AutoPowerOff.After15Min => T("apo.15"),
        AutoPowerOff.After30Min => T("apo.30"), AutoPowerOff.After60Min => T("apo.60"), AutoPowerOff.After180Min => T("apo.180"),
        _ => a.ToString(),
    };

    public static string Of(AudioCodec c) => c switch
    {
        AudioCodec.Ldac => "LDAC", AudioCodec.Aac => "AAC", AudioCodec.Sbc => "SBC", AudioCodec.Lc3 => "LC3",
        AudioCodec.AptX => "aptX", AudioCodec.AptXHd => "aptX HD", AudioCodec.Unsettled => T("codec.unsettled"), _ => T("codec.other"),
    };

    public static string Of(VoiceAssistant v) => v switch
    {
        VoiceAssistant.VoiceRecognition => T("va.phone"),
        VoiceAssistant.GoogleAssistant => T("va.google"),
        VoiceAssistant.AmazonAlexa => T("va.alexa"),
        VoiceAssistant.SonyVoiceAssistant => T("va.sony"),
        VoiceAssistant.None => T("va.none"),
        _ => v.ToString(),
    };

    public static string Of(FitTestState t) => t.Status switch
    {
        FitTestStatus.Started => T("fit.running"),
        FitTestStatus.Failed => t.Error switch
        {
            FitTestError.LeftConnection or FitTestError.RightConnection => T("fit.err.connection"),
            FitTestError.Unavailable => T("fit.err.unavailable"),
            FitTestError.LeftFitting => T("fit.err.left"),
            FitTestError.RightFitting => T("fit.err.right"),
            FitTestError.BothFitting => T("fit.err.both"),
            _ => T("fit.err.other"),
        },
        FitTestStatus.Completed when t.LeftGood is { } l && t.RightGood is { } r =>
            T("fit.result", l ? T("fit.good") : T("fit.adjust"), r ? T("fit.good") : T("fit.adjust")),
        FitTestStatus.Completed => T("fit.done"),
        _ => "",
    };

    public static string Of(SoundPressure p) => p.Decibels is { } db ? T("level.db", db) : p.Reason switch
    {
        SoundPressureReason.InCall => T("level.call"),
        SoundPressureReason.Detached => T("level.detached"),
        _ => T("level.none"),
    };

    /// <summary>
    /// Name of a self-described setting: a translation when Resonance knows it ("gs.TAP_SENSITIVITY_SETTING"),
    /// otherwise the identifier made readable ("TOUCH_PANEL_SETTING" → "Touch panel").
    /// </summary>
    public static string GeneralSettingName(string name) =>
        TryT($"gs.{name}") ?? Readable(name.EndsWith("_SETTING") ? name[..^"_SETTING".Length] : name);

    /// <summary>Choice of a list setting: "TAP_SENSITIVITY_SETTING_ELEM_LOW" → "gs.elem.LOW" or "Low".</summary>
    public static string GeneralSettingChoice(string setting, string choice)
    {
        // "TAP_SENSITIVITY_SETTING_ELEM_LOW" or "ASSIGNABLE_KEY_ELEM_NCASM" (v1 drops "_SETTING")
        var stem = setting.EndsWith("_SETTING") ? setting[..^"_SETTING".Length] : setting;
        int at = choice.IndexOf("_ELEM_", StringComparison.Ordinal);
        var elem = choice.StartsWith(stem) && at >= 0 ? choice[(at + "_ELEM_".Length)..] : choice;
        return TryT($"gs.elem.{elem}") ?? Readable(elem);
    }

    private static string? TryT(string key) => T(key) is var text && text != key ? text : null;

    private static string Readable(string id)
    {
        var words = id.Replace('_', ' ').Trim().ToLowerInvariant();
        return words.Length == 0 ? id : char.ToUpperInvariant(words[0]) + words[1..];
    }

    public static Option<NoiseAdaptiveSensitivity>[] AutoAmbientSensitivities { get; } =
    [
        new(NoiseAdaptiveSensitivity.Standard, T("sens.standard")), new(NoiseAdaptiveSensitivity.High, T("sens.high")),
        new(NoiseAdaptiveSensitivity.Low, T("sens.low")),
    ];

    public static Option<PriorMode>[] Priorities { get; } =
    [
        new(PriorMode.SoundQuality, T("prio.sound")),
        new(PriorMode.ConnectionQuality, T("prio.connection")),
    ];

    public static Option<DetectSensitivity>[] Sensitivities { get; } =
    [
        new(DetectSensitivity.Auto, T("sens.auto")), new(DetectSensitivity.High, T("sens.high")), new(DetectSensitivity.Low, T("sens.low")),
    ];

    public static Option<ModeOutTime>[] ModeOutTimes { get; } =
    [
        new(ModeOutTime.Short, T("mot.short")), new(ModeOutTime.Standard, T("mot.standard")),
        new(ModeOutTime.Long, T("mot.long")), new(ModeOutTime.Never, T("mot.never")),
    ];

    public static Option<RoomSize>[] RoomSizes { get; } =
    [
        new(RoomSize.Small, T("room.small")), new(RoomSize.Middle, T("room.middle")), new(RoomSize.Large, T("room.large")),
    ];
}
