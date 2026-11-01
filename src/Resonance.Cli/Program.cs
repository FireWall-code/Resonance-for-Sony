using Resonance.Core.Device;
using Resonance.Core.Protocol;
using Resonance.Core.Transport;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Everyday commands (etat, nc, ambiant 12, eq puissant…) are handled by CommandRunner,
// through the running app when there is one. The commands below are protocol tools:
// resonance-cli list
// resonance-cli scan [address]          query every supported function (GET capability/status/param)
// resonance-cli send <1|2> <hex> [address]
// resonance-cli listen [address]        print notifications until Ctrl+C
string[] toolCommands = ["list", "scan", "deepscan", "send", "listen", "test", "device", "state"];
var cmd = args.Length > 0 ? args[0] : "etat";
if (!toolCommands.Contains(cmd))
{
    Environment.ExitCode = await UserCommand.RunAsync(args);
    return;
}
if (await ControlPipe.TrySendAsync(["__ping"], TimeSpan.FromSeconds(2)) != null)
{
    Console.Error.WriteLine(Resonance.Core.Localization.Loc.T("cli.toolsbusy"));
    Environment.ExitCode = 4;
    return;
}
var devices = await MdrConnection.FindPairedAsync();
if (cmd == "list")
{
    foreach (var d in devices) Console.WriteLine($"{d.Name}  {d.Address:X12}  {(d.V2 ? "MDR v2" : "MDR v1")}");
    return;
}
if (cmd == "device")
{
    // resonance-cli device <connect|disconnect|forget> <AA:BB:CC:DD:EE:FF>
    await using var hp = await Headphones.ConnectAsync(devices.First().Address);
    var action = args[1] switch { "connect" => DeviceAction.Connect, "disconnect" => DeviceAction.Disconnect, _ => DeviceAction.Forget };
    var done = new TaskCompletionSource();
    hp.StateChanged += () =>
    {
        if (hp.State.LastDeviceAction is { } r && r.Address == args[2].ToUpperInvariant())
        {
            Console.WriteLine($"{r.Action}: {(r.InProgress ? "in progress…" : r.Success ? "done" : "failed")}");
            if (!r.InProgress) done.TrySetResult();
        }
    };
    await hp.SendDeviceActionAsync(action, args[2]);
    if (await Task.WhenAny(done.Task, Task.Delay(30000)) != done.Task) Console.WriteLine("no final answer within 30 s");
    return;
}
if (cmd is "state" or "test")
{
    var addr = args.ElementAtOrDefault(1) is { } a ? Convert.ToUInt64(a.Replace(":", ""), 16)
        : DeviceMemory.Last ?? devices.FirstOrDefault().Address;
    await using var hp = await Headphones.ConnectAsync(addr);
    var json = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
    string Dump() => System.Text.Json.JsonSerializer.Serialize(hp.State, json);
    if (cmd == "state") { Console.WriteLine(Dump()); return; }
    await Tests.RunAsync(hp, args.Skip(2).ToArray());
    return;
}
string? addrArg = cmd switch { "send" => args.ElementAtOrDefault(3), _ => args.ElementAtOrDefault(1) };
ulong address = addrArg != null ? Convert.ToUInt64(addrArg.Replace(":", ""), 16)
              : devices.FirstOrDefault().Address;
if (address == 0) { Console.Error.WriteLine("No MDR headphones paired."); return; }

await using var conn = await MdrConnection.ConnectAsync(address);
Console.WriteLine($"Connected to {conn.DeviceName} ({(conn.IsV2 ? "MDR v2" : "MDR v1")})");
conn.MessageReceived += m => Console.WriteLine($"  <- T{(m.Table == DataType.DataMdrNo2 ? 2 : 1)} {Convert.ToHexString(m.Payload)}");

async Task<MdrMessage?> Try(DataType t, params byte[] p)
{
    try { return await conn.RequestAsync(t, p, m => m.Table == t && m.Command == (byte)(p[0] + 1), TimeSpan.FromMilliseconds(900)); }
    catch (TimeoutException) { Console.WriteLine($"  (no answer to {Convert.ToHexString(p)})"); return null; }
}

switch (cmd)
{
    case "send":
        var table = args[1] == "2" ? DataType.DataMdrNo2 : DataType.DataMdrNo1;
        await conn.SendAsync(table, Convert.FromHexString(args[2]));
        await Task.Delay(1500);
        break;
    case "deepscan":
    {
        // Tries every known inquired type of each command group (GET only: changes nothing).
        var groups = new (DataType T, byte Group, byte[] Inquired, byte[] Gets)[]
        {
            (DataType.DataMdrNo1, 0x10, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09], [0x10, 0x12]),
            (DataType.DataMdrNo1, 0x20, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0E, 0x0F], [0x20, 0x22, 0x26]),
            (DataType.DataMdrNo1, 0x40, [0x02, 0x0B, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE], [0x40, 0x42, 0x46, 0x4A]),
            (DataType.DataMdrNo1, 0x50, [0x00, 0x01, 0x02, 0x03, 0x04, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x40], [0x50, 0x52, 0x56, 0x5A]),
            (DataType.DataMdrNo1, 0x60, [0x01, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x21, 0x22, 0x30, 0x40], [0x60, 0x62, 0x66]),
            (DataType.DataMdrNo1, 0x70, [0x00, 0x01], [0x70, 0x72, 0x76, 0x7A]),
            (DataType.DataMdrNo1, 0x80, [0x00, 0x01, 0x02, 0x30, 0x31], [0x80, 0x82, 0x86]),
            (DataType.DataMdrNo1, 0x90, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06], [0x90, 0x92]),
            (DataType.DataMdrNo1, 0xA0, [0x01, 0x02, 0x03, 0x20, 0x21, 0x30, 0x31, 0x40], [0xA0, 0xA2, 0xA6]),
            (DataType.DataMdrNo1, 0xB0, [0x00, 0x01, 0x02, 0x10, 0x20, 0x21], [0xB0, 0xB2, 0xB6]),
            (DataType.DataMdrNo1, 0xD0, [0xD1, 0xD2, 0xD3, 0xD4], [0xD0, 0xD2, 0xD6]),
            (DataType.DataMdrNo1, 0xE0, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B], [0xE0, 0xE2, 0xE6]),
            (DataType.DataMdrNo1, 0xF0, [0x00, 0x02, 0x03, 0x04, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10], [0xF0, 0xF2, 0xF6, 0xFA]),
            (DataType.DataMdrNo2, 0x20, [0x00, 0x01, 0x02], [0x20, 0x22, 0x26]),
            (DataType.DataMdrNo2, 0x30, [0x00, 0x01, 0x02, 0x03], [0x30, 0x32, 0x36]),
            (DataType.DataMdrNo2, 0x40, [0x00, 0x01, 0x02, 0x03, 0x20, 0x21, 0x30, 0x31, 0x32], [0x40, 0x42, 0x46, 0x4A]),
            (DataType.DataMdrNo2, 0x50, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05], [0x50, 0x52, 0x56, 0x5A]),
            (DataType.DataMdrNo2, 0x60, [0x02, 0xFE], [0x60, 0x62, 0x66]),
            (DataType.DataMdrNo2, 0xF0, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11], [0xF0, 0xF2, 0xF6, 0xFA]),
        };
        await Try(DataType.DataMdrNo1, 0x00, 0x00);
        foreach (var g in groups)
        {
            foreach (var value in g.Inquired)
            {
                Console.WriteLine($"# T{(g.T == DataType.DataMdrNo2 ? 2 : 1)} group 0x{g.Group:X2} type 0x{value:X2}");
                foreach (var get in g.Gets)
                {
                    try { await conn.RequestAsync(g.T, [get, value], m => m.Table == g.T && m.Command == (byte)(get + 1), TimeSpan.FromMilliseconds(450)); }
                    catch (TimeoutException) { }
                }
            }
        }
        break;
    }
    case "listen":
        await Try(DataType.DataMdrNo1, 0x00, 0x00);
        Console.WriteLine("Listening to notifications (Ctrl+C to quit)...");
        await Task.Delay(Timeout.Infinite);
        break;
    default:
        Console.WriteLine("# Protocol / device");
        await Try(DataType.DataMdrNo1, 0x00, 0x00);
        await Try(DataType.DataMdrNo1, 0x02, 0x00);
        for (byte i = 0; i < 4; i++) await Try(DataType.DataMdrNo1, 0x04, i);
        var functions = new List<(DataType Table, byte Id)>();
        foreach (var (t, label) in new[] { (DataType.DataMdrNo1, "T1"), (DataType.DataMdrNo2, "T2") })
        {
            Console.WriteLine($"# Supported functions {label}");
            var r = await Try(t, 0x06, 0x00);
            // RET_SUPPORT_FUNCTION: 07 00 <count> then (functionId, version) pairs
            if (r is { Payload.Length: > 3 })
                for (int i = 3; i + 1 < r.Payload.Length; i += 2) functions.Add((t, r.Payload[i]));
        }
        foreach (var (t, id) in functions)
        {
            byte group = (byte)(id & 0xF0);
            if (group is 0x00 or 0x30 && t == DataType.DataMdrNo1) continue; // connect / firmware update
            Console.WriteLine($"# T{(t == DataType.DataMdrNo2 ? 2 : 1)} fonction 0x{id:X2}");
            await Try(t, group, id);
            await Try(t, (byte)(group | 2), id);
            await Try(t, (byte)(group | 6), id);
        }
        break;
}


