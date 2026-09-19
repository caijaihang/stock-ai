using StockAutoTrader.Infrastructure.Providers;
using Xunit;

namespace StockAutoTrader.Tests.Providers;

/// <summary>
/// 模拟行情数据提供器测试
/// </summary>
public class SimulatedMarketDataProviderTests
{
    /// <summary>
    /// 测试连接与断开
    /// </summary>
    [Fact]
    public async Task ConnectAsync_And_DisconnectAsync_TogglesConnectionState()
    {
        var provider = new SimulatedMarketDataProvider(0.01);

        Assert.False(provider.IsConnected);

        await provider.ConnectAsync();
        Assert.True(provider.IsConnected);

        await provider.DisconnectAsync();
        Assert.False(provider.IsConnected);
    }

    /// <summary>
    /// 测试获取行情快照
    /// </summary>
    [Fact]
    public async Task GetSnapshotAsync_AfterInitialize_ReturnsSnapshot()
    {
        var provider = new SimulatedMarketDataProvider(0.01);
        provider.InitializeStock("000001", "平安银行", 10.0m);

        var snapshot = await provider.GetSnapshotAsync("000001");

        Assert.NotNull(snapshot);
        Assert.Equal("000001", snapshot.StockCode);
        Assert.Equal("平安银行", snapshot.StockName);
        Assert.Equal(10.0m, snapshot.CurrentPrice);
    }

    /// <summary>
    /// 测试批量获取行情快照
    /// </summary>
    [Fact]
    public async Task GetSnapshotsAsync_ReturnsInitializedStocks()
    {
        var provider = new SimulatedMarketDataProvider(0.01);
        provider.InitializeStock("000001", "平安银行", 10.0m);
        provider.InitializeStock("000002", "万科A", 20.0m);

        var snapshots = await provider.GetSnapshotsAsync(["000001", "000002", "000003"]);

        Assert.Equal(2, snapshots.Count);
    }
}
