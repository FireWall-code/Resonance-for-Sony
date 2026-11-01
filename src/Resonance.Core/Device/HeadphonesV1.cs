using System.Text;
using Resonance.Core.Protocol;

namespace Resonance.Core.Device;

/// <summary>
/// First version of the MDR protocol (WH-1000XM3/XM4, WF-1000XM3/XM4…, service 96CC203E). The frames and
/// most command groups are the same as v2; the common group, the inquired types and several layouts differ,
/// on/off values use 01 for on, and the function list has no priorities.
/// </summary>
public sealed partial class Headphones
{
    private byte _v1NoiseInq = V1.NcAsmNcAndAmbient;

    private IEnumerable<Query> PlanV1()
    {
        var f = State.Functions;
        const ReadGroup info = ReadGroup.Info, bat = ReadGroup.Battery, noise = ReadGroup.Noise, eq = ReadGroup.Equalizer,
            audio = ReadGroup.Audio, set = ReadGroup.Settings, play = ReadGroup.Playback;
        bool all = !f.Known;

        yield return Q1(T1.ConnectGetDeviceInfo, Inq.DeviceInfoModelName, info);
        yield return Q1(T1.ConnectGetDeviceInfo, Inq.DeviceInfoFwVersion, info);
        yield return Q1(T1.ConnectGetDeviceInfo, Inq.DeviceInfoSeriesColor, info);
        yield return Q1(T1.ConnectGetCapabilityInfo, 0x00, info);
        if (all || f.Has(V1.FnCodec)) yield return Q1(V1.GetAudioCodec, 0x00, info);
        if (all || f.Has(V1.FnUpscalingIndicator)) yield return Q1(V1.GetUpscalingEffect, 0x00, audio);

        if (all || f.Has(V1.FnBattery)) yield return Q1(V1.GetBattery, V1.BatterySingle, bat);
        if (f.Has(V1.FnLrBattery)) yield return Q1(V1.GetBattery, V1.BatteryLr, bat);
        if (f.Has(V1.FnCradleBattery)) yield return Q1(V1.GetBattery, V1.BatteryCradle, bat);

        byte? nc = f.Has(V1.FnNcAndAmbient) || all ? V1.NcAsmNcAndAmbient
            : f.Has(V1.FnAmbient) ? V1.NcAsmAmbient : f.Has(V1.FnNc) ? V1.NcAsmNc : null;
        if (nc is { } n) yield return Q1(T1.NcAsm | Op.GetParam, n, noise);
        if (f.Has(V1.FnSmartTalking)) yield return Q1(T1.System | Op.GetParam, V1.SystemSmartTalking, noise);

        if (all || f.Has(V1.FnPresetEq))
        {
            yield return Q1(T1.EqEbb | Op.GetCapability, V1.PresetEq, eq);
            yield return Q1(T1.EqEbb | Op.GetExt, V1.PresetEq, eq);
            yield return Q1(T1.EqEbb | Op.GetParam, V1.PresetEq, eq);
        }

        if (all || f.Has(V1.FnConnectionMode)) yield return Q1(T1.Audio | Op.GetParam, V1.AudioConnectionMode, audio);
        if (all || f.Has(V1.FnUpscaling)) yield return Q1(T1.Audio | Op.GetParam, V1.AudioUpscaling, audio);

        if (f.Has(V1.FnAutoPowerOff))
        {
            yield return Q1(T1.System | Op.GetCapability, V1.SystemAutoPowerOff, set);
            yield return Q1(T1.System | Op.GetParam, V1.SystemAutoPowerOff, set);
        }
        if (f.Has(V1.FnControlByWearing)) yield return Q1(T1.System | Op.GetParam, V1.SystemControlByWearing, set);
        for (byte gs = F1.GeneralSetting1; gs <= V1.FnGeneralSetting3; gs++)
        {
            if (!f.Has(gs)) continue;
            yield return Q1(T1.GeneralSetting | Op.GetCapability, gs, set);
            yield return Q1(T1.GeneralSetting | Op.GetParam, gs, set);
        }

        if (all || f.Has(V1.FnPlayback))
        {
            yield return Q1(T1.Play | Op.GetCapability, Inq.PlaybackController, play);
            yield return Q1(T1.Play | Op.GetParam, Inq.PlaybackController, play);
        }
    }

    private bool ParseV1(byte[] p, DataType table)
    {
        if (table != DataType.DataMdrNo1) return false; // table 2 layouts of v1 models are not decoded yet
        var s = State;
        byte inq = p[1];
        switch (p[0])
        {
            case V1.RetBattery or V1.NtfyBattery:
                switch (inq)
                {
                    case V1.BatterySingle: s.Battery = new(p[2], V1Charging(p[3])); return true;
                    case V1.BatteryLr:
                        s.BatteryLeft = new(p[2], V1Charging(p[3]));
                        s.BatteryRight = new(p[4], V1Charging(p[5]));
                        return true;
                    case V1.BatteryCradle: s.BatteryCase = new(p[2], V1Charging(p[3])); return true;
                }
                return false;
            case V1.RetUpscalingEffect or V1.NtfyUpscalingEffect: // <type> <effect type> <01 active>
                s.DseeActive = p.Length > 3 && p[3] == 0x01; return true;
            case V1.RetAudioCodec or V1.NtfyAudioCodec:
                s.Codec = (AudioCodec)p[2]; return true;

            // NC + ambient: <effect> <nc type> <nc 00 off / 01 single / 02 dual> <asm type> <voice> <level>
            case (T1.NcAsm | Op.RetParam) or (T1.NcAsm | Op.NtfyParam) when inq == V1.NcAsmNcAndAmbient:
                _v1NoiseInq = inq;
                s.NoiseVariant = inq;
                s.NoiseControl = new(p[2] != 0x00, p[4] == V1.NcDual ? NcAsmMode.NoiseCancelling : NcAsmMode.Ambient,
                    p[6] == 0x01, p[7], false, NoiseAdaptiveSensitivity.Standard);
                return true;
            case (T1.NcAsm | Op.RetParam) or (T1.NcAsm | Op.NtfyParam) when inq == V1.NcAsmAmbient: // <effect> <type> <voice> <level>
                _v1NoiseInq = inq;
                s.NoiseVariant = Inq.AsmSeamless; // ambient sound only: no noise cancelling choice
                s.NoiseControl = new(p[2] != 0x00, NcAsmMode.Ambient, p[4] == 0x01, p[5], false, NoiseAdaptiveSensitivity.Standard);
                return true;
            case (T1.NcAsm | Op.RetParam) or (T1.NcAsm | Op.NtfyParam) when inq == V1.NcAsmNc: // <type> <01 on>
                _v1NoiseInq = inq;
                s.NoiseVariant = inq;
                s.NoiseControl = new(p[3] == 0x01, NcAsmMode.NoiseCancelling, false, 0, false, NoiseAdaptiveSensitivity.Standard);
                return true;

            case T1.EqEbb | Op.RetCapability when inq == V1.PresetEq:
                ParseEqualizerCapability(p); return true;
            case T1.EqEbb | Op.RetExt when inq == V1.PresetEq:
                ParseEqualizerBands(p); return true;
            case (T1.EqEbb | Op.RetParam) or (T1.EqEbb | Op.NtfyParam) when inq == V1.PresetEq:
                ParseEqualizerParam(p); return true;

            case (T1.Audio | Op.RetParam) or (T1.Audio | Op.NtfyParam) when inq == V1.AudioConnectionMode: // <type> <00 sound / 01 stable>
                s.ConnectionPriority = (PriorMode)p[3]; return true;
            case (T1.Audio | Op.RetParam) or (T1.Audio | Op.NtfyParam) when inq == V1.AudioUpscaling: // <type> <00 off / 01 auto>
                s.Dsee = p[3] == 0x01; return true;

            case T1.System | Op.RetCapability when inq == V1.SystemAutoPowerOff: // <n> <element>×n
                s.AutoPowerOffOptions = p.Skip(3).Take(p[2]).Select(b => (AutoPowerOff)b).ToList(); return true;
            case (T1.System | Op.RetParam) or (T1.System | Op.NtfyParam) when inq == V1.SystemAutoPowerOff: // <type> <active> <selected time>
                s.AutoPowerOff = (AutoPowerOff)p[3]; return true;
            case (T1.System | Op.RetParam) or (T1.System | Op.NtfyParam) when inq == V1.SystemControlByWearing:
                s.PauseWhenRemoved = p[3] == 0x01; return true;
            case (T1.System | Op.RetParam) or (T1.System | Op.NtfyParam) when inq == V1.SystemSmartTalking:
                s.SpeakToChat = new(p[3] == 0x01, DetectSensitivity.Auto, ModeOutTime.Standard); return true;

            case T1.GeneralSetting | Op.RetCapability:
                ParseGeneralCapabilityV1(p); return true;
            case (T1.GeneralSetting | Op.RetParam) or (T1.GeneralSetting | Op.NtfyParam):
                UpdateGeneralSetting(inq, p[2], p[3]); return true;

            case T1.Play | Op.RetCapability when inq == Inq.PlaybackController: // <volume steps> …
                s.MusicVolumeMax = p[2] - 1; return true;
            case (T1.Play | Op.RetParam) or (T1.Play | Op.NtfyParam) when inq == Inq.PlaybackController:
                ParseNowPlayingV1(p); return true;
        }
        return false;
    }

    private static ChargingStatus V1Charging(byte b) => b == 0x01 ? ChargingStatus.Charging : ChargingStatus.NotCharging;

    // A7 01 <data type> …: 00 title / 01 album / 02 artist = <status> <len> <utf8>; 20 volume = <value>
    private void ParseNowPlayingV1(byte[] p)
    {
        var s = State;
        if (p[2] == V1.PlayVolume) { s.MusicVolume = p[3]; return; }
        if (p[2] > 0x02 || p.Length < 5) return;
        var text = Encoding.UTF8.GetString(p, 5, Math.Min(p[4], p.Length - 5));
        var np = s.NowPlaying ?? new NowPlaying("", "", "", PlaybackStatus.Unsettled);
        s.NowPlaying = p[2] switch
        {
            0x00 => np with { Title = text },
            0x01 => np with { Album = text },
            _ => np with { Artist = text },
        };
    }

    // D1 <slot> <format> <len> name <len> summary <type 01 switch / 02 list> [<n> (<format> <len> name <len> summary)×n]
    private void ParseGeneralCapabilityV1(byte[] p)
    {
        byte id = p[1];
        int i = 3;
        var name = Str(p, i);
        i += 1 + p[i];
        i += 1 + p[i];
        bool isList = i < p.Length && p[i] == V1.GsList;
        i++;
        var options = new List<GeneralSettingOption>();
        if (isList && i < p.Length)
        {
            int n = p[i++];
            for (int k = 0; k < n && i + 1 < p.Length; k++)
            {
                i++; // format
                options.Add(new((byte)k, Str(p, i)));
                i += 1 + p[i];
                i += 1 + p[i];
            }
        }
        var value = State._generalSettings.TryGetValue(id, out var old) ? old.Value : null;
        State._generalSettings[id] = new(id, name, isList, options, value, OnValue: 0x01);
        ApplyKnownGeneralSetting(State._generalSettings[id]);
    }

    // ---------------------------------------------------------------- writing

    private Task SetNoiseControlV1Async(NoiseControlState nc)
    {
        byte effect = (byte)(nc.Enabled ? 1 : 0), voice = (byte)(nc.VoiceFocus ? 1 : 0);
        byte level = (byte)Math.Clamp(nc.AmbientLevel, 0, 20);
        return _v1NoiseInq switch
        {
            V1.NcAsmAmbient => Set1(T1.NcAsm | Op.SetParam, V1.NcAsmAmbient, effect, 0x01, voice, level),
            V1.NcAsmNc => Set1(T1.NcAsm | Op.SetParam, V1.NcAsmNc, 0x00, effect),
            // Same encoding as SonyHeadphonesClient, verified there on a WH-1000XM3/XM4: level adjustment type,
            // dual = noise cancelling, off = ambient sound at the given level.
            _ => nc.Mode == NcAsmMode.NoiseCancelling
                ? Set1(T1.NcAsm | Op.SetParam, V1.NcAsmNcAndAmbient, effect, 0x01, V1.NcDual, 0x01, voice, 0x00)
                : Set1(T1.NcAsm | Op.SetParam, V1.NcAsmNcAndAmbient, effect, 0x01, 0x00, 0x01, voice, level),
        };
    }

    private Task SetSystemSwitchV1Async(byte inq, bool on) => Set1(T1.System | Op.SetParam, inq, 0x00, (byte)(on ? 1 : 0));
}
