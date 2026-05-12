using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CXMTCode.Plugins.Common.Notification;

/// <summary>
/// 邮件通知服务 - 使用 System.Net.Mail.SmtpClient（仅同步支持，但在 async 下用 SendMailAsync 包装）。
/// 配置项（appsettings.json）：
/// <code>
/// "Smtp": {
///   "Host": "smtp.example.com",
///   "Port": 465,
///   "UseSsl": true,
///   "Username": "alert@example.com",
///   "Password": "...",
///   "From": "CXMTCode &lt;alert@example.com&gt;",
///   "Enabled": false
/// }
/// </code>
/// 若 <c>Smtp:Enabled=false</c>（默认），SendAsync 不抛错，仅记录到日志（"DryRun" 模式），
/// 便于开发 / 测试环境快速跑通。
/// </summary>
public sealed class EmailNotificationService
{
    private readonly SmtpOptions _opts;
    private readonly ILogger<EmailNotificationService>? _log;

    public EmailNotificationService(IConfiguration config, ILogger<EmailNotificationService>? log = null)
    {
        _opts = new SmtpOptions();
        config.GetSection("Smtp").Bind(_opts);
        _log = log;
    }

    /// <summary>是否启用真实 SMTP 发送（false 时仅落日志）</summary>
    public bool Enabled => _opts.Enabled && !string.IsNullOrWhiteSpace(_opts.Host);

    public async Task<NotificationResult> SendAsync(
        IEnumerable<string> recipients, string subject, string body, bool isHtml = false)
    {
        var toList = recipients.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        if (toList.Count == 0) return NotificationResult.SkippedFor("收件人为空");

        if (!Enabled)
        {
            _log?.LogInformation(
                "[Smtp DryRun] To={To}, Subject={Subject}, BodyLength={BodyLen}",
                string.Join(",", toList), subject, body?.Length ?? 0);
            return NotificationResult.SkippedFor("Smtp:Enabled=false，已记录到日志");
        }

        try
        {
            using var msg = new MailMessage
            {
                From = new MailAddress(_opts.From ?? _opts.Username ?? "noreply@cxmtcode"),
                Subject = subject,
                Body = body ?? string.Empty,
                IsBodyHtml = isHtml
            };
            foreach (var to in toList) msg.To.Add(to);

            using var client = new SmtpClient(_opts.Host, _opts.Port)
            {
                EnableSsl = _opts.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_opts.Username ?? string.Empty, _opts.Password ?? string.Empty)
            };
            await client.SendMailAsync(msg);
            return NotificationResult.Ok($"邮件已发送给 {toList.Count} 位收件人");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Smtp 发送失败");
            return NotificationResult.Failed(ex.Message);
        }
    }
}

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public bool Enabled { get; set; } = false;
}

public sealed class NotificationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public bool Skipped { get; init; }

    public static NotificationResult Ok(string msg)        => new() { Success = true,  Message = msg };
    public static NotificationResult SkippedFor(string msg) => new() { Success = true, Message = msg, Skipped = true };
    public static NotificationResult Failed(string msg)    => new() { Success = false, Message = msg };
}
