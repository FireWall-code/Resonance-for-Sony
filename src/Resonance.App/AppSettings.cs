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

    /// <summary>
    /// Disconnect from the headphones when Resonance has not been used for <see cref="IdleMinutes"/>, so the
    /// phone app can connect (the headphones accept one control connection at a time).
    /// </summary>
    public bool ReleaseWhenIdle { get; set; } = true;
    public int IdleMinutes { get; set; } = 3;

    /// <summary>Show the animated card above the notification area when headphones connect.</summary>
    public bool ShowConnectionCard { get; set; } = true;

    /// <summary>The user's own picture of their headphones (a copy in %LOCALAPPDATA%\Resonance), or null for the drawing.</summary>
    public string? DevicePicture { get; set; }

    /// <summary>A folder of numbered PNG frames played when headphones connect, or null.</summary>
    public string? DeviceAnimation { get; set; }

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
