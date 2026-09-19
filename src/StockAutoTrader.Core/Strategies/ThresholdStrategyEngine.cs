using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Extensions;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Core.Strategies;

/// <summary>
/// 涨跌幅阈值策略引擎
/// </summary>
public class ThresholdStrategyEngine : IStrategyEngine
{
    /// <summary>
    /// 判断买入/卖出信号
    /// </summary>
    public TradeSignal Evaluate(
        MarketDataSnapshot snapshot,
        StockConfig config,
        Position? position,
        int todayBuyCount,
        int todaySellCount)
    {
        if (!snapshot.IsValid)
        {
            return CreateSignal(TradeSignalType.None, config, snapshot, 0, "行情数据无效", 0);
        }

        if (config.Status != StockStatus.Running)
        {
            return CreateSignal(TradeSignalType.None, config, snapshot, 0, "股票未处于运行状态", 0);
        }

        if (!snapshot.Timestamp.IsInTradingHours(config.TradingHours))
        {
            return CreateSignal(TradeSignalType.None, config, snapshot, 0, "不在交易时段内", 0);
        }

        var benchmark = GetBenchmarkPrice(snapshot, config, position);
        if (benchmark <= 0)
        {
            return CreateSignal(TradeSignalType.None, config, snapshot, 0, "基准价无效", 0);
        }

        var changePercent = (snapshot.CurrentPrice - benchmark) / benchmark * 100m;

        // 买入判断
        if (todayBuyCount < config.MaxBuyTimesPerDay)
        {
            var cooldownOk = (DateTime.Now - config.LastBuyTriggerTime).TotalSeconds >= config.CooldownSeconds;
            var canBuy = config.AllowRepeatBuy || position == null || position.TotalQuantity == 0;

            if (changePercent >= config.BuyThresholdPercent && cooldownOk && canBuy)
            {
                return CreateSignal(
                    TradeSignalType.Buy,
                    config,
                    snapshot,
                    config.BuyQuantity,
                    $"涨幅 {changePercent:F2}% 达到买入阈值 {config.BuyThresholdPercent:F2}%",
                    benchmark);
            }
        }

        // 卖出判断
        if (position != null && position.AvailableQuantity > 0 && todaySellCount < config.MaxSellTimesPerDay)
        {
            var cooldownOk = (DateTime.Now - config.LastSellTriggerTime).TotalSeconds >= config.CooldownSeconds;
            var t1Ok = !config.T1Enabled || position.LastBuyDate.Date < DateTime.Now.Date;

            // 相对基准价跌幅
            var sellByBenchmark = changePercent <= -config.SellThresholdPercent;

            // 相对持仓成本跌幅
            var costDropPercent = position.AverageCost > 0
                ? (snapshot.CurrentPrice - position.AverageCost) / position.AverageCost * 100m
                : 0m;
            var sellByCost = costDropPercent <= -config.SellThresholdPercent;

            if ((sellByBenchmark || sellByCost) && cooldownOk && t1Ok)
            {
                var quantity = config.AllowPartialSell
                    ? Math.Min(config.SellQuantity, position.AvailableQuantity)
                    : position.AvailableQuantity;

                var reason = sellByCost
                    ? $"相对成本跌幅 {costDropPercent:F2}% 达到卖出阈值 {config.SellThresholdPercent:F2}%"
                    : $"相对基准跌幅 {changePercent:F2}% 达到卖出阈值 {config.SellThresholdPercent:F2}%";

                return CreateSignal(TradeSignalType.Sell, config, snapshot, quantity, reason, benchmark);
            }
        }

        return CreateSignal(TradeSignalType.None, config, snapshot, 0, $"无信号，相对基准价 {changePercent:F2}%", benchmark);
    }

    /// <summary>
    /// 获取基准价
    /// </summary>
    private static decimal GetBenchmarkPrice(MarketDataSnapshot snapshot, StockConfig config, Position? position)
    {
        return config.BenchmarkPriceType switch
        {
            BenchmarkPriceType.PreviousClose => snapshot.PreviousClose,
            BenchmarkPriceType.Open => snapshot.Open,
            BenchmarkPriceType.Latest => snapshot.CurrentPrice,
            BenchmarkPriceType.PositionCost => position?.AverageCost ?? 0,
            BenchmarkPriceType.Custom => config.CustomBenchmarkPrice,
            _ => snapshot.PreviousClose
        };
    }

    /// <summary>
    /// 构造交易信号
    /// </summary>
    private static TradeSignal CreateSignal(
        TradeSignalType signalType,
        StockConfig config,
        MarketDataSnapshot snapshot,
        int quantity,
        string reason,
        decimal benchmark)
    {
        return new TradeSignal
        {
            SignalType = signalType,
            StockCode = config.StockCode,
            StockName = config.StockName,
            CurrentPrice = snapshot.CurrentPrice,
            BenchmarkPrice = benchmark,
            ChangePercent = snapshot.ChangePercent,
            SuggestedQuantity = quantity,
            OrderType = config.OrderType,
            Reason = reason,
            Timestamp = DateTime.Now
        };
    }
}
