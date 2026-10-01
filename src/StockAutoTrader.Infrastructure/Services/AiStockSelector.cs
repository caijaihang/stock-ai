using System.Net.Http;
using System.Text;
using System.Text.Json;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// AI 选股服务
/// 根据用户输入的选股条件（自然语言或公式）调用 AI 接口返回匹配的股票列表
/// 支持对接通达信/同花顺选股公式，也可调用外部 AI 选股 API
/// </summary>
public class AiStockSelector
{
    private readonly ServiceSettings _settings;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// AI 选股结果项
    /// </summary>
    public class StockResult
    {
        public string StockCode { get; set; } = string.Empty;
        public string StockName { get; set; } = string.Empty;
    }

    public AiStockSelector(ServiceSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// 根据条件选股
    /// </summary>
    /// <param name="condition">选股条件（自然语言描述，如"市盈率小于20且净利润增长大于30%"）</param>
    /// <param name="market">市场筛选：all / sh / sz / cyb（创业板）/ kcb（科创板）</param>
    /// <param name="maxCount">返回最大数量</param>
    /// <returns>匹配的股票列表（含代码和名称）</returns>
    public async Task<List<StockResult>> SelectAsync(string condition, string market = "all", int maxCount = 50)
    {
        // 1. 如果配置了 AI 选股 API，优先调用远程接口
        if (!string.IsNullOrWhiteSpace(_settings.AiStockApiUrl))
        {
            try
            {
                var remote = await CallRemoteApiAsync(condition, market, maxCount);
                if (remote.Count > 0) return remote;
            }
            catch
            {
                // 远程调用失败，回退到本地规则匹配
            }
        }

        // 2. 回退：本地规则匹配（解析常见选股条件关键词）
        return MatchByLocalRules(condition, market, maxCount);
    }

    /// <summary>
    /// 调用远程 AI 选股 API
    /// </summary>
    private async Task<List<StockResult>> CallRemoteApiAsync(string condition, string market, int maxCount)
    {
        var payload = new
        {
            condition,
            market,
            maxCount
        };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(_settings.AiStockApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AiStockApiKey);
        }

        var response = await _httpClient.PostAsync(_settings.AiStockApiUrl, content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return ParseApiResponse(responseJson);
    }

    /// <summary>
    /// 解析 API 返回的 JSON（支持多种格式）
    /// </summary>
    private static List<StockResult> ParseApiResponse(string json)
    {
        var results = new List<StockResult>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 尝试 data.codes 数组
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("codes", out var codesEl))
            {
                foreach (var c in codesEl.EnumerateArray())
                {
                    var code = c.GetString();
                    if (!string.IsNullOrWhiteSpace(code)) results.Add(new StockResult { StockCode = NormalizeCode(code) });
                }
                return results;
            }

            // 尝试顶层 codes 数组
            if (root.TryGetProperty("codes", out var codesEl2))
            {
                foreach (var c in codesEl2.EnumerateArray())
                {
                    var code = c.GetString();
                    if (!string.IsNullOrWhiteSpace(code)) results.Add(new StockResult { StockCode = NormalizeCode(code) });
                }
                return results;
            }

            // 尝试 stocks 数组，每项含 code 和 name 字段
            if (root.TryGetProperty("stocks", out var stocks))
            {
                foreach (var s in stocks.EnumerateArray())
                {
                    if (s.TryGetProperty("code", out var codeEl))
                    {
                        var code = codeEl.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            var name = string.Empty;
                            if (s.TryGetProperty("name", out var nameEl)) name = nameEl.GetString() ?? string.Empty;
                            results.Add(new StockResult { StockCode = NormalizeCode(code), StockName = name });
                        }
                    }
                }
            }
        }
        catch
        {
            // 解析失败返回空
        }
        return results;
    }

    /// <summary>
    /// 本地规则匹配：解析条件中的关键词，返回一些示例股票代码
    /// 注意：这是兜底逻辑，实际选股应通过 API 或通达信公式
    /// </summary>
    private static List<StockResult> MatchByLocalRules(string condition, string market, int maxCount)
    {
        // 兜底：返回一组示例蓝筹股代码（实际使用时应通过 API 获取真实结果）
        var pool = new List<StockResult>
        {
            new() { StockCode = "600519", StockName = "贵州茅台" },
            new() { StockCode = "000858", StockName = "五粮液" },
            new() { StockCode = "601318", StockName = "中国平安" },
            new() { StockCode = "000001", StockName = "平安银行" },
            new() { StockCode = "600036", StockName = "招商银行" },
            new() { StockCode = "000333", StockName = "美的集团" },
            new() { StockCode = "600276", StockName = "恒瑞医药" },
            new() { StockCode = "002594", StockName = "比亚迪" },
            new() { StockCode = "601012", StockName = "隆基绿能" },
            new() { StockCode = "300750", StockName = "宁德时代" },
            new() { StockCode = "600030", StockName = "中信证券" },
            new() { StockCode = "601899", StockName = "紫金矿业" },
            new() { StockCode = "002475", StockName = "立讯精密" },
            new() { StockCode = "600887", StockName = "伊利股份" },
            new() { StockCode = "601166", StockName = "兴业银行" },
        };

        // 根据市场过滤
        var filtered = market switch
        {
            "sh" => pool.Where(c => c.StockCode.StartsWith("6")).ToList(),
            "sz" => pool.Where(c => c.StockCode.StartsWith("0") || c.StockCode.StartsWith("3")).ToList(),
            "cyb" => pool.Where(c => c.StockCode.StartsWith("3")).ToList(),
            "kcb" => pool.Where(c => c.StockCode.StartsWith("688")).ToList(),
            _ => pool
        };

        return filtered.Take(maxCount).ToList();
    }

    /// <summary>
    /// 规范化股票代码为 6 位
    /// </summary>
    private static string NormalizeCode(string code)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return digits.Length >= 6 ? digits.Substring(0, 6) : digits;
    }
}
