using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// WebSocket 行情数据提供器
/// 连接到 WebSocket 行情服务器，订阅股票实时行情推送
/// 协议：JSON 格式 { code, price, change, prevClose, open, high, low, volume, time }
/// </summary>
public class WebSocketMarketDataProvider : IMarketDataProvider, IDisposable
{
    private readonly string _serverUrl;
    private readonly string _apiKey;
    private readonly ConcurrentDictionary<string, MarketDataSnapshot> _snapshots = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private readonly HashSet<string> _subscribedCodes = new();
    private readonly object _subscribeLock = new();

    /// <summary>行情源名称</summary>
    public string Name => "WebSocket";

    /// <summary>是否已连接</summary>
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    /// <summary>实时行情事件</summary>
    public event EventHandler<MarketDataSnapshot>? OnMarketData;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serverUrl">WebSocket 服务器地址（ws:// 或 wss://）</param>
    /// <param name="apiKey">API 密钥（可选）</param>
    public WebSocketMarketDataProvider(string serverUrl, string apiKey = "")
    {
        _serverUrl = serverUrl;
        _apiKey = apiKey;
    }

    /// <summary>
    /// 连接 WebSocket 服务器并启动接收循环
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_serverUrl))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _socket = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _socket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
        }

        try
        {
            await _socket.ConnectAsync(new Uri(_serverUrl), _cts.Token);
            // 发送已订阅股票的订阅请求
            lock (_subscribeLock)
            {
                foreach (var code in _subscribedCodes)
                {
                    SendSubscribeAsync(code).GetAwaiter().GetResult();
                }
            }
            // 启动接收循环
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        }
        catch
        {
            // 连接失败时保持未连接状态，不抛出
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        if (_socket != null)
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", cancellationToken);
                }
            }
            catch { /* ignore */ }
            _socket.Dispose();
            _socket = null;
        }
    }

    /// <summary>
    /// 获取单只股票快照
    /// </summary>
    public Task<MarketDataSnapshot?> GetSnapshotAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        // 确保已订阅
        EnsureSubscribed(stockCode);
        _snapshots.TryGetValue(stockCode, out var snapshot);
        return Task.FromResult<MarketDataSnapshot?>(snapshot);
    }

    /// <summary>
    /// 批量获取行情快照
    /// </summary>
    public async Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotsAsync(IEnumerable<string> stockCodes, CancellationToken cancellationToken = default)
    {
        var results = new List<MarketDataSnapshot>();
        foreach (var code in stockCodes)
        {
            var snapshot = await GetSnapshotAsync(code, cancellationToken);
            if (snapshot != null) results.Add(snapshot);
        }
        return results;
    }

    /// <summary>
    /// 确保股票已被订阅
    /// </summary>
    private void EnsureSubscribed(string stockCode)
    {
        lock (_subscribeLock)
        {
            if (_subscribedCodes.Add(stockCode) && IsConnected)
            {
                _ = SendSubscribeAsync(stockCode);
            }
        }
    }

    /// <summary>
    /// 发送订阅消息
    /// </summary>
    private async Task SendSubscribeAsync(string stockCode)
    {
        if (_socket == null || _socket.State != WebSocketState.Open) return;
        var msg = JsonSerializer.Serialize(new { action = "subscribe", code = stockCode });
        var bytes = Encoding.UTF8.GetBytes(msg);
        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>
    /// 接收循环：持续读取服务器推送并解析
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[8192];
        while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var snapshot = ParseSnapshot(json);
                if (snapshot != null)
                {
                    _snapshots[snapshot.StockCode] = snapshot;
                    OnMarketData?.Invoke(this, snapshot);
                }
            }
            catch
            {
                // 接收异常时短暂重试
                await Task.Delay(1000, token);
            }
        }
    }

    /// <summary>
    /// 解析 JSON 行情消息
    /// 支持格式：{ code, name, price, change, prevClose, open, high, low, volume, time }
    /// </summary>
    private static MarketDataSnapshot? ParseSnapshot(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("code", out var codeEl)) return null;

            var code = codeEl.GetString() ?? "";
            var price = GetDecimal(root, "price");
            var prevClose = GetDecimal(root, "prevClose", "prev_close", "preClose");
            var changePct = GetDecimal(root, "change", "changePct", "change_pct");
            if (changePct == 0 && prevClose > 0)
            {
                changePct = (price - prevClose) / prevClose * 100m;
            }

            return new MarketDataSnapshot
            {
                StockCode = code,
                StockName = GetString(root, "name"),
                CurrentPrice = price,
                ChangePercent = changePct,
                PreviousClose = prevClose,
                Open = GetDecimal(root, "open"),
                High = GetDecimal(root, "high"),
                Low = GetDecimal(root, "low"),
                Volume = GetLong(root, "volume"),
                Timestamp = DateTime.Now
            };
        }
        catch
        {
            return null;
        }
    }

    private static decimal GetDecimal(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var el))
            {
                return el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : 0m;
            }
        }
        return 0m;
    }

    private static long GetLong(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var el))
            {
                return el.ValueKind == JsonValueKind.Number ? el.GetInt64() : 0;
            }
        }
        return 0;
    }

    private static string GetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                return el.GetString() ?? "";
            }
        }
        return "";
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _socket?.Dispose();
        _cts?.Dispose();
    }
}
