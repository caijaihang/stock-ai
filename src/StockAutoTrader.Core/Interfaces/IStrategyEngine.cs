using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 策略引擎接口
/// </summary>
public interface IStrategyEngine
{
    /// <summary>
    /// 判断买入/卖出信号
    /// </summary>
    /// <param name="snapshot">行情快照</param>
    /// <param name="config">股票配置</param>
    /// <param name="position">当前持仓，可为 null</param>
    /// <param name="todayBuyCount">今日已买入次数</param>
    /// <param name="todaySellCount">今日已卖出次数</param>
    /// <returns>交易信号</returns>
    TradeSignal Evaluate(
        MarketDataSnapshot snapshot,
        StockConfig config,
        Position? position,
        int todayBuyCount,
        int todaySellCount);
}
