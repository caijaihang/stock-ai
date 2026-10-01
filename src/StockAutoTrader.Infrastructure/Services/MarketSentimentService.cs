using System.Net.Http;
using System.Text.Json;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 市场情绪服务
/// 提供看涨/看跌情绪指标，优先从外部情绪数据源获取，失败时回退到本地计算
/// </summary>
public class MarketSentimentService
{
    private readonly HttpClient _httpClient;
    private readonly ServiceSettings _settings;

    public MarketSentimentService(ServiceSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// 情绪数据
    /// </summary>
    public class SentimentData
    {
        /// <summary>
        /// 看涨比例 0-100
        /// </summary>
        public decimal BullishPercent { get; set; }
        /// <summary>
        /// 看跌比例 0-100
        /// </summary>
        public decimal BearishPercent { get; set; }
        /// <summary>
        /// 情绪分数：-100（极度看空）到 +100（极度看多）
        /// </summary>
        public decimal SentimentScore => BullishPercent - BearishPercent;
        /// <summary>
        /// 数据来源
        /// </summary>
        public string Source { get; set; } = "local";
        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 获取市场情绪
    /// </summary>
    /// <param name="market">市场标识，如 "sh"、"sz"、"cyb"</param>
    public async Task<SentimentData> GetSentimentAsync(string market = "sh")
    {
        // 尝试从外部 API 获取
        if (!string.IsNullOrWhiteSpace(_settings.WebhookUrl))
        {
            // 这里可以对接真实的情绪数据 API
            // 暂时回退到本地计算
        }

        // 本地回退：返回中性情绪
        return await Task.FromResult(new SentimentData
        {
            BullishPercent = 50m,
            BearishPercent = 50m,
            Source = "local",
            UpdatedAt = DateTime.Now
        });
    }

    /// <summary>
    /// 根据历史行情数据计算简易情绪指标
    /// </summary>
    /// <param name="recentChanges">最近涨跌幅列表</param>
    public static SentimentData CalcFromPriceChanges(List<decimal> recentChanges)
    {
        if (recentChanges.Count == 0)
        {
            return new SentimentData { BullishPercent = 50m, BearishPercent = 50m };
        }

        var upCount = recentChanges.Count(c => c > 0);
        var downCount = recentChanges.Count(c => c < 0);
        var total = recentChanges.Count;

        return new SentimentData
        {
            BullishPercent = Math.Round((decimal)upCount / total * 100, 1),
            BearishPercent = Math.Round((decimal)downCount / total * 100, 1),
            Source = "calculated"
        };
    }
}
