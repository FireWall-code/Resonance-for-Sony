using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Resonance.Core.Localization;

/// <summary>
/// UI strings, loaded from the embedded Localization/&lt;language&gt;.json files.
/// English is the reference: a key missing from a translation falls back to it.
/// To add a language, copy en.json to xx.json (ISO 639-1 code) and translate the values.
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> _strings = new(), _english = new();

    /// <summary>Language actually in use, e.g. "en" or "fr".</summary>
    public static string Language { get; private set; } = "en";

    /// <summary>Languages shipped with Resonance (code, native name).</summary>
    public static IReadOnlyList<(string Code, string Name)> Available { get; } =
        Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix) && n.EndsWith(".json"))
            .Select(n => n[Prefix.Length..^5])
            .Select(code => (code, CultureInfo.GetCultureInfo(code).NativeName))
            .Select(t => (t.code, char.ToUpper(t.NativeName[0]) + t.NativeName[1..]))
            .OrderBy(t => t.code).ToList();

    private const string Prefix = "Resonance.Core.Localization.";

    static Loc() => Initialize(Settings.Language);

    /// <summary>
    /// Selects the language: <paramref name="language"/> when given ("en", "fr"…),
    /// else the Windows display language, else English.
    /// </summary>
    public static void Initialize(string? language = null)
    {
        _english = Read("en") ?? new();
        var wanted = string.IsNullOrWhiteSpace(language) || language == "auto"
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : language;
        var strings = Read(wanted);
        Language = strings is null ? "en" : wanted;
        _strings = strings ?? _english;
    }

    public static string T(string key) =>
        _strings.TryGetValue(key, out var s) || _english.TryGetValue(key, out s) ? s : key;

    public static string T(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    private static Dictionary<string, string>? Read(string code)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{Prefix}{code}.json");
        return stream is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
    }

    /// <summary>The "Language" entry of %LOCALAPPDATA%\Resonance\settings.json, shared by the app and the CLI.</summary>
    public static class Settings
    {
        public static string FilePath { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resonance", "settings.json");

        public static string? Language
        {
            get
            {
                try
                {
                    if (!File.Exists(FilePath)) return null;
                    using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                    return doc.RootElement.TryGetProperty("Language", out var l) ? l.GetString() : null;
                }
                catch (Exception ex) when (ex is IOException or JsonException) { return null; }
            }
        }
    }
}
