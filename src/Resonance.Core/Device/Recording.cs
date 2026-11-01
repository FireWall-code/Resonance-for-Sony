using Resonance.Core.Protocol;
using Resonance.Core.Transport;

namespace Resonance.Core.Device;

/// <summary>
/// A recorded session: a folder of .bin files holding one escaped frame each, read in file name order.
/// Files whose name contains ".tx." are frames the host sent and are skipped.
/// </summary>
public static class Recording
{
    public static IEnumerable<MdrMessage> Load(string folder)
    {
        foreach (var file in Directory.GetFiles(folder, "*.bin").Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).Contains(".tx.")) continue;
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length < 2 || bytes[0] != MdrFrame.Start || bytes[^1] != MdrFrame.End) continue;
            if (MdrFrame.Decode(bytes.AsSpan(1, bytes.Length - 2)) is { } f && f.Type is DataType.DataMdrNo1 or DataType.DataMdrNo2)
                yield return new MdrMessage(f.Type, f.Payload);
        }
    }
}
