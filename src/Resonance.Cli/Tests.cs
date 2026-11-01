using Resonance.Core.Device;
using Resonance.Core.Protocol;

/// <summary>
/// Round-trip tests on real headphones: change a setting, wait for the
/// headphones' notification, check the decoded state, then restore it.
/// </summary>
static class Tests
{
    public static async Task RunAsync(Headphones hp, string[] only)
    {
        var s = hp.State;
        if (Environment.GetEnvironmentVariable("RESONANCE_TRACE") == "1") hp.Trace += t => Console.WriteLine($"      {t}");
        var signal = new SemaphoreSlim(0);
        hp.StateChanged += () => signal.Release();
        int pass = 0, fail = 0;

        async Task<bool> WaitFor(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!condition())
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero || !await signal.WaitAsync(left)) return condition();
            }
            return true;
        }

        Task Check<T>(string name, Func<T?> read, T? changed, Func<T, Task> write) where T : struct =>
            CheckCore(name, () => read() is { } v ? (object)v : null, changed, v => write((T)v));
        Task CheckS(string name, Func<string?> read, string changed, Func<string, Task> write) =>
            CheckCore(name, read, changed, v => write((string)v));

        async Task CheckCore(string name, Func<object?> read, object? changed, Func<object, Task> write)
        {
            if (only.Length > 0 && !only.Any(o => name.Contains(o, StringComparison.OrdinalIgnoreCase))) return;
            var original = read();
            if (original is null) { Console.WriteLine($"SKIP  {name} (not reported by the headphones)"); return; }
            bool changedOk = false, restoredOk = false;
            try
            {
                await write(changed!);
                changedOk = await WaitFor(() => Equals(read(), changed));
                await write(original!);
                restoredOk = await WaitFor(() => Equals(read(), original));
            }
            catch (Exception ex) { Console.WriteLine($"      {ex.Message}"); }
            bool ok = changedOk && restoredOk;
            if (ok) pass++; else fail++;
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}: {original} -> {changed} ({(changedOk ? "ok" : "not confirmed")}) -> {original} ({(restoredOk ? "ok" : "not confirmed")})");
        }

        var nc = s.NoiseControl;
        if (nc != null)
        {
            await Check("Noise control: mode", () => s.NoiseControl?.Mode, nc.Mode == NcAsmMode.Ambient ? NcAsmMode.NoiseCancelling : NcAsmMode.Ambient,
                m => hp.SetNoiseControlAsync(s.NoiseControl! with { Mode = m }));
            await Check("Noise control: on/off", () => s.NoiseControl?.Enabled, !nc.Enabled,
                v => hp.SetNoiseControlAsync(nc with { Enabled = v })); // the headphones report mode 00 while off
            // Level and voice only apply in ambient mode; restore ambient values first, then the mode.
            async Task RestoreNc() { await hp.SetNoiseControlAsync(nc with { Mode = NcAsmMode.Ambient }); await hp.SetNoiseControlAsync(nc); }
            await Check<int>("Ambient sound: level",
                () => s.NoiseControl?.Mode == NcAsmMode.Ambient ? s.NoiseControl.AmbientLevel : s.NoiseControl == nc ? nc.AmbientLevel : null,
                nc.AmbientLevel == 10 ? 15 : 10,
                async v => { if (v == nc.AmbientLevel) await RestoreNc(); else await hp.SetNoiseControlAsync(nc with { Mode = NcAsmMode.Ambient, AmbientLevel = v }); });
            await Check<bool>("Ambient sound: voice focus",
                () => s.NoiseControl?.Mode == NcAsmMode.Ambient ? s.NoiseControl.VoiceFocus : s.NoiseControl == nc ? nc.VoiceFocus : null,
                !nc.VoiceFocus,
                async v => { if (v == nc.VoiceFocus) await RestoreNc(); else await hp.SetNoiseControlAsync(nc with { Mode = NcAsmMode.Ambient, VoiceFocus = v }); });
        }
        var ambTap = s.TouchCustomizations?.FirstOrDefault(c => c.Preset == TouchPreset.AmbientSoundControlQuickAccess)
            ?.Bindings.FirstOrDefault(g => g.Action == TouchAction.SingleTap)?.Function;
        await Check("Ambient key: cycle", () => s.TouchCustomizations?.FirstOrDefault(c => c.Preset == TouchPreset.AmbientSoundControlQuickAccess)
                ?.Bindings.FirstOrDefault(g => g.Action == TouchAction.SingleTap)?.Function,
            ambTap == TouchFunction.NcAmbient ? TouchFunction.NcAmbientOff : TouchFunction.NcAmbient,
            f => hp.SetGestureAsync(TouchPreset.AmbientSoundControlQuickAccess, TouchAction.SingleTap, f));

        if (s.Equalizer is { } eq)
        {
            var bands = eq.Bands.ToArray();
            await Check("Equalizer: preset", () => s.Equalizer?.Preset, eq.Preset == EqPreset.Heavy ? EqPreset.Clear : EqPreset.Heavy,
                hp.SetEqualizerPresetAsync);
            var tweaked = bands.ToArray(); tweaked[0] = tweaked[0] == 8 ? 4 : 8;
            await CheckS("Equalizer: bands (Custom)", () => s.Equalizer is { } e ? $"{e.Preset}:{string.Join(",", e.Bands)}" : null,
                $"Custom:{string.Join(",", tweaked)}",
                async v => { if (v.StartsWith("Custom")) await hp.SetEqualizerAsync(EqPreset.Custom, tweaked); else await hp.SetEqualizerPresetAsync(eq.Preset); });
        }

        await Check("DSEE", () => s.Dsee, !(s.Dsee ?? false), hp.SetDseeAsync);
        // Connection priority renegotiates the codec and can drop the control link: verified by hand.
        await Check("Background music", () => s.ListeningModeBgm, !(s.ListeningModeBgm ?? false),
            v => hp.SetListeningModeAsync(v, s.BgmRoomSize ?? RoomSize.Small));
        await Check("Speak-to-chat", () => s.SpeakToChat?.Enabled, !(s.SpeakToChat?.Enabled ?? false), hp.SetSpeakToChatAsync);
        await Check("Speak-to-chat: timeout", () => s.SpeakToChat?.ModeOutTime,
            s.SpeakToChat?.ModeOutTime == ModeOutTime.Long ? ModeOutTime.Short : ModeOutTime.Long,
            t => hp.SetSpeakToChatOptionsAsync(s.SpeakToChat!.Sensitivity, t));
        await CheckS("Touch presets", () => s.TouchLeft is { } l && s.TouchRight is { } r ? $"{l}/{r}" : null,
            $"{s.TouchRight}/{s.TouchLeft}",
            v => { var p = v.Split('/'); return hp.SetTouchPresetsAsync(Enum.Parse<TouchPreset>(p[0]), Enum.Parse<TouchPreset>(p[1])); });
        await Check("Head gestures", () => s.HeadGestures, !(s.HeadGestures ?? false), hp.SetHeadGesturesAsync);
        await Check("Auto power off", () => s.AutoPowerOff,
            s.AutoPowerOff == AutoPowerOff.Disabled ? AutoPowerOff.WhenRemovedFromEars : AutoPowerOff.Disabled, hp.SetAutoPowerOffAsync);
        await Check("Sidetone", () => s.Sidetone, !(s.Sidetone ?? false), hp.SetSidetoneAsync);
        await Check("One-earbud noise control", () => s.OneSideNoiseControl, !(s.OneSideNoiseControl ?? false), hp.SetOneSideNoiseControlAsync);
        await Check("Battery care", () => s.CaringCharge, !(s.CaringCharge ?? false), hp.SetCaringChargeAsync);
        await Check("Voice guidance", () => s.VoiceGuidance, !(s.VoiceGuidance ?? false), hp.SetVoiceGuidanceAsync);
        await Check("Volume", () => s.MusicVolume, (s.MusicVolume ?? 8) > 3 ? s.MusicVolume - 2 : s.MusicVolume + 2,
            v => hp.SetMusicVolumeAsync(v));

        if (nc != null)
        {
            // Auto ambient sound only applies in ambient mode.
            async Task RestoreAuto() { await hp.SetNoiseControlAsync(nc with { Mode = NcAsmMode.Ambient }); await hp.SetNoiseControlAsync(nc); }
            await Check<bool>("Auto ambient sound",
                () => s.NoiseControl?.Mode == NcAsmMode.Ambient ? s.NoiseControl.NoiseAdaptive : s.NoiseControl == nc ? nc.NoiseAdaptive : null,
                !nc.NoiseAdaptive,
                async v => { if (v == nc.NoiseAdaptive) await RestoreAuto(); else await hp.SetNoiseControlAsync(nc with { Mode = NcAsmMode.Ambient, NoiseAdaptive = v }); });
        }
        if (s.VoiceAssistantOptions is { Count: > 1 } va)
            await Check("Voice assistant", () => s.VoiceAssistant, va.Contains(VoiceAssistant.VoiceRecognition) && s.VoiceAssistant != VoiceAssistant.VoiceRecognition ? VoiceAssistant.VoiceRecognition : va.First(v => v != s.VoiceAssistant && v != VoiceAssistant.SonyVoiceAssistant), hp.SetVoiceAssistantAsync);
        await Check("Voice guidance volume", () => s.VoiceGuidanceVolume, s.VoiceGuidanceVolume == 1 ? 0 : 1, hp.SetVoiceGuidanceVolumeAsync);

        if (only.Any(o => o is "devices" or "appareils") && s.PairedDevices?.FirstOrDefault(d => !d.Connected) is { } off)
        {
            // Asking to connect a device that is switched off must come back with an explicit result.
            await hp.SendDeviceActionAsync(DeviceAction.Connect, off.Address);
            bool answered = await WaitFor(() => s.LastDeviceAction is { InProgress: false } r && r.Address == off.Address);
            Console.WriteLine($"{(answered ? "PASS" : "FAIL")}  Connecting {off.Name}: {(s.LastDeviceAction is { } r2 ? (r2.Success ? "connected" : "refused / unreachable") : "no answer")}");
            if (answered) pass++; else fail++;
            if (s.LastDeviceAction is { Success: true })
            {
                await hp.SendDeviceActionAsync(DeviceAction.Disconnect, off.Address);
                await WaitFor(() => s.LastDeviceAction is { Action: DeviceAction.Disconnect, InProgress: false });
                Console.WriteLine("      disconnected again");
            }
        }

        if (s.QuickAccess is { Count: 2 } qa)
            await CheckS("Quick access 1", () => s.QuickAccess is { Count: 2 } q ? $"{q[0]},{q[1]}" : null,
                $"{(qa[0] == 1 ? 0 : 1)},{qa[1]}", v => { var b = v.Split(',').Select(byte.Parse).ToArray(); return hp.SetQuickAccessAsync(b[0], b[1]); });

        Console.WriteLine($"\n{pass} passed, {fail} failed");
    }
}
