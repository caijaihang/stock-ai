using System.Text.Json;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 交易策略定义
/// </summary>
public class StrategyDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// 选股公式（通达信语法）
    /// </summary>
    public string Formula { get; set; } = string.Empty;
    /// <summary>
    /// 买入阈值百分比
    /// </summary>
    public decimal BuyThresholdPercent { get; set; } = 2.0m;
    /// <summary>
    /// 卖出阈值百分比
    /// </summary>
    public decimal SellThresholdPercent { get; set; } = 2.0m;
    /// <summary>
    /// 买入数量
    /// </summary>
    public int BuyQuantity { get; set; } = 100;
    /// <summary>
    /// 卖出数量
    /// </summary>
    public int SellQuantity { get; set; } = 100;
    /// <summary>
    /// 冷却秒数
    /// </summary>
    public int CooldownSeconds { get; set; } = 60;
    /// <summary>
    /// 作者
    /// </summary>
    public string Author { get; set; } = string.Empty;
    /// <summary>
    /// 评分（0-5）
    /// </summary>
    public decimal Rating { get; set; }
    /// <summary>
    /// 使用次数
    /// </summary>
    public int UseCount { get; set; }
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 策略广场服务（本地策略库，支持导入导出）
/// </summary>
public class StrategySquareService
{
    private readonly string _dir;
    private readonly List<StrategyDefinition> _strategies = new();

    public StrategySquareService()
    {
        _dir = Path.Combine(StockAutoTrader.Core.PortablePathHelper.GetDataDirectory(), "strategies");
        if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
        LoadAll();
        SeedDefaultStrategies();
    }

    /// <summary>
    /// 获取所有策略（按评分和使用次数排序）
    /// </summary>
    public IReadOnlyList<StrategyDefinition> GetAll() =>
        _strategies.OrderByDescending(s => s.Rating).ThenByDescending(s => s.UseCount).ToList();

    /// <summary>
    /// 获取指定策略
    /// </summary>
    public StrategyDefinition? Get(string id) => _strategies.FirstOrDefault(s => s.Id == id);

    /// <summary>
    /// 保存策略
    /// </summary>
    public void Save(StrategyDefinition strategy)
    {
        var existing = _strategies.FirstOrDefault(s => s.Id == strategy.Id);
        if (existing != null)
        {
            _strategies.Remove(existing);
        }
        _strategies.Add(strategy);
        SaveToFile(strategy);
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    public void Delete(string id)
    {
        _strategies.RemoveAll(s => s.Id == id);
        var file = Path.Combine(_dir, $"{id}.json");
        if (File.Exists(file)) File.Delete(file);
    }

    /// <summary>
    /// 导出策略为 JSON 字符串
    /// </summary>
    public string Export(string id)
    {
        var s = Get(id);
        return s == null ? string.Empty : JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// 从 JSON 字符串导入策略
    /// </summary>
    public StrategyDefinition? Import(string json)
    {
        try
        {
            var s = JsonSerializer.Deserialize<StrategyDefinition>(json);
            if (s == null) return null;
            s.Id = Guid.NewGuid().ToString("N");
            Save(s);
            return s;
        }
        catch
        {
            return null;
        }
    }

    private void LoadAll()
    {
        foreach (var file in Directory.GetFiles(_dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var s = JsonSerializer.Deserialize<StrategyDefinition>(json);
                if (s != null) _strategies.Add(s);
            }
            catch { /* 忽略损坏文件 */ }
        }
    }

    private void SaveToFile(StrategyDefinition s)
    {
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_dir, $"{s.Id}.json"), json);
    }

    /// <summary>
    /// 预置示例策略
    /// </summary>
    private void SeedDefaultStrategies()
    {
        if (_strategies.Count > 0) return;

        var defaults = new[]
        {
            new StrategyDefinition
            {
                Name = "均线金叉",
                Description = "5日均线上穿10日均线，短期上涨趋势",
                Formula = "CROSS(MA(C,5),MA(C,10))",
                Author = "系统",
                Rating = 4.2m
            },
            new StrategyDefinition
            {
                Name = "放量突破",
                Description = "收盘价创20日新高且成交量放大",
                Formula = "C>HHV(H,20) AND V>REF(V,1)*1.5",
                Author = "系统",
                Rating = 3.8m
            },
            new StrategyDefinition
            {
                Name = "低位反弹",
                Description = "收盘价接近20日最低价且收阳线",
                Formula = "C<LLV(L,20)*1.02 AND C>O",
                Author = "系统",
                Rating = 3.5m
            }
        };

        foreach (var s in defaults) Save(s);
    }
}
