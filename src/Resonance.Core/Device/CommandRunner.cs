using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Resonance.Core.Protocol;
using static Resonance.Core.Localization.Loc;

namespace Resonance.Core.Device;

/// <summary>Outcome of a command: the text to print, whether it succeeded, and whether the
/// app was still connecting (the CLI then waits and retries).</summary>
public sealed record CommandResult(bool Ok, string Text, bool NotReady = false)
{
    public static CommandResult Done(string text) => new(true, text);
    public static CommandResult Fail(string text) => new(false, T("cli.error", text));
    public static CommandResult Waiting(string text) => new(false, T("cli.error", text), NotReady: true);
}

/// <summary>
/// Everyday commands shared by the command line and the running app (which executes them on
/// behalf of the CLI through a named pipe). English names first, French names as aliases.
/// </summary>
public static class CommandRunner
{
    public static string Help => T("cli.help");

    public static async Task<CommandResult> RunAsync(Headphones hp, string[] args)
    {
        try { return CommandResult.Done(await RunCoreAsync(hp, args)); }
        catch (UsageException ex) { return CommandResult.Fail(ex.Message); }
        catch (TimeoutException) { return CommandResult.Fail(T("cli.noanswer")); }
    }

    private sealed class UsageException(string message) : Exception(message);

    /// <summary>Canonical command name for an English or French spelling.</summary>
    private static string Canonical(string cmd) => Normalize(cmd) switch
    {
        "status" or "etat" => "status",
        "json" => "json",
        "help" or "aide" or "-h" or "--help" or "/?" => "help",
        "nc" or "anc" => "nc",
        "ambient" or "ambiant" => "ambient",
        "off" or "desactive" => "off",
        "voice" or "voix" => "voice",
        "auto" => "auto",
        "toggle" or "bascule" => "toggle",
        "eq" => "eq",
        "dsee" => "dsee",
        "bgm" or "fond" => "bgm",
        "stc" or "speak-to-chat" or "conversation" => "stc",
        "volume" or "vol" => "volume",
        "play" or "lecture" => "play",
        "pause" => "pause",
        "next" or "suivant" => "next",
        "prev" or "previous" or "precedent" => "prev",
        "devices" or "appareils" => "devices",
        "pin" or "epingler" => "pin",
        "connect" or "connecter" => "connect",
        "disconnect" or "deconnecter" => "disconnect",
        "poweroff" or "eteindre" => "poweroff",
        var other => other,
    };

    /// <summary>What a command must read first; a direct CLI connection reads nothing else.</summary>
    private static ReadGroup Needs(string cmd) => cmd switch
    {
        "status" => ReadGroup.Info | ReadGroup.Battery | ReadGroup.Noise | ReadGroup.Equalizer | ReadGroup.Audio | ReadGroup.Playback,
        "json" => ReadGroup.All,
        "nc" or "ambient" or "off" or "voice" or "auto" or "toggle" or "stc" => ReadGroup.Noise,
        "eq" => ReadGroup.Equalizer,
        "dsee" or "bgm" => ReadGroup.Audio,
        "volume" => ReadGroup.Playback,
        "devices" or "connect" or "disconnect" or "pin" => ReadGroup.Devices,
        _ => ReadGroup.None,
    };

    private static async Task<string> RunCoreAsync(Headphones hp, string[] args)
    {
        var s = hp.State;
        string cmd = Canonical(args.ElementAtOrDefault(0) ?? "status");
        string? a1 = args.ElementAtOrDefault(1), a2 = args.ElementAtOrDefault(2);
        await hp.EnsureAsync(Needs(cmd));

        switch (cmd)
        {
            case "status":
                return Status(s);
            case "json":
                return JsonSerializer.Serialize(s, JsonOptions);
            case "help":
                return Help;

            case "nc":
                return await ApplyNc(hp, nc => nc with { Enabled = true, Mode = NcAsmMode.NoiseCancelling });
            case "ambient":
            {
                int? level = a1 is null ? null : ParseInt(a1, 1, 20, T("cli.what.level"));
                // Level and voice only apply in ambient mode: always send them together with the mode.
                return await ApplyNc(hp, nc => nc with
                {
                    Enabled = true, Mode = NcAsmMode.Ambient, AmbientLevel = level ?? nc.AmbientLevel,
                    NoiseAdaptive = level is null && nc.NoiseAdaptive,
                });
            }
            case "off":
                return await ApplyNc(hp, nc => nc with { Enabled = false });
            case "voice":
                return await ApplyNc(hp, nc => nc with { Enabled = true, Mode = NcAsmMode.Ambient, VoiceFocus = ParseOnOff(a1) });
            case "auto":
                return await ApplyNc(hp, nc => nc with { Enabled = true, Mode = NcAsmMode.Ambient, NoiseAdaptive = ParseOnOff(a1) });
            case "toggle":
                return await ApplyNc(hp, nc => !nc.Enabled
                    ? nc with { Enabled = true, Mode = NcAsmMode.NoiseCancelling }
                    : nc.Mode == NcAsmMode.NoiseCancelling ? nc with { Mode = NcAsmMode.Ambient } : nc with { Enabled = false });

            case "eq":
                return await Equalizer(hp, args.Skip(1).ToArray());

            case "dsee":
            {
                bool on = ParseOnOff(a1);
                await hp.SetDseeAsync(on);
                return await Confirm(() => s.Dsee == on, T("cli.dsee", OnOffWord(on)));
            }
            case "pin":
            {
                if (s.PlaybackPinned is null) throw new UsageException(T("cli.pin.unavailable"));
                if (a1 is null) return T("cli.pin", OnOffWord(s.PlaybackPinned.Value));
                bool on = ParseOnOff(a1);
                await hp.SetPlaybackPinnedAsync(on);
                return await Confirm(() => s.PlaybackPinned == on, T("cli.pin", OnOffWord(on)));
            }
            case "bgm":
            {
                bool on = ParseOnOff(a1);
                var room = a2 is null ? s.BgmRoomSize ?? RoomSize.Small : Normalize(a2) switch
                {
                    "office" or "bureau" or "small" => RoomSize.Small,
                    "living" or "salon" or "middle" => RoomSize.Middle,
                    "cafe" or "large" => RoomSize.Large,
                    _ => throw new UsageException(T("cli.room")),
                };
                await hp.SetListeningModeAsync(on, room);
                return await Confirm(() => s.ListeningModeBgm == on, on ? T("cli.bgm.on", RoomName(room)) : T("cli.bgm.off"));
            }
            case "stc":
            {
                bool on = ParseOnOff(a1);
                await hp.SetSpeakToChatAsync(on);
                return await Confirm(() => s.SpeakToChat?.Enabled == on, T("cli.stc", OnOffWord(on)));
            }

            case "volume":
            {
                int max = s.MusicVolumeMax ?? 30;
                if (a1 is null) return T("cli.volume", s.MusicVolume, max);
                int v = ParseInt(a1, 0, max, T("cli.what.volume"));
                await hp.SetMusicVolumeAsync(v);
                return await Confirm(() => s.MusicVolume == v, T("cli.volume", v, max));
            }
            case "play":
                await hp.SendPlaybackAsync(PlaybackControl.Play); return T("cli.play");
            case "pause":
                await hp.SendPlaybackAsync(PlaybackControl.Pause); return T("cli.pause");
            case "next":
                await hp.SendPlaybackAsync(PlaybackControl.Next); return T("cli.next");
            case "prev":
                await hp.SendPlaybackAsync(PlaybackControl.Previous); return T("cli.prev");

            case "devices":
                return Devices(s);
            case "connect":
                return await RunDeviceAction(hp, DeviceAction.Connect, a1);
            case "disconnect":
                return await RunDeviceAction(hp, DeviceAction.Disconnect, a1);

            case "poweroff":
                if (a1 is not ("--yes" or "--oui")) return T("cli.poweroff.confirm");
                await hp.PowerOffAsync();
                return T("cli.poweroff.done");

            default:
                throw new UsageException(T("cli.unknown", args[0]));
        }
    }

    // ------------------------------------------------------------ helpers

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Lower-case, accents removed: "Éteindre" → "eteindre".</summary>
    private static string Normalize(string text)
    {
        var d = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(d.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static bool ParseOnOff(string? v) => Normalize(v ?? "") switch
    {
        "on" or "yes" or "oui" or "1" or "true" => true,
        "off" or "no" or "non" or "0" or "false" => false,
        _ => throw new UsageException(T("cli.onoff")),
    };

    private static string OnOffWord(bool on) => on ? T("cli.on") : T("cli.off");

    private static int ParseInt(string v, int min, int max, string what) =>
        int.TryParse(v, out var n) && n >= min && n <= max ? n : throw new UsageException(T("cli.range", what, min, max));

    /// <summary>Waits up to 2 s for the headphones' notification so the reply reflects the real state.</summary>
    private static async Task<string> Confirm(Func<bool> done, string message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!done() && DateTime.UtcNow < deadline) await Task.Delay(50);
        return done() ? message : message + T("cli.unconfirmed");
    }

    private static async Task<string> ApplyNc(Headphones hp, Func<NoiseControlState, NoiseControlState> change)
    {
        var current = hp.State.NoiseControl ?? throw new UsageException(T("cli.nc.unavailable"));
        var next = change(current);
        await hp.SetNoiseControlAsync(next);
        return await Confirm(() => hp.State.NoiseControl is { } n && n.Enabled == next.Enabled
            && (!n.Enabled || n.Mode == next.Mode), NoiseLine(next));
    }

    private static string NoiseLine(NoiseControlState nc) =>
        !nc.Enabled ? T("cli.noise.off")
        : nc.Mode == NcAsmMode.NoiseCancelling ? T("cli.noise.nc")
        : T("cli.noise.ambient", nc.NoiseAdaptive ? T("cli.noise.auto") : T("cli.noise.level", nc.AmbientLevel),
            nc.VoiceFocus ? T("cli.noise.voice") : "");

    private static string RoomName(RoomSize r) => r switch
    {
        RoomSize.Small => T("cli.room.small"), RoomSize.Middle => T("cli.room.middle"), _ => T("cli.room.large"),
    };

    private static string Status(HeadphoneState s)
    {
        var sb = new StringBuilder();
        var codec = s.Codec switch
        {
            AudioCodec.Ldac => "LDAC", AudioCodec.Aac => "AAC", AudioCodec.Sbc => "SBC", AudioCodec.Lc3 => "LC3",
            AudioCodec.AptX => "aptX", AudioCodec.AptXHd => "aptX HD", null => "?", _ => T("codec.other"),
        };
        sb.AppendLine(T("cli.status.header", s.ModelName ?? T("headphones"), s.FirmwareVersion, codec));
        string Bat(BatteryInfo? b) => b is null ? "?" : $"{b.Level}%{(b.IsCharging ? " ⚡" : "")}";
        sb.AppendLine(s.Battery is { } single && s.BatteryLeft is null
            ? T("cli.status.battery1", Bat(single))
            : T("cli.status.battery", Bat(s.BatteryLeft), Bat(s.BatteryRight), Bat(s.BatteryCase)));
        if (s.NoiseControl is { } nc) sb.AppendLine(NoiseLine(nc));
        if (s.Equalizer is { } eq) sb.AppendLine(T("cli.status.eq", PresetName(eq.Preset)));
        if (s.SoundPressure is { Decibels: { } db }) sb.AppendLine(T("cli.status.level", db));
        sb.AppendLine(T("cli.status.line", OnOffWord(s.Dsee == true),
            s.ListeningModeBgm == true ? $"{T("cli.on")} ({RoomName(s.BgmRoomSize ?? RoomSize.Small)})" : T("cli.off"),
            OnOffWord(s.SpeakToChat?.Enabled == true)));
        if (s.NowPlaying is { Title.Length: > 0 } np)
            sb.AppendLine(T("cli.status.playing", np.Status == PlaybackStatus.Playing ? "▶" : "⏸", np.Title, np.Artist,
                s.MusicVolume, s.MusicVolumeMax ?? 30));
        return sb.ToString().TrimEnd();
    }

    private static string Devices(HeadphoneState s)
    {
        if (s.PairedDevices is not { Count: > 0 } list) return T("cli.devices.none");
        return string.Join(Environment.NewLine, list.Select(d =>
            $"{(d.Playing ? "▶" : d.Connected ? "●" : "○")} {d.Name,-24} {d.Address}  " +
            (d.Playing ? T("cli.dev.playing") : d.Connected ? T("cli.dev.connected") : T("cli.dev.off"))));
    }

    private static async Task<string> RunDeviceAction(Headphones hp, DeviceAction action, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new UsageException(T("cli.device.missing"));
        var list = hp.State.PairedDevices ?? [];
        var t = Normalize(target);
        var device = list.FirstOrDefault(d => Normalize(d.Address) == t)
                     ?? list.FirstOrDefault(d => Normalize(d.Name).Contains(t))
                     ?? throw new UsageException(T("cli.device.nomatch", target));
        await hp.SendDeviceActionAsync(action, device.Address);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (hp.State.LastDeviceAction is { InProgress: false } r && r.Address == device.Address && r.Action == action)
                return r.Success ? T("cli.device.ok", device.Name) : T("cli.device.fail", device.Name);
            await Task.Delay(100);
        }
        return T("cli.device.noanswer", device.Name);
    }

    // ------------------------------------------------------------ equalizer

    private static readonly (string Name, EqPreset Preset)[] PresetNames =
    [
        ("off", EqPreset.Off), ("desactive", EqPreset.Off), ("heavy", EqPreset.Heavy), ("puissant", EqPreset.Heavy),
        ("clear", EqPreset.Clear), ("clair", EqPreset.Clear), ("hard", EqPreset.Hard), ("percutant", EqPreset.Hard),
        ("soft", EqPreset.Soft), ("doux", EqPreset.Soft), ("gaming", EqPreset.Gaming), ("jeu", EqPreset.Gaming),
        ("custom", EqPreset.Custom), ("perso", EqPreset.Custom), ("personnalise", EqPreset.Custom),
        ("user1", EqPreset.User1), ("user2", EqPreset.User2),
    ];

    private static string PresetName(EqPreset p) => p switch
    {
        EqPreset.Off => T("eq.off"), EqPreset.Heavy => T("eq.heavy"), EqPreset.Clear => T("eq.clear"), EqPreset.Hard => T("eq.hard"),
        EqPreset.Soft => T("eq.soft"), EqPreset.Gaming => T("eq.gaming"), EqPreset.Custom => T("eq.custom"),
        EqPreset.User1 => T("eq.user1"), EqPreset.User2 => T("eq.user2"), _ => p.ToString(),
    };

    /// <summary>The name to type for a preset, in the current language (e.g. "heavy" / "puissant").</summary>
    private static string PresetCommand(EqPreset p)
    {
        var names = PresetNames.Where(n => n.Preset == p).Select(n => n.Name).ToList();
        return Language == "fr" && names.Count > 1 ? names[1] : names.FirstOrDefault() ?? p.ToString();
    }

    private static async Task<string> Equalizer(Headphones hp, string[] args)
    {
        var s = hp.State;
        var cap = s.EqualizerCapability ?? throw new UsageException(T("cli.eq.unavailable"));
        if (args.Length == 0)
        {
            var bands = s.Equalizer is { } e
                ? string.Join("  ", e.Bands.Select((b, i) => $"{FormatHz(cap.FrequenciesHz.ElementAtOrDefault(i))}:{b - cap.Center:+0;-0;0}"))
                : "?";
            return $"{T("cli.status.eq", PresetName(s.Equalizer?.Preset ?? EqPreset.Off))}\n{bands}\n" +
                   T("cli.eq.presets", string.Join(", ", cap.Presets.Select(PresetCommand)));
        }
        if (Normalize(args[0]) is "bands" or "bandes")
        {
            var values = args.Skip(1).Select(v => ParseInt(v, -cap.Center, cap.Center, T("cli.what.gain"))).ToArray();
            if (values.Length != cap.BandCount) throw new UsageException(T("cli.eq.count", cap.BandCount));
            var preset = s.Equalizer?.Preset is EqPreset.User1 or EqPreset.User2 ? s.Equalizer.Preset : EqPreset.Custom;
            await hp.SetEqualizerAsync(preset, values.Select(v => v + cap.Center).ToArray());
            return await Confirm(() => s.Equalizer?.Bands.SequenceEqual(values.Select(v => v + cap.Center)) == true,
                T("cli.eq.bands", PresetName(preset), string.Join(" ", values.Select(v => v.ToString("+0;-0;0")))));
        }
        var name = Normalize(string.Join(" ", args));
        var match = PresetNames.Where(p => p.Name == name).Select(p => (EqPreset?)p.Preset).FirstOrDefault();
        if (match is not { } wanted || !cap.Presets.Contains(wanted))
            throw new UsageException(T("cli.eq.unknown", string.Join(", ", cap.Presets.Select(PresetCommand))));
        await hp.SetEqualizerPresetAsync(wanted);
        return await Confirm(() => s.Equalizer?.Preset == wanted, T("cli.status.eq", PresetName(wanted)));
    }

    private static string FormatHz(int hz) =>
        hz == 0 ? T("eq.clearbass") : hz >= 1000 ? (hz / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k" : hz.ToString(CultureInfo.InvariantCulture);
}
