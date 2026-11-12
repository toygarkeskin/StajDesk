using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using StajDesk.Application.Abstractions;
using StajDesk.Domain.Entities;

namespace StajDesk.Infrastructure.Persistence;

/// <summary>
/// Talep araması (PostgreSQL tam metin arama).
/// - "#42" veya "42" → talep numarası
/// - Diğer metinler → websearch_to_tsquery('turkish', …): "yazıcı" araması "yazıcıda", "yazıcılar" içeren talepleri de bulur;
///   tırnaklı ifade ("kasa açılmıyor") ve -hariç tutma desteklenir.
/// - Kelimenin bir parçası yazıldıysa (ör. "yazı") başlıkta ILIKE ile de eşleşir.
/// </summary>
internal sealed class PostgresTicketSearch : ITicketSearch
{
    public IQueryable<Ticket> Apply(IQueryable<Ticket> query, string term)
    {
        var s = term.Trim();
        if (s.Length == 0) return query;
        var like = "%" + EscapeLike(s) + "%";
        if (int.TryParse(s.TrimStart('#'), out var id))
            return query.Where(t => t.Id == id || EF.Functions.ILike(t.Title, like));

        return query.Where(t =>
            EF.Property<NpgsqlTsVector>(t, TicketSearch.VectorProperty).Matches(EF.Functions.WebSearchToTsQuery("turkish", s)) ||
            EF.Functions.ILike(t.Title, like));
    }

    /// <summary>LIKE özel karakterleri (%, _, \) kullanıcı girdisinde joker olarak yorumlanmasın.</summary>
    private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

public static class TicketSearch
{
    public const string VectorProperty = "SearchVector";
}
