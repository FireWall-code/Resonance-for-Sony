using System.Text.Json;
using System.Text.Json.Serialization;
using Resonance.Core.Device;

/// <summary>resonance-cli replay &lt;folder&gt;: the state Resonance decodes from a recorded session.</summary>
static class Replay
{
    public static string Run(string folder) =>
        JsonSerializer.Serialize(Headphones.Replay(Recording.Load(folder)), new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
}
