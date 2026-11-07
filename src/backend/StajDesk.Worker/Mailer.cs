using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using StajDesk.Domain.Entities;
using StajDesk.Infrastructure.Persistence;

namespace StajDesk.Worker;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string From { get; set; } = "StajDesk <no-reply@stajdesk.local>";
    public string WebUrl { get; set; } = "http://localhost:8080";
}

/// <summary>
/// SMTP ile gerçek e-posta gönderimi (MailKit). Demo ortamında SMTP sunucusu Mailpit'tir:
/// e-posta gerçekten SMTP protokolüyle gönderilir ama dışarı çıkmaz; http://localhost:8025 adresinde görülür.
/// Her gönderim sent_emails tablosuna da yazılır (arayüzdeki "E-posta kutusu" ekranı).
/// </summary>
public sealed class Mailer(IOptions<SmtpOptions> options, AppDbContext db, ILogger<Mailer> logger)
{
    public string WebUrl => options.Value.WebUrl.TrimEnd('/');

    public async Task SendAsync(string to, string subject, string template, string title, IReadOnlyList<string> lines, (string Label, string Url)? action, CancellationToken ct)
    {
        var o = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(o.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = Text(title, lines, action), HtmlBody = Html(title, lines, action) }.ToMessageBody();

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(o.Host, o.Port, SecureSocketOptions.None, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
            db.SentEmails.Add(new SentEmail { ToAddress = to, Subject = subject, Template = template, Status = "sent" });
            logger.LogInformation("E-posta gönderildi → {To}: {Subject} ({Template})", to, subject, template);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.SentEmails.Add(new SentEmail { ToAddress = to, Subject = subject, Template = template, Status = "failed", Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message });
            logger.LogError(ex, "E-posta gönderilemedi → {To} ({Template})", to, template);
        }
    }

    private static string Text(string title, IReadOnlyList<string> lines, (string Label, string Url)? action) =>
        string.Join('\n', new[] { title, "" }.Concat(lines).Concat(action is { } a ? ["", $"{a.Label}: {a.Url}"] : []).Concat(["", "— StajDesk"]));

    /// <summary>Basit, kaçışlanmış (XSS'e kapalı) HTML şablon.</summary>
    private static string Html(string title, IReadOnlyList<string> lines, (string Label, string Url)? action)
    {
        static string e(string s) => WebUtility.HtmlEncode(s);
        var body = string.Concat(lines.Select(l => $"<p style=\"margin:0 0 12px;line-height:1.5\">{e(l)}</p>"));
        var button = action is { } a
            ? $"<p style=\"margin:24px 0\"><a href=\"{e(a.Url)}\" style=\"background:#8b5cf6;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:600\">{e(a.Label)}</a></p>"
            : "";
        return $"""
            <!doctype html><html><body style="margin:0;background:#f3f4f6;font-family:Inter,Arial,sans-serif">
            <div style="max-width:560px;margin:24px auto;background:#fff;border-radius:12px;padding:32px;color:#374151">
            <div style="font-weight:700;color:#7c3aed;font-size:18px;margin-bottom:16px">StajDesk</div>
            <h1 style="font-size:20px;color:#111827;margin:0 0 16px">{e(title)}</h1>
            {body}{button}
            <p style="font-size:12px;color:#9ca3af;margin-top:32px">Bu e-posta StajDesk tarafından otomatik gönderildi.</p>
            </div></body></html>
            """;
    }
}
