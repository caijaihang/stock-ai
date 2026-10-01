using System.Text.Json;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 交易日记条目
/// </summary>
public class TradingDiaryEntry
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>
    /// 类型：trade（交易记录）/ changelog（变更日志）/ note（随笔）
    /// </summary>
    public string Type { get; set; } = "note";
    /// <summary>
    /// 关联的股票代码（可选）
    /// </summary>
    public string StockCode { get; set; } = string.Empty;
}

/// <summary>
/// 交易日记服务（JSON 文件持久化）
/// </summary>
public class TradingDiaryService
{
    private readonly string _filePath;
    private readonly List<TradingDiaryEntry> _entries = new();
    private int _nextId = 1;

    public TradingDiaryService()
    {
        _filePath = System.IO.Path.Combine(
            StockAutoTrader.Core.PortablePathHelper.GetDataDirectory(),
            "trading_diary.json");
        Load();
    }

    /// <summary>
    /// 获取所有日记条目
    /// </summary>
    public IReadOnlyList<TradingDiaryEntry> GetAll() => _entries.OrderByDescending(e => e.Date).ToList();

    /// <summary>
    /// 添加日记条目
    /// </summary>
    public TradingDiaryEntry Add(string title, string content, string type = "note", string stockCode = "")
    {
        var entry = new TradingDiaryEntry
        {
            Id = _nextId++,
            Date = DateTime.Now,
            Title = title,
            Content = content,
            Type = type,
            StockCode = stockCode
        };
        _entries.Add(entry);
        Save();
        return entry;
    }

    /// <summary>
    /// 删除日记条目
    /// </summary>
    public void Delete(int id)
    {
        _entries.RemoveAll(e => e.Id == id);
        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize<List<TradingDiaryEntry>>(json);
                if (list != null && list.Count > 0)
                {
                    _entries.AddRange(list);
                    _nextId = list.Max(e => e.Id) + 1;
                }
            }
        }
        catch
        {
            // 加载失败则从空开始
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 保存失败忽略
        }
    }
}
