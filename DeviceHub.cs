using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AstroDeviceHub;

public enum DeviceKind { Caa, Focuser, CoverCalibrator, FilterWheel }
public enum TransportKind { Serial, Tcp }

public sealed record DeviceRequest(string Name, DeviceKind Kind, TransportKind Transport, string Endpoint, int? Port, int? BaudRate);
public sealed record DeviceCommand(string Action, string ClientId, int? Value = null, string? Payload = null);
public sealed record DeviceLeaseRequest(string ClientId);
public sealed record DeviceSnapshot(string Id, string Name, DeviceKind Kind, int AscomSlot, TransportKind Transport, string Endpoint, string Status, string? Firmware, string? LastResponse, IReadOnlyCollection<string> Clients, int ClientLimit);

public sealed class DeviceHub : IDisposable
{
    internal const int DevicesPerKindLimit = 3;
    private static readonly IReadOnlyDictionary<DeviceKind, int> Limits = new Dictionary<DeviceKind, int>
    {
        [DeviceKind.Caa] = 2,
        [DeviceKind.Focuser] = 1,
        [DeviceKind.CoverCalibrator] = 1,
        [DeviceKind.FilterWheel] = 1
    };

    private readonly ConcurrentDictionary<string, ManagedDevice> _devices = new();
    private readonly object _configurationGate = new();
    private readonly string _configPath;

    public DeviceHub()
    {
        _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDeviceHub", "devices.json");
        Load();
    }

    public static IEnumerable<string> ListPorts() => SerialPort.GetPortNames().OrderBy(x => x);
    public IEnumerable<DeviceSnapshot> List() => _devices.Values.OrderBy(x => x.Kind).ThenBy(x => x.AscomSlot).Select(x => x.Snapshot());
    public object ServerStatus()
    {
        var snapshots = List().ToArray();
        return new
        {
            state = "running",
            deviceCount = snapshots.Length,
            connectedDeviceCount = snapshots.Count(x => x.Clients.Count > 0),
            activeClientCount = snapshots.Sum(x => x.Clients.Count),
            startedAt = System.Diagnostics.Process.GetCurrentProcess().StartTime,
            processId = Environment.ProcessId
        };
    }

    public IResult Add(DeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Endpoint))
            return Results.BadRequest(new { message = "设备名称和连接地址不能为空。" });
        ManagedDevice device;
        lock (_configurationGate)
        {
            var slot = NextAvailableSlot(_devices.Values.Where(x => x.Kind == request.Kind).Select(x => x.AscomSlot));
            if (slot is null)
                return Results.BadRequest(new { message = $"每类设备最多添加 {DevicesPerKindLimit} 台。请移除现有设备后再添加。" });
            var id = Guid.NewGuid().ToString("N")[..8];
            device = new ManagedDevice(id, request, Limits[request.Kind], slot.Value);
            _devices.TryAdd(id, device);
        }
        Save();
        return Results.Created($"/api/devices/{device.Id}", device.Snapshot());
    }

    public IResult Remove(string id)
    {
        if (!_devices.TryRemove(id, out var device)) return Results.NotFound();
        device.Dispose();
        Save();
        return Results.NoContent();
    }

    public IResult FindByKind(DeviceKind kind)
    {
        var device = _devices.Values.Where(x => x.Kind == kind).OrderBy(x => x.AscomSlot).FirstOrDefault();
        return device is null ? Results.NotFound(new { message = $"尚未登记 {kind} 设备。" }) : Results.Ok(device.Snapshot());
    }

    public IResult FindByKindAndSlot(DeviceKind kind, int slot)
    {
        if (slot is < 1 or > DevicesPerKindLimit) return Results.BadRequest(new { message = "设备槽位必须介于 Device1 和 Device3。" });
        var device = _devices.Values.FirstOrDefault(x => x.Kind == kind && x.AscomSlot == slot);
        return device is null
            ? Results.NotFound(new { message = $"尚未配置 {kind}-Device{slot}。请先在 Astro Device Hub 中添加该设备。" })
            : Results.Ok(device.Snapshot());
    }

    public async Task<IResult> ConnectAsync(string id, DeviceLeaseRequest lease)
    {
        if (!_devices.TryGetValue(id, out var device)) return Results.NotFound();
        try
        {
            await device.ConnectAsync(lease.ClientId);
            return Results.Ok(device.Snapshot());
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    public IResult Disconnect(string id, DeviceLeaseRequest lease)
    {
        if (!_devices.TryGetValue(id, out var device)) return Results.NotFound();
        device.Disconnect(lease.ClientId);
        return Results.Ok(device.Snapshot());
    }

    public async Task<IResult> CommandAsync(string id, DeviceCommand command)
    {
        if (!_devices.TryGetValue(id, out var device)) return Results.NotFound();
        try
        {
            var response = await device.SendAsync(command);
            return Results.Ok(new { response, device = device.Snapshot() });
        }
        catch (InvalidOperationException ex) { return Results.Conflict(new { message = ex.Message }); }
        catch (Exception ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway); }
    }

    public async Task<IResult> StageFirmwareAsync(string id, IFormFile file)
    {
        if (!_devices.TryGetValue(id, out var device)) return Results.NotFound();
        var result = await FirmwareService.StoreAsync(file, device.Kind);
        return result.Ok
            ? Results.Ok(new { message = "固件包已校验并归档。刷写适配器将在对应设备进入引导模式后执行。", result.FileName, result.Sha256 })
            : Results.BadRequest(new { message = result.Message });
    }

    public async Task<IResult> FlashFirmwareAsync(string id, IFormFile file, string target, string port, bool motorPowerDisconnected)
    {
        if (!_devices.TryGetValue(id, out var device)) return Results.NotFound();
        if (device.Snapshot().Clients.Count > 0) return Results.Conflict(new { message = "请先断开该设备的所有应用客户端。" });
        if (device.Kind == DeviceKind.Caa && !motorPowerDisconnected)
            return Results.BadRequest(new { message = "刷写 CAA 前必须确认已断开 12V 电机电源。" });
        var result = await FirmwareService.FlashAsync(file, device.Kind, target, port);
        return result.Ok ? Results.Ok(result) : Results.BadRequest(new { message = result.Message, result.Output });
    }

    public void Dispose() { foreach (var device in _devices.Values) device.Dispose(); }

    private void Load()
    {
        if (!File.Exists(_configPath)) return;
        try
        {
            var items = JsonSerializer.Deserialize<List<StoredDevice>>(File.ReadAllText(_configPath), JsonOptions()) ?? new();
            var occupied = new Dictionary<DeviceKind, HashSet<int>>();
            foreach (var item in items.OrderBy(x => x.Id))
            {
                if (!occupied.TryGetValue(item.Request.Kind, out var slots))
                    occupied[item.Request.Kind] = slots = new HashSet<int>();
                var slot = item.AscomSlot is >= 1 and <= DevicesPerKindLimit && !slots.Contains(item.AscomSlot)
                    ? item.AscomSlot
                    : NextAvailableSlot(slots) ?? 0;
                if (slot == 0) continue;
                slots.Add(slot);
                _devices[item.Id] = new ManagedDevice(item.Id, item.Request, Limits[item.Request.Kind], slot);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"无法加载设备配置 {_configPath}: {ex.Message}");
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var items = _devices.Values.Select(x => new StoredDevice(x.Id, x.Request, x.AscomSlot)).OrderBy(x => x.Id).ToArray();
        var temporary = _configPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(items, JsonOptions()));
        File.Move(temporary, _configPath, true);
    }

    private static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    internal static int? NextAvailableSlot(IEnumerable<int> occupiedSlots)
    {
        var occupied = new HashSet<int>(occupiedSlots);
        foreach (var slot in Enumerable.Range(1, DevicesPerKindLimit))
            if (!occupied.Contains(slot)) return slot;
        return null;
    }

    private sealed record StoredDevice(string Id, DeviceRequest Request, int AscomSlot = 0);
}

internal sealed class ManagedDevice : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DeviceRequest _request;
    private IDeviceTransport? _transport;
    private readonly ClientLeaseSet _clients;
    private string _status = "未连接";
    private string? _firmware;
    private string? _lastResponse;

    public string Id { get; }
    public string Name => _request.Name;
    public DeviceKind Kind => _request.Kind;
    public int AscomSlot { get; }
    public DeviceRequest Request => _request;
    public ManagedDevice(string id, DeviceRequest request, int clientLimit, int ascomSlot) { Id = id; _request = request; _clients = new ClientLeaseSet(clientLimit); AscomSlot = ascomSlot; }

    public DeviceSnapshot Snapshot() => new(Id, Name, Kind, AscomSlot, _request.Transport, _request.Endpoint, _status, _firmware, _lastResponse, _clients.Snapshot(), _clients.Limit);
    public async Task ConnectAsync(string clientId)
    {
        await _gate.WaitAsync();
        try
        {
            _clients.Acquire(clientId);
            if (_transport?.IsConnected == true) return;
            _transport?.Dispose();
            _transport = _request.Transport == TransportKind.Tcp
                ? new TcpDeviceTransport(_request.Endpoint, _request.Port ?? 4030)
                : new SerialDeviceTransport(_request.Endpoint, _request.BaudRate ?? DefaultBaud(Kind));
            await _transport.ConnectAsync();
            _status = "已连接";
            _firmware = await SendRawAsync(Protocol.Version(Kind));
        }
        catch
        {
            _clients.Release(clientId);
            _transport?.Dispose();
            _transport = null;
            _status = "连接失败";
            throw;
        }
        finally { _gate.Release(); }
    }

    public void Disconnect(string clientId)
    {
        _gate.Wait();
        try
        {
            _clients.Release(clientId);
            if (_clients.Count > 0) return;
            _transport?.Dispose(); _transport = null; _status = "未连接";
        }
        finally { _gate.Release(); }
    }
    public async Task<string> SendAsync(DeviceCommand command)
    {
        await _gate.WaitAsync();
        try
        {
            if (_transport?.IsConnected != true) throw new InvalidOperationException("请先连接设备。");
            if (!_clients.Contains(command.ClientId))
                throw new InvalidOperationException("应用客户端尚未取得该设备的连接租约。");
            var response = await SendRawAsync(Protocol.Command(Kind, command));
            return response;
        }
        finally { _gate.Release(); }
    }
    private async Task<string> SendRawAsync(string command)
    {
        if (_transport is null) throw new InvalidOperationException("设备连接不可用。");
        _lastResponse = await _transport.SendAsync(command, Protocol.Terminator(Kind));
        return _lastResponse;
    }
    private static int DefaultBaud(DeviceKind kind) => kind == DeviceKind.FilterWheel ? 57600 : kind == DeviceKind.CoverCalibrator ? 9600 : 9600;
    public void Dispose() { _clients.Clear(); _transport?.Dispose(); _transport = null; _gate.Dispose(); }
}

internal sealed class ClientLeaseSet
{
    private readonly object _sync = new();
    private readonly HashSet<string> _clients = new(StringComparer.OrdinalIgnoreCase);
    internal ClientLeaseSet(int limit) { if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit)); Limit = limit; }
    internal int Limit { get; }
    internal int Count { get { lock (_sync) return _clients.Count; } }
    internal void Acquire(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("应用客户端标识不能为空。", nameof(clientId));
        lock (_sync)
        {
            if (_clients.Contains(clientId)) return;
            if (_clients.Count >= Limit) throw new InvalidOperationException($"该设备已达到 {Limit} 个应用客户端的连接上限。当前连接：{string.Join("、", _clients)}。");
            _clients.Add(clientId);
        }
    }
    internal bool Contains(string clientId) { if (string.IsNullOrWhiteSpace(clientId)) return false; lock (_sync) return _clients.Contains(clientId); }
    internal void Release(string clientId) { lock (_sync) _clients.Remove(clientId); }
    internal string[] Snapshot() { lock (_sync) return _clients.OrderBy(x => x).ToArray(); }
    internal void Clear() { lock (_sync) _clients.Clear(); }
}

internal static class Protocol
{
    public static string Terminator(DeviceKind kind) => kind switch { DeviceKind.CoverCalibrator => ">", DeviceKind.FilterWheel => "\n", _ => "#" };
    public static string Version(DeviceKind kind) => kind switch { DeviceKind.CoverCalibrator => "<V>", DeviceKind.FilterWheel => "ID?\n", _ => "V#" };
    public static string Command(DeviceKind kind, DeviceCommand input) => kind switch
    {
        DeviceKind.Caa => input.Action switch { "status" => "G#", "home" => "H#", "halt" => "S#", "move" => $"M {Required(input.Value)}#", "set-zero" => $"P {Required(input.Value)}#", "reverse" => $"R {Required(input.Value)}#", "raw" => Raw(input.Payload, "#"), _ => throw Unknown(input.Action) },
        DeviceKind.Focuser => input.Action switch { "status" => "G#", "info" => "I#", "home" => "H#", "halt" => "S#", "move" => $"M {Required(input.Value)}#", "set-zero" => $"P {Required(input.Value)}#", "raw" => Raw(input.Payload, "#"), _ => throw Unknown(input.Action) },
        DeviceKind.CoverCalibrator => input.Action switch { "status" => "<P>", "cover-state" => "<P>", "calibrator-state" => "<L>", "brightness-get" => "<B>", "max-brightness" => "<M>", "open" => "<O>", "close" => "<C>", "halt" => "<H>", "brightness" => $"<T{Required(input.Value)}>", "light-off" => "<F>", "raw" => Raw(input.Payload, ">", "<"), _ => throw Unknown(input.Action) },
        DeviceKind.FilterWheel => input.Action switch { "status" => "STATE?\n", "home" => "HOME\n", "halt" => "STOP\n", "position" => "POS?\n", "goto" => $"GOTO {Required(input.Value)}\n", "raw" => Raw(input.Payload, "\n"), _ => throw Unknown(input.Action) },
        _ => throw Unknown(input.Action)
    };
    private static int Required(int? value) => value ?? throw new ArgumentException("该操作需要数值参数。");
    private static string Raw(string? payload, string suffix, string prefix = "")
    {
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("原始命令不能为空。");
        var value = payload.Trim();
        if (!string.IsNullOrEmpty(prefix) && !value.StartsWith(prefix, StringComparison.Ordinal)) value = prefix + value;
        if (!value.EndsWith(suffix, StringComparison.Ordinal)) value += suffix;
        return value;
    }
    private static ArgumentException Unknown(string action) => new($"不支持的设备操作: {action}");
}

internal interface IDeviceTransport : IDisposable { bool IsConnected { get; } Task ConnectAsync(); Task<string> SendAsync(string command, string terminator); }
internal sealed class TcpDeviceTransport(string host, int port) : IDeviceTransport
{
    private TcpClient? _client; private NetworkStream? _stream;
    public bool IsConnected => _client?.Connected == true && _stream is not null;
    public async Task ConnectAsync() { _client = new TcpClient { NoDelay = true }; await _client.ConnectAsync(host, port); _stream = _client.GetStream(); }
    public async Task<string> SendAsync(string command, string terminator)
    {
        if (_stream is null) throw new InvalidOperationException("TCP 未连接。");
        var bytes = Encoding.ASCII.GetBytes(command); await _stream.WriteAsync(bytes);
        return await ReadUntilAsync(_stream, terminator);
    }
    public void Dispose() { _stream?.Dispose(); _client?.Dispose(); }
    internal static async Task<string> ReadUntilAsync(Stream stream, string terminator)
    {
        var result = new StringBuilder(); var one = new byte[1];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true) { var count = await stream.ReadAsync(one, timeout.Token); if (count == 0) throw new IOException("设备在返回响应前断开连接。"); result.Append((char)one[0]); if (result.ToString().EndsWith(terminator, StringComparison.Ordinal)) return result.ToString().Trim(); }
    }
}
internal sealed class SerialDeviceTransport(string portName, int baudRate) : IDeviceTransport
{
    private SerialPort? _port; public bool IsConnected => _port?.IsOpen == true;
    public Task ConnectAsync() { _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One) { ReadTimeout = 5000, WriteTimeout = 3000, DtrEnable = false, RtsEnable = false }; _port.Open(); _port.DiscardInBuffer(); return Task.CompletedTask; }
    public async Task<string> SendAsync(string command, string terminator)
    {
        if (_port?.BaseStream is null) throw new InvalidOperationException("串口未连接。");
        _port.DiscardInBuffer(); var bytes = Encoding.ASCII.GetBytes(command); await _port.BaseStream.WriteAsync(bytes); await _port.BaseStream.FlushAsync(); return await TcpDeviceTransport.ReadUntilAsync(_port.BaseStream, terminator);
    }
    public void Dispose() { _port?.Dispose(); }
}

public sealed class FirmwareService
{
    public async Task<IResult> ValidateAsync(IFormFile file)
    {
        var result = await StoreAsync(file, null);
        return result.Ok ? Results.Ok(result) : Results.BadRequest(new { message = result.Message });
    }
    public static async Task<FirmwareResult> StoreAsync(IFormFile? file, DeviceKind? kind)
    {
        if (file is null || file.Length == 0) return FirmwareResult.Fail("请选择固件文件。");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".bin" and not ".hex") return FirmwareResult.Fail("仅支持 .bin 或 .hex 固件包。");
        var directory = Path.Combine(AppContext.BaseDirectory, "firmware-packages"); Directory.CreateDirectory(directory);
        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileName(file.FileName)}";
        var target = Path.Combine(directory, safeName);
        await using (var output = File.Create(target)) await file.CopyToAsync(output);
        await using var input = File.OpenRead(target);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input));
        return new FirmwareResult(true, safeName, hash, kind?.ToString(), null, null, target);
    }

    public static async Task<FirmwareResult> FlashAsync(IFormFile? file, DeviceKind kind, string target, string port)
    {
        if (string.IsNullOrWhiteSpace(port) || !Regex.IsMatch(port, "^COM[1-9][0-9]{0,2}$", RegexOptions.IgnoreCase))
            return FirmwareResult.Fail("请选择有效的 COM 串口。");
        var uploadPort = port.ToUpperInvariant();

        var stored = await StoreAsync(file, kind);
        if (!stored.Ok || stored.StoredPath is null) return stored;

        var extension = Path.GetExtension(stored.StoredPath).ToLowerInvariant();
        var normalizedTarget = (target ?? string.Empty).Trim();
        string fqbn;
        string? boardOption = null;
        switch (normalizedTarget.ToLowerInvariant())
        {
            case "nano": fqbn = "arduino:avr:nano"; break;
            case "nanooldbootloader": fqbn = "arduino:avr:nano"; boardOption = "cpu=atmega328old"; break;
            case "esp8266": fqbn = "esp8266:esp8266:d1_mini"; break;
            default: return FirmwareResult.Fail("请选择 Arduino Nano、Nano Old Bootloader 或 ESP8266 目标。");
        }

        if (normalizedTarget.StartsWith("Nano", StringComparison.OrdinalIgnoreCase) && extension != ".hex")
            return FirmwareResult.Fail("Arduino Nano 刷写需要 .hex 固件文件。");
        if (normalizedTarget.Equals("Esp8266", StringComparison.OrdinalIgnoreCase) && extension != ".bin")
            return FirmwareResult.Fail("ESP8266 刷写需要 .bin 固件文件。");

        var cli = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArduinoCLI", "arduino-cli.exe");
        if (!File.Exists(cli)) return FirmwareResult.Fail("未找到 Arduino CLI。请先安装 Arduino CLI 与对应的 AVR/ESP8266 Core。");

        var start = new ProcessStartInfo
        {
            FileName = cli,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("upload");
        start.ArgumentList.Add("--fqbn"); start.ArgumentList.Add(fqbn);
        start.ArgumentList.Add("--port"); start.ArgumentList.Add(uploadPort);
        start.ArgumentList.Add("--input-file"); start.ArgumentList.Add(stored.StoredPath);
        start.ArgumentList.Add("--verify");
        start.ArgumentList.Add("--no-color");
        if (boardOption is not null) { start.ArgumentList.Add("--board-options"); start.ArgumentList.Add(boardOption); }

        using var process = new Process { StartInfo = start };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            return FirmwareResult.Fail("固件刷写在两分钟后超时。", await stderr);
        }
        var output = (await stdout) + Environment.NewLine + (await stderr);
        return process.ExitCode == 0
            ? stored with { Message = "固件刷写与校验完成。", Output = output }
            : FirmwareResult.Fail("Arduino CLI 刷写失败。", output);
    }
}
public sealed record FirmwareResult(bool Ok, string? FileName, string? Sha256, string? DeviceKind, string? Message, string? Output = null, [property: JsonIgnore] string? StoredPath = null)
{ public static FirmwareResult Fail(string message, string? output = null) => new(false, null, null, null, message, output); }
