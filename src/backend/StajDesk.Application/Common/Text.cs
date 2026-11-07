using System.Text.RegularExpressions;
using FluentValidation;

namespace StajDesk.Application.Common;

/// <summary>
/// XSS savunması (derinlemesine savunma):
/// 1) Düz metin alanlarında HTML etiketleri ve tehlikeli şemalar sunucuda temizlenir.
/// 2) React çıktıyı zaten kaçışlar; dangerouslySetInnerHTML kullanılmaz.
/// 3) API ve nginx sıkı Content-Security-Policy başlığı gönderir.
/// </summary>
public static partial class Text
{
    [GeneratedRegex(@"<\/?[a-zA-Z][\s\S]*?>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"(javascript|vbscript|data)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousSchemes();

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]")]
    private static partial Regex ControlChars();

    public static string Clean(string? value) =>
        value is null ? string.Empty : ControlChars().Replace(DangerousSchemes().Replace(Tags().Replace(value, string.Empty), string.Empty), string.Empty).Trim();

    public static string? CleanOrNull(string? value)
    {
        var clean = Clean(value);
        return clean.Length == 0 ? null : clean;
    }

    public static bool ContainsMarkup(string? value) => value is not null && (Tags().IsMatch(value) || DangerousSchemes().IsMatch(value));

    /// <summary>FluentValidation kuralı: HTML / script içeren girdiyi reddeder.</summary>
    public static IRuleBuilderOptions<T, string?> NoMarkup<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => !ContainsMarkup(v)).WithMessage("HTML veya betik içeren metin kabul edilmez. Düz metin olarak yazın.");
}
