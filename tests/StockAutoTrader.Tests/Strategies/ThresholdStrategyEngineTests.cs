using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Models;
using StockAutoTrader.Core.Strategies;
using Xunit;

namespace StockAutoTrader.Tests.Strategies;

/// <summary>
/// 阈值策略引擎测试
/// </summary>
public class ThresholdStrategyEngineTests
{
    private readonly ThresholdStrategyEngine _engine = new();

    /// <summary>
    /// 测试买入信号：涨幅达到阈值
    /// </summary>
    [Fact]
    public void Evaluate_BuyThresholdReached_ReturnsBuySignal()
    {
        var config = new StockConfig
        {
            StockCode = "000001",
            StockName = "平安银行",
            BenchmarkPriceType = BenchmarkPriceType.PreviousClose,
            BuyThresholdPercent = 2.0m,
            SellThresholdPercent = 2.0m,
            BuyQuantity = 100,
            Status = StockStatus.Running,
            TradingHours = "00:00-23:59",
            CooldownSeconds = 0
        };

        var snapshot = new MarketDataSnapshot
        {
            StockCode = "000001",
            CurrentPrice = 10.2m,
            PreviousClose = 10.0m,
            Timestamp = DateTime.Now
        };

        var signal = _engine.Evaluate(snapshot, config, null, 0, 0);

        Assert.Equal(TradeSignalType.Buy, signal.SignalType);
        Assert.Equal(100, signal.SuggestedQuantity);
    }

    /// <summary>
    /// 测试卖出信号：相对基准价跌幅达到阈值
    /// </summary>
    [Fact]
    public void Evaluate_SellThresholdReached_ReturnsSellSignal()
    {
        var config = new StockConfig
        {
            StockCode = "000001",
            StockName = "平安银行",
            BenchmarkPriceType = BenchmarkPriceType.PreviousClose,
            BuyThresholdPercent = 2.0m,
            SellThresholdPercent = 2.0m,
            SellQuantity = 100,
            Status = StockStatus.Running,
            TradingHours = "00:00-23:59",
            CooldownSeconds = 0,
            AllowPartialSell = true
        };

        var position = new Position
        {
            StockCode = "000001",
            TotalQuantity = 1000,
            AvailableQuantity = 1000,
            AverageCost = 10.0m
        };

        var snapshot = new MarketDataSnapshot
        {
            StockCode = "000001",
            CurrentPrice = 9.8m,
            PreviousClose = 10.0m,
            Timestamp = DateTime.Now
        };

        var signal = _engine.Evaluate(snapshot, config, position, 0, 0);

        Assert.Equal(TradeSignalType.Sell, signal.SignalType);
        Assert.Equal(100, signal.SuggestedQuantity);
    }

    /// <summary>
    /// 测试非交易时段不触发信号
    /// </summary>
    [Fact]
    public void Evaluate_OutsideTradingHours_ReturnsNone()
    {
        var config = new StockConfig
        {
            StockCode = "000001",
            BuyThresholdPercent = 1.0m,
            Status = StockStatus.Running,
            TradingHours = "09:30-11:30"
        };

        var snapshot = new MarketDataSnapshot
        {
            StockCode = "000001",
            CurrentPrice = 11.0m,
            PreviousClose = 10.0m,
            Timestamp = new DateTime(2024, 1, 1, 15, 0, 0)
        };

        var signal = _engine.Evaluate(snapshot, config, null, 0, 0);

        Assert.Equal(TradeSignalType.None, signal.SignalType);
    }

    /// <summary>
    /// 测试超过最大买入次数不触发买入
    /// </summary>
    [Fact]
    public void Evaluate_MaxBuyCountReached_ReturnsNone()
    {
        var config = new StockConfig
        {
            StockCode = "000001",
            BuyThresholdPercent = 1.0m,
            MaxBuyTimesPerDay = 2,
            Status = StockStatus.Running,
            TradingHours = "00:00-23:59"
        };

        var snapshot = new MarketDataSnapshot
        {
            StockCode = "000001",
            CurrentPrice = 11.0m,
            PreviousClose = 10.0m,
            Timestamp = DateTime.Now
        };

        var signal = _engine.Evaluate(snapshot, config, null, 2, 0);

        Assert.Equal(TradeSignalType.None, signal.SignalType);
    }
}
