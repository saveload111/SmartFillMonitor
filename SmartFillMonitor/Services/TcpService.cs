using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SmartFillMonitor.Services.Logs;

namespace SmartFillMonitor.Services;

/// <summary>
/// TCP 网络连接实例 — 每个连接一个实例，支持多设备
/// 用法：await using var conn = new TcpConnection();
/// </summary>
public sealed class TcpConnection : IAsyncDisposable
{
 
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly object _lock = new();

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync(string ip, int port, CancellationToken connectToken = default)
    {
        // 防并发重连：如果已连接，直接拒绝
        lock (_lock)
        {
            if (_client != null && IsConnected)
                throw new InvalidOperationException("已经连接，请先断开");
        }

        var newClient = new TcpClient();
        try
        {
            var connectTask = newClient.ConnectAsync(ip, port);

            if (await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, connectToken)) != connectTask)
            {
                newClient.Dispose();  // 连接超时或取消，释放资源
                throw new OperationCanceledException(connectToken);
            }

            await connectTask;
        }
        catch
        {
            newClient.Dispose();
            throw;
        }

        lock (_lock)
        {
            _client = newClient;
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();
            _receiveTask = ReceiveLoop(_cts.Token);
        }

        ConnectionChanged?.Invoke(this, true);
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        var buffer = new byte[1024];
        try
        {
            while (!token.IsCancellationRequested)
            {
                int count = await _stream!.ReadAsync(buffer, token);
                if (count > 0)
                    DataReceived?.Invoke(this, buffer[..count]);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消，不报错
        }
        catch (Exception ex)
        {
            LogService.Warn($"TCP 连接断开: {ex.Message}");
        }
        finally
        {
            // 唯一触发点，保证只通知一次
            ConnectionChanged?.Invoke(this, false);
        }
    }

    public async Task SendAsync(byte[] data)
    {
        var stream = _stream;
        if (stream == null) throw new InvalidOperationException("未连接");
        await stream.WriteAsync(data);
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();

        if (_receiveTask != null)
        {
            try { await _receiveTask; }
            catch { }
        }

        lock (_lock)
        {
            _stream?.Dispose();
            _client?.Dispose();
            _cts?.Dispose();
            _stream = null;
            _client = null;
        }
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());

 
  
}
