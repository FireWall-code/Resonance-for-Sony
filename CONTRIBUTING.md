# Contributing to Resonance

Thanks for helping! The three most useful contributions are translations, support for more
headphones, and bug reports.

## Translate Resonance

All texts live in `src/Resonance.Core/Localization/`:

1. Copy `en.json` to `xx.json`, where `xx` is the two-letter ISO 639-1 code of your language (`de`, `es`, `ja`…).
2. Translate the values, not the keys. Keep `{0}`, `{1}`… placeholders and `\n` line breaks.
3. Build and run: Resonance picks the Windows display language, or choose it in Settings → Language.

Missing keys fall back to English, so a partial translation is fine to start with.

## Add support for your headphones

Resonance was verified on a WF-1000XM6. Other Sound Connect models use the same protocol but
different feature variants. To help support yours:

1. Pair and connect the headphones to your PC, and quit the Resonance app.
2. Run `resonance-cli deepscan > scan.txt`. It only **reads** settings and changes nothing.
3. Open an issue with the **New headphones** template and attach `scan.txt`, plus the model name
   and firmware version shown in Sound Connect.

If you can build from source, `resonance-cli test` changes each supported setting, waits for the
headphones' confirmation, then restores it, and prints a PASS / FAIL report.

### Check a recorded session without the headphones

A recorded session is a folder of `.bin` files, one MDR frame each (the format of
[SonyHeadphonesClient's captures](https://github.com/mos9527/SonyHeadphonesClient/tree/main/tests)).

- `resonance-cli replay <folder>` prints the state Resonance decodes from it, as JSON.
- `Resonance.exe --replay <folder>` opens the app on that session: every page shows what that model
  supports, and changes are refused. Quit the running Resonance first.

## Report a bug

Include your headphones model and firmware, your Windows version, what you did and what happened.
`resonance-cli json` prints the full state the app reads from the headphones.

## Code

To refresh the README images, run the app in English with your headphones connected, then:

```text
resonance-cli __screenshot <repo>/docs/images dark
resonance-cli __screenshot <repo>/docs/images/light light
powershell -ExecutionPolicy Bypass -File docs/make-readme-art.ps1
```

Every page is rendered off-screen (the window does not appear), with sample track, device and battery values and
never your own picture or animation.

- C# / .NET 10, WPF with the Fluent theme. Match the style of the surrounding code.
- Protocol changes belong in `src/Resonance.Core`; document new commands in `docs/PROTOCOL.md` with
  their evidence level.
- Never commit code, images or text copied from Sony's apps.
