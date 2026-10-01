using System.Net.Http;
using System.Text;
using System.Text.Json;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// Webhook 通知服务
/// 支持飞书、钉钉、通用 Webhook 三种格式，用于将交易事件推送到外部 IM
/// </summary>
public class WebhookNotifier
{
    private readonly ServiceSettings _settings;
    private readonly HttpClient _httpClient;

    public WebhookNotifier(ServiceSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// 发送通知到配置的 Webhook
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="content">通知内容</param>
    public async Task SendAsync(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookUrl)) return;

        try
        {
            var payload = _settings.WebhookType.ToLower() switch
            {
                "feishu" => BuildFeishuPayload(title, content),
                "dingtalk" => BuildDingTalkPayload(title, content),
                _ => BuildGenericPayload(title, content)
            };

            var json = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(_settings.WebhookUrl, httpContent);
        }
        catch
        {
            // Webhook 推送失败不影响主流程，静默忽略
        }
    }

    /// <summary>
    /// 构建飞书机器人消息体（text 类型）
    /// </summary>
    private static object BuildFeishuPayload(string title, string content)
    {
        return new
        {
            msg_type = "text",
            content = new
            {
                text = $"【{title}】\n{content}"
            }
        };
    }

    /// <summary>
    /// 构建钉钉机器人消息体（text 类型）
    /// </summary>
    private static object BuildDingTalkPayload(string title, string content)
    {
        return new
        {
            msgtype = "text",
            text = new
            {
                content = $"【{title}】\n{content}"
            }
        };
    }

    /// <summary>
    /// 构建通用 Webhook 消息体
    /// </summary>
    private static object BuildGenericPayload(string title, string content)
    {
        return new
        {
            title,
            content,
            timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        };
    }
}
