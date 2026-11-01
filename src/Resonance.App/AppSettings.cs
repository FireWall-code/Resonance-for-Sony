using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace Resonance.App;

/// <summary>User preferences, stored in %LOCALAPPDATA%\Resonance\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resonance", "settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public sealed record HotkeyEntry(ModifierKeys Modifiers, Key Key);

    /// <summary>"auto" (Windows display language) or a language code such as "en" or "fr".</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Action name ("nc", "ambient", "off", "toggle") → key combination.</summary>
    public Dictionary<string, HotkeyEntry> Hotkeys { get; set; } = new();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException) { /* corrupt or locked: start clean */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (IOException) { /* best effort */ }
    }
}
