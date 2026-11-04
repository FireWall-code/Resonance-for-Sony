using System.Threading.Channels;
using Resonance.Core.Localization;
using Resonance.Core.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;

namespace Resonance.Core.Transport;

/// <summary>A command received from the headphones: the table it came from and its payload.</summary>
public sealed record MdrMessage(DataType Table, byte[] Payload)
{
    public byte Command => Payload.Length > 0 ? Payload[0] : (byte)0xFF;
    public byte InquiredType => Payload.Length > 1 ? Payload[1] : (byte)0xFF;
    public override string ToString() => $"{Table} {Convert.ToHexString(Payload)}";
}

/// <summary>
/// RFCOMM link to the MDR service. Handles framing, ACKs and the alternating
/// sequence number; one data frame is in flight at a time, as the app does.
/// </summary>
public sealed class MdrConnection : IAsyncDisposable
{
    /// <summary>MDR v2 service (WF-1000XM4 and later, WH-1000XM5 and later).</summary>
    public static readonly Guid MdrV2Service = Guid.Parse("956C7B26-D49A-4BA8-B03F-B17D393CB6E2");
    /// <summary>MDR v1 service (older models).</summary>
    public static readonly Guid MdrV1Service = Guid.Parse("96CC203E-5068-46AD-B32D-E316F5E069BA");

    private readonly StreamSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly MdrFrameReader _reader = new();
    private readonly Channel<MdrMessage> _incoming = Channel.CreateUnbounded<MdrMessage>();
    private readonly CancellationTokenSource _cts = new();
    private Stream? _in, _out;
    private Task? _readLoop;
    // Kept so that closing really frees the headphones' control channel for other apps (Sound Connect).
    private BluetoothDevice? _device;
    private RfcommDeviceService? _service;
    private byte _txSeq;
    private TaskCompletionSource? _pendingAck;

    public ulong Address { get; }
    public string? DeviceName { get; private set; }
    public bool IsV2 { get; private set; }

    public event Action<MdrMessage>? MessageReceived;
    public event Action<Exception?>? Disconnected;
    public event Action<string>? Trace;

    private MdrConnection(ulong address) => Address = address;

    /// <summary>Lists paired devices that expose an MDR service.</summary>
    public static async Task<IReadOnlyList<(string Name, ulong Address, bool V2)>> FindPairedAsync()
    {
        var result = new List<(string, ulong, bool)>();
        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        foreach (var info in paired)
        {
            try
            {
                // The pairing list mixes classic and LE entries; resolve LE ones to the classic device by address.
                BluetoothDevice? dev;
                try { dev = await BluetoothDevice.FromIdAsync(info.Id); }
                catch (ArgumentException)
                {
                    // Association id: "Bluetooth#Bluetooth<local mac>-<device mac>".
                    var mac = info.Id[(info.Id.LastIndexOf('-') + 1)..].Replace(":", "");
                    if (mac.Length != 12 || !ulong.TryParse(mac, System.Globalization.NumberStyles.HexNumber, null, out var addr)
                        || result.Any(r => r.Item2 == addr)) continue;
                    dev = await BluetoothDevice.FromBluetoothAddressAsync(addr);
                }
                if (dev is null) continue;
                using var _ = dev;
                foreach (var (uuid, v2) in new[] { (MdrV2Service, true), (MdrV1Service, false) })
                {
                    var id = RfcommServiceId.FromUuid(uuid);
                    var r = await dev.GetRfcommServicesForIdAsync(id, BluetoothCacheMode.Cached);
                    // A live SDP query takes ~1-2 s: only for connected audio devices (not phones, watches…).
                    if (r.Services.Count == 0 && dev.ConnectionStatus == BluetoothConnectionStatus.Connected
                        && dev.ClassOfDevice.MajorClass == BluetoothMajorClass.AudioVideo)
                        r = await dev.GetRfcommServicesForIdAsync(id, BluetoothCacheMode.Uncached);
                    bool found = r.Services.Count > 0;
                    foreach (var s in r.Services) s.Dispose();
                    if (found) { result.Add((dev.Name, dev.BluetoothAddress, v2)); break; }
                }
            }
            catch (ArgumentException) { /* stale pairing entry */ }
        }
        return result;
    }

    /// <summary>
    /// Calls <paramref name="changed"/> when Windows reports that the headphones connected to or left this PC.
    /// No polling and no Bluetooth traffic. Dispose the result to stop. Null when the device is unknown.
    /// </summary>
    public static async Task<IDisposable?> WatchDeviceAsync(ulong address, Action<bool> changed)
    {
        BluetoothDevice? device;
        try { device = await BluetoothDevice.FromBluetoothAddressAsync(address); }
        catch (Exception) { return null; }
        if (device is null) return null;
        void OnChanged(BluetoothDevice d, object _) => changed(d.ConnectionStatus == BluetoothConnectionStatus.Connected);
        device.ConnectionStatusChanged += OnChanged;
        return new DeviceWatch(device, OnChanged);
    }

    private sealed class DeviceWatch(BluetoothDevice device, Windows.Foundation.TypedEventHandler<BluetoothDevice, object> handler) : IDisposable
    {
        public void Dispose()
        {
            device.ConnectionStatusChanged -= handler;
            device.Dispose();
        }
    }

    /// <summary>Whether Windows currently has a Bluetooth link with the headphones.</summary>
    public static async Task<bool> IsDeviceConnectedAsync(ulong address)
    {
        try
        {
            using var device = await BluetoothDevice.FromBluetoothAddressAsync(address);
            return device?.ConnectionStatus == BluetoothConnectionStatus.Connected;
        }
        catch (Exception) { return false; }
    }

    public static async Task<MdrConnection> ConnectAsync(ulong address, CancellationToken ct = default)
    {
        var conn = new MdrConnection(address);
        try { await conn.OpenAsync(ct); }
        catch { await conn.DisposeAsync(); throw; }
        return conn;
    }

    private async Task OpenAsync(CancellationToken ct)
    {
        var device = _device = await BluetoothDevice.FromBluetoothAddressAsync(Address)
                     ?? throw new InvalidOperationException(Loc.T("err.btnotfound"));
        DeviceName = device.Name;
        RfcommDeviceService? service = null;
        foreach (var (uuid, v2) in new[] { (MdrV2Service, true), (MdrV1Service, false) })
        {
            // Windows' SDP cache is instant; a live query (~0.5 s) only when the cache has nothing.
            var id = RfcommServiceId.FromUuid(uuid);
            var r = await device.GetRfcommServicesForIdAsync(id, BluetoothCacheMode.Cached);
            if (r.Services.Count == 0) r = await device.GetRfcommServicesForIdAsync(id, BluetoothCacheMode.Uncached);
            foreach (var extra in r.Services.Skip(1)) extra.Dispose();
            if (r.Services.Count > 0) { service = _service = r.Services[0]; IsV2 = v2; break; }
        }
        if (service is null)
            throw new InvalidOperationException(Loc.T("err.nomdr"));

        await _socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName,
            SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication).AsTask(ct);
        _in = _socket.InputStream.AsStreamForRead(0);
        _out = _socket.OutputStream.AsStreamForWrite(0);
        _readLoop = Task.Run(ReadLoopAsync);
    }

    private async Task ReadLoopAsync()
    {
        var buf = new byte[4096];
        Exception? error = null;
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                int n = await _in!.ReadAsync(buf, _cts.Token);
                if (n <= 0) break;
                foreach (var frame in _reader.Feed(buf.AsSpan(0, n)))
                    await HandleFrameAsync(frame);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { error = ex; }
        _incoming.Writer.TryComplete(error);
        Disconnected?.Invoke(error);
    }

    private async Task HandleFrameAsync(MdrFrame frame)
    {
        if (frame.Type == DataType.Ack)
        {
            Trace?.Invoke($"<- ACK seq={frame.Sequence}");
            _pendingAck?.TrySetResult();
            return;
        }
        // Every data frame is acknowledged with the opposite sequence number.
        var ack = new MdrFrame(DataType.Ack, (byte)(1 - frame.Sequence), Array.Empty<byte>()).Encode();
        await _out!.WriteAsync(ack);
        await _out.FlushAsync();
        var msg = new MdrMessage(frame.Type, frame.Payload);
        Trace?.Invoke($"<- {msg}");
        _incoming.Writer.TryWrite(msg);
        MessageReceived?.Invoke(msg);
    }

    /// <summary>Sends a command and waits for the headphones' ACK (retries twice).</summary>
    public async Task SendAsync(DataType table, byte[] payload, CancellationToken ct = default)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                _pendingAck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var bytes = new MdrFrame(table, _txSeq, payload).Encode();
                Trace?.Invoke($"-> {table} seq={_txSeq} {Convert.ToHexString(payload)}");
                await _out!.WriteAsync(bytes, ct);
                await _out.FlushAsync(ct);
                var done = await Task.WhenAny(_pendingAck.Task, Task.Delay(1000, ct));
                if (done == _pendingAck.Task)
                {
                    _txSeq ^= 1;
                    return;
                }
            }
            throw new TimeoutException(Loc.T("err.noack"));
        }
        finally { _sendLock.Release(); }
    }

    /// <summary>Sends a command and waits for the first reply matching <paramref name="match"/>.</summary>
    public async Task<MdrMessage> RequestAsync(DataType table, byte[] payload, Func<MdrMessage, bool> match,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<MdrMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(MdrMessage m) { if (match(m)) tcs.TrySetResult(m); }
        MessageReceived += Handler;
        try
        {
            await SendAsync(table, payload, ct);
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(3), ct));
            if (done != tcs.Task) throw new TimeoutException(Loc.T("err.noreply", Convert.ToHexString(payload)));
            return await tcs.Task;
        }
        finally { MessageReceived -= Handler; }
    }

    /// <summary>GET command → RET command (GET + 1) with the same inquired type.</summary>
    public Task<MdrMessage> GetAsync(DataType table, byte getCommand, params byte[] args) =>
        RequestAsync(table, [getCommand, .. args],
            m => m.Table == table && m.Command == (byte)(getCommand + 1) && (args.Length == 0 || m.InquiredType == args[0]));

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _in?.Dispose();
        _out?.Dispose();
        _socket.Dispose();
        if (_readLoop != null) { try { await _readLoop; } catch { } }
        // Without these, Windows keeps the RFCOMM service open and the phone app cannot connect.
        _service?.Dispose();
        _device?.Dispose();
    }
}
