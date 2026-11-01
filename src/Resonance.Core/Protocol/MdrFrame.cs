namespace Resonance.Core.Protocol;

/// <summary>Data type byte of an MDR frame (TandemfamilyTableNumber in the Android app).</summary>
public enum DataType : byte
{
    Data = 0x00,
    Ack = 0x01,
    DataMcNo1 = 0x02,
    DataMdrNo1 = 0x0C,   // command table 1
    DataCommon = 0x0D,
    DataMdrNo2 = 0x0E,   // command table 2
}

/// <summary>
/// One MDR frame: 0x3E | type | seq | len (u32 BE) | payload | checksum | 0x3C,
/// where 0x3C/0x3D/0x3E inside the body are escaped as 0x3D, (b &amp; 0xEF).
/// </summary>
public sealed record MdrFrame(DataType Type, byte Sequence, byte[] Payload)
{
    public const byte Start = 0x3E, End = 0x3C, Escape = 0x3D;

    public byte[] Encode()
    {
        var body = new byte[6 + Payload.Length + 1];
        body[0] = (byte)Type;
        body[1] = Sequence;
        body[2] = (byte)(Payload.Length >> 24);
        body[3] = (byte)(Payload.Length >> 16);
        body[4] = (byte)(Payload.Length >> 8);
        body[5] = (byte)Payload.Length;
        Payload.CopyTo(body, 6);
        byte sum = 0;
        for (int i = 0; i < body.Length - 1; i++) sum += body[i];
        body[^1] = sum;

        var output = new List<byte>(body.Length + 8) { Start };
        foreach (var b in body)
        {
            if (b is Start or End or Escape) { output.Add(Escape); output.Add((byte)(b & 0xEF)); }
            else output.Add(b);
        }
        output.Add(End);
        return output.ToArray();
    }

    /// <summary>Decodes the bytes between Start and End (exclusive). Returns null on a bad checksum or length.</summary>
    public static MdrFrame? Decode(ReadOnlySpan<byte> escaped)
    {
        var body = new List<byte>(escaped.Length);
        for (int i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] == Escape && i + 1 < escaped.Length) body.Add((byte)(escaped[++i] | 0x10));
            else body.Add(escaped[i]);
        }
        if (body.Count < 7) return null;
        int len = (body[2] << 24) | (body[3] << 16) | (body[4] << 8) | body[5];
        if (len < 0 || body.Count != 6 + len + 1) return null;
        byte sum = 0;
        for (int i = 0; i < 6 + len; i++) sum += body[i];
        if (sum != body[^1]) return null;
        return new MdrFrame((DataType)body[0], body[1], body.GetRange(6, len).ToArray());
    }

    public override string ToString() => $"{Type} seq={Sequence} {Convert.ToHexString(Payload)}";
}

/// <summary>Splits a raw RFCOMM byte stream into frames.</summary>
public sealed class MdrFrameReader
{
    private readonly List<byte> _buffer = new();

    public IEnumerable<MdrFrame> Feed(ReadOnlySpan<byte> data)
    {
        _buffer.AddRange(data.ToArray());
        var frames = new List<MdrFrame>();
        while (true)
        {
            int start = _buffer.IndexOf(MdrFrame.Start);
            if (start < 0) { _buffer.Clear(); break; }
            int end = _buffer.IndexOf(MdrFrame.End, start + 1);
            if (end < 0) { _buffer.RemoveRange(0, start); break; }
            var frame = MdrFrame.Decode(_buffer.GetRange(start + 1, end - start - 1).ToArray());
            _buffer.RemoveRange(0, end + 1);
            if (frame != null) frames.Add(frame);
        }
        return frames;
    }
}
