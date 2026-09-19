using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Data;
using StockAutoTrader.Infrastructure.Logging;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.Tests.Services;

/// <summary>
/// 模拟交易执行器测试
/// </summary>
public class SimulatedTradeExecutorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;
    private readonly ITradeExecutor _executor;
    private readonly IAccountManager _accountManager;

    public SimulatedTradeExecutorTests()
    {
        _connection = new SqliteConnection("DataSource=StockAutoTraderTests;Mode=Memory;Cache=Shared");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new TradingDbContext(options);
        initContext.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
        var logger = new SerilogLoggerService(Path.Combine(Path.GetTempPath(), "test-logs"));
        var notifications = new NoopNotificationService();
        _accountManager = new EfAccountManager(_contextFactory);
        _accountManager.InitializeAsync(100000m).GetAwaiter().GetResult();

        _executor = TradeExecutorFactory.Create(new ServiceSettings(), _contextFactory, logger, notifications);
    }

    /// <summary>
    /// 测试买入成功后资金减少、持仓增加
    /// </summary>
    [Fact]
    public async Task BuyAsync_SufficientFunds_CreatesOrderAndPosition()
    {
        var order = await _executor.BuyAsync("000001", "平安银行", 100, 10.0m, OrderType.SimulatedImmediate);

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(OrderSide.Buy, order.Side);

        var account = await _accountManager.GetAccountAsync();
        Assert.True(account.AvailableCash < 100000m);

        var positions = await _executor.QueryPositionsAsync();
        Assert.Single(positions);
        Assert.Equal(100, positions[0].TotalQuantity);
    }

    /// <summary>
    /// 测试卖出成功后持仓减少、资金增加
    /// </summary>
    [Fact]
    public async Task SellAsync_WithPosition_CreatesSellOrder()
    {
        await _executor.BuyAsync("000001", "平安银行", 100, 10.0m, OrderType.SimulatedImmediate);

        var order = await _executor.SellAsync("000001", "平安银行", 50, 11.0m, OrderType.SimulatedImmediate);

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(OrderSide.Sell, order.Side);

        var positions = await _executor.QueryPositionsAsync();
        Assert.Single(positions);
        Assert.Equal(50, positions[0].TotalQuantity);
    }

    /// <summary>
    /// 测试资金不足时买入被拒绝
    /// </summary>
    [Fact]
    public async Task BuyAsync_InsufficientFunds_OrderRejected()
    {
        var order = await _executor.BuyAsync("000001", "平安银行", 20000, 10.0m, OrderType.SimulatedImmediate);

        Assert.Equal(OrderStatus.Rejected, order.Status);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private class TestDbContextFactory : IDbContextFactory<TradingDbContext>
    {
        private readonly DbContextOptions<TradingDbContext> _options;

        public TestDbContextFactory(DbContextOptions<TradingDbContext> options)
        {
            _options = options;
        }

        public TradingDbContext CreateDbContext()
        {
            return new TradingDbContext(_options);
        }
    }
}
