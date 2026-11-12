using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Social;

/// <summary>Anlık iletim (SignalR). Uygulama katmanı yalnızca "şu kullanıcılara şu tür sinyal" der; veri her zaman API'den okunur.</summary>
public interface IRealtimeNotifier
{
    Task NotifyAsync(IReadOnlyCollection<int> userIds, string kind, string? title, CancellationToken ct = default);
}

/* =========================================================
   Sosyal akış: yalnızca kurumsal içerik; giriş yapmış çalışanlara açık.
   Paylaşım/yorum/beğeni: social.post. Moderasyon (kaldırma, şikayet sonuçlandırma): social.moderate.
   ========================================================= */

public sealed record SocialAuthorDto(int Id, string FullName, string? Title, string? Department, bool HasPhoto, string? PhotoVersion);
public sealed record SocialPostDto(int Id, SocialAuthorDto Author, string Body, bool HasImage, DateTime CreatedAt, DateTime? UpdatedAt,
    int LikeCount, bool LikedByMe, int CommentCount, bool CanDelete, bool CanModerate);
public sealed record SocialCommentDto(int Id, SocialAuthorDto Author, string Body, DateTime CreatedAt, bool CanDelete);
public sealed record CreatePostRequest(string Body);
public sealed record CreateCommentRequest(string Body);
public sealed record ReportPostRequest(string Reason);
public sealed record RemoveContentRequest(string Reason);
public sealed record ResolveReportRequest(bool RemovePost, string? Reason);
public sealed record SocialReportDto(int Id, int PostId, string PostExcerpt, string PostAuthor, string Reporter, string Reason, DateTime CreatedAt, bool PostRemoved);
public sealed record SocialImage(Stream Content, string ContentType);

public interface ISocialService
{
    Task<PagedResult<SocialPostDto>> FeedAsync(PagedQuery query, CancellationToken ct = default);
    Task<SocialPostDto> CreateAsync(string body, string? imageName, Stream? image, long imageLength, CancellationToken ct = default);
    Task DeleteAsync(int postId, CancellationToken ct = default);
    Task<SocialPostDto> ToggleLikeAsync(int postId, CancellationToken ct = default);
    Task<IReadOnlyList<SocialCommentDto>> CommentsAsync(int postId, CancellationToken ct = default);
    Task<SocialCommentDto> CommentAsync(int postId, string body, CancellationToken ct = default);
    Task DeleteCommentAsync(int commentId, CancellationToken ct = default);
    Task ReportAsync(int postId, string reason, CancellationToken ct = default);
    Task RemoveAsync(int postId, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<SocialReportDto>> PendingReportsAsync(CancellationToken ct = default);
    Task ResolveReportAsync(int reportId, bool removePost, string? reason, CancellationToken ct = default);
    Task<SocialImage> ImageAsync(int postId, CancellationToken ct = default);
}

internal sealed class SocialService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, IFileStorage storage) : ISocialService
{
    public const int MaxPostsPerHour = 10;
    public const long MaxImageBytes = 3 * 1024 * 1024;
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase) { [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp" };
    private bool CanModerate => currentUser.Has(Permissions.SocialModerate);

    internal static readonly System.Linq.Expressions.Expression<Func<User, SocialAuthorDto>> Author = u => new SocialAuthorDto(
        u.Id, u.FullName, u.Position == null ? null : u.Position.Title, u.Department == null ? null : u.Department.Name,
        u.Profile != null && u.Profile.PhotoKey != null, u.Profile == null ? null : u.Profile.PhotoKey);

    private IQueryable<SocialPostDto> Project(IQueryable<SocialPost> q, int me) => q.Select(p => new SocialPostDto(p.Id,
        new SocialAuthorDto(p.Author.Id, p.Author.FullName, p.Author.Position == null ? null : p.Author.Position.Title, p.Author.Department == null ? null : p.Author.Department.Name,
            p.Author.Profile != null && p.Author.Profile.PhotoKey != null, p.Author.Profile == null ? null : p.Author.Profile.PhotoKey),
        p.Body, p.ImageKey != null, p.CreatedAt, p.UpdatedAt, p.Likes.Count, p.Likes.Any(l => l.UserId == me),
        p.Comments.Count(c => c.RemovedAt == null), p.AuthorId == me || CanModerate, CanModerate));

    private async Task<SocialPost> VisiblePostAsync(int id, CancellationToken ct) =>
        await db.SocialPosts.FirstOrDefaultAsync(p => p.Id == id && p.RemovedAt == null, ct) ?? throw NotFoundException.For("Paylaşım", id);

    public async Task<PagedResult<SocialPostDto>> FeedAsync(PagedQuery query, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        return await Project(db.SocialPosts.AsNoTracking().Where(p => p.RemovedAt == null).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id), me)
            .ToPagedResultAsync(query, ct);
    }

    private async Task<SocialPostDto> GetAsync(int id, CancellationToken ct) =>
        await Project(db.SocialPosts.AsNoTracking().Where(p => p.Id == id), currentUser.RequireUserId()).FirstAsync(ct);

    public async Task<SocialPostDto> CreateAsync(string body, string? imageName, Stream? image, long imageLength, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.SocialPost);
        var me = currentUser.RequireUserId();
        var text = Text.Clean(body);
        if (text.Length == 0 && image is null) throw new DomainException("Paylaşım boş olamaz; bir metin yazın veya görsel ekleyin.");
        if (text.Length > 2000) throw new DomainException("Paylaşım en fazla 2000 karakter olabilir.");
        var since = DateTime.UtcNow.AddHours(-1);
        if (await db.SocialPosts.CountAsync(p => p.AuthorId == me && p.CreatedAt >= since, ct) >= MaxPostsPerHour)
            throw new DomainException($"Saatte en fazla {MaxPostsPerHour} paylaşım yapılabilir. Biraz sonra tekrar deneyin.");

        var post = new SocialPost { AuthorId = me, Body = text };
        if (image is not null)
        {
            var ext = Path.GetExtension(imageName ?? "");
            if (!ImageTypes.TryGetValue(ext, out var type)) throw new DomainException("Yalnızca PNG, JPG veya WEBP görsel eklenebilir.");
            using var buffer = new MemoryStream();
            await image.CopyToAsync(buffer, ct);
            if (buffer.Length == 0 || buffer.Length > MaxImageBytes) throw new DomainException("Görsel en fazla 3 MB olabilir.");
            if (!Tickets.AttachmentRules.SignatureMatches(ext, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 4096))))
                throw new DomainException("Görselin içeriği uzantısıyla uyuşmuyor.");
            post.ImageKey = Guid.NewGuid().ToString("N");
            post.ImageContentType = type;
            buffer.Position = 0;
            await storage.SaveAsync(post.ImageKey, buffer, ct);
        }
        db.SocialPosts.Add(post);
        await db.SaveChangesAsync(ct);
        audit.Add("social.post_created", "social_post", post.Id, new { hasImage = post.ImageKey != null });
        await db.SaveChangesAsync(ct);
        return await GetAsync(post.Id, ct);
    }

    public async Task DeleteAsync(int postId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var post = await VisiblePostAsync(postId, ct);
        if (post.AuthorId != me) throw new ForbiddenException("Yalnızca kendi paylaşımınızı silebilirsiniz. Uygunsuz içeriği şikayet edin.");
        var key = post.ImageKey;
        db.SocialPosts.Remove(post);
        audit.Add("social.post_deleted", "social_post", postId);
        await db.SaveChangesAsync(ct);
        if (key is not null) await storage.DeleteAsync(key, ct);
    }

    public async Task<SocialPostDto> ToggleLikeAsync(int postId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.SocialPost);
        var me = currentUser.RequireUserId();
        await VisiblePostAsync(postId, ct);
        var like = await db.SocialLikes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == me, ct);
        if (like is null) db.SocialLikes.Add(new SocialLike { PostId = postId, UserId = me });
        else db.SocialLikes.Remove(like);
        await db.SaveChangesAsync(ct);
        return await GetAsync(postId, ct);
    }

    public async Task<IReadOnlyList<SocialCommentDto>> CommentsAsync(int postId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        await VisiblePostAsync(postId, ct);
        var moderate = CanModerate;
        return await db.SocialComments.AsNoTracking().Where(c => c.PostId == postId && c.RemovedAt == null).OrderBy(c => c.CreatedAt).Take(200)
            .Select(c => new SocialCommentDto(c.Id,
                new SocialAuthorDto(c.Author.Id, c.Author.FullName, c.Author.Position == null ? null : c.Author.Position.Title, c.Author.Department == null ? null : c.Author.Department.Name,
                    c.Author.Profile != null && c.Author.Profile.PhotoKey != null, c.Author.Profile == null ? null : c.Author.Profile.PhotoKey),
                c.Body, c.CreatedAt, c.AuthorId == me || moderate))
            .ToListAsync(ct);
    }

    public async Task<SocialCommentDto> CommentAsync(int postId, string body, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.SocialPost);
        var me = currentUser.RequireUserId();
        var post = await VisiblePostAsync(postId, ct);
        var text = Text.Clean(body);
        if (text.Length == 0) throw new DomainException("Yorum boş olamaz.");
        var comment = new SocialComment { PostId = postId, AuthorId = me, Body = text };
        db.SocialComments.Add(comment);
        if (post.AuthorId != me)
            db.Notifications.Add(new Notification { UserId = post.AuthorId, Title = $"{currentUser.FullName} paylaşımınıza yorum yaptı", Message = text.Length > 140 ? text[..140] + "…" : text, Link = $"/social?post={postId}" });
        await db.SaveChangesAsync(ct);
        return (await CommentsAsync(postId, ct)).First(c => c.Id == comment.Id);
    }

    public async Task DeleteCommentAsync(int commentId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var c = await db.SocialComments.FirstOrDefaultAsync(x => x.Id == commentId && x.RemovedAt == null, ct) ?? throw NotFoundException.For("Yorum", commentId);
        if (c.AuthorId == me) db.SocialComments.Remove(c);
        else if (CanModerate)
        {
            c.RemovedAt = DateTime.UtcNow;
            c.RemovedById = me;
            audit.Add("social.comment_removed", "social_comment", commentId, new { c.PostId });
        }
        else throw new ForbiddenException("Yalnızca kendi yorumunuzu silebilirsiniz.");
        await db.SaveChangesAsync(ct);
    }

    public async Task ReportAsync(int postId, string reason, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var post = await VisiblePostAsync(postId, ct);
        if (post.AuthorId == me) throw new DomainException("Kendi paylaşımınızı şikayet edemezsiniz; silebilirsiniz.");
        if (await db.SocialReports.AnyAsync(r => r.PostId == postId && r.ReporterId == me && r.ResolvedAt == null, ct)) return; // aynı kişiden tekrar sayılmaz
        db.SocialReports.Add(new SocialReport { PostId = postId, ReporterId = me, Reason = Text.Clean(reason) });
        var moderators = await db.Users.Where(u => u.IsActive && u.Id != me && u.Roles.Any(r => r.Role.Permissions.Any(p => p.Permission.Key == Permissions.SocialModerate)))
            .Select(u => u.Id).Take(20).ToListAsync(ct);
        foreach (var m in moderators)
            db.Notifications.Add(new Notification { UserId = m, Title = "Sosyal akışta şikayet edilen paylaşım", Message = "Bir paylaşım uygunsuz olarak bildirildi; incelemeniz bekleniyor.", Link = "/social?tab=moderation" });
        audit.Add("social.post_reported", "social_post", postId);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(int postId, string reason, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.SocialModerate);
        var me = currentUser.RequireUserId();
        var post = await VisiblePostAsync(postId, ct);
        var why = Text.CleanOrNull(reason) ?? throw new DomainException("Kaldırma gerekçesi yazın; paylaşım sahibine bildirilir.");
        post.RemovedAt = DateTime.UtcNow;
        post.RemovedById = me;
        post.RemovedReason = why;
        foreach (var r in await db.SocialReports.Where(r => r.PostId == postId && r.ResolvedAt == null).ToListAsync(ct))
            (r.ResolvedAt, r.ResolvedById, r.Resolution) = (DateTime.UtcNow, me, "removed");
        db.Notifications.Add(new Notification { UserId = post.AuthorId, Title = "Paylaşımınız kaldırıldı", Message = $"Gerekçe: {why}", Link = "/social" });
        audit.Add("social.post_removed", "social_post", postId, new { reason = why });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SocialReportDto>> PendingReportsAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.SocialModerate);
        return await db.SocialReports.AsNoTracking().Where(r => r.ResolvedAt == null).OrderBy(r => r.CreatedAt).Take(100)
            .Select(r => new SocialReportDto(r.Id, r.PostId, r.Post.Body.Length > 200 ? r.Post.Body.Substring(0, 200) + "…" : r.Post.Body,
                r.Post.Author.FullName, r.Reporter.FullName, r.Reason, r.CreatedAt, r.Post.RemovedAt != null))
            .ToListAsync(ct);
    }

    public async Task ResolveReportAsync(int reportId, bool removePost, string? reason, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.SocialModerate);
        var me = currentUser.RequireUserId();
        var report = await db.SocialReports.FirstOrDefaultAsync(r => r.Id == reportId && r.ResolvedAt == null, ct) ?? throw NotFoundException.For("Şikayet", reportId);
        if (removePost) { await RemoveAsync(report.PostId, reason ?? report.Reason, ct); return; }
        foreach (var r in await db.SocialReports.Where(r => r.PostId == report.PostId && r.ResolvedAt == null).ToListAsync(ct))
            (r.ResolvedAt, r.ResolvedById, r.Resolution) = (DateTime.UtcNow, me, "dismissed");
        audit.Add("social.report_dismissed", "social_post", report.PostId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SocialImage> ImageAsync(int postId, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var p = await db.SocialPosts.AsNoTracking().Where(x => x.Id == postId && (x.RemovedAt == null || CanModerate))
            .Select(x => new { x.ImageKey, x.ImageContentType }).FirstOrDefaultAsync(ct);
        if (p?.ImageKey is null) throw NotFoundException.For("Görsel", postId);
        var stream = await storage.OpenReadAsync(p.ImageKey, ct) ?? throw NotFoundException.For("Görsel", postId);
        return new SocialImage(stream, p.ImageContentType ?? "image/png");
    }
}

/* =========================================================
   Mesajlaşma: birebir. Yalnızca aktif çalışanlar arasında; mesajı yalnızca iki taraf görür.
   ========================================================= */

public sealed record ConversationDto(int Id, SocialAuthorDto Other, string? LastMessage, bool LastIsMine, DateTime LastMessageAt, int Unread);
public sealed record DirectMessageDto(long Id, int SenderId, string Body, DateTime CreatedAt, DateTime? ReadAt, bool IsMine);
public sealed record SendMessageRequest(string Body);

public interface IMessageService
{
    Task<IReadOnlyList<ConversationDto>> ConversationsAsync(CancellationToken ct = default);
    Task<int> UnreadCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DirectMessageDto>> WithAsync(int otherUserId, long? beforeId, CancellationToken ct = default);
    Task<DirectMessageDto> SendAsync(int toUserId, string body, CancellationToken ct = default);
    Task MarkReadAsync(int otherUserId, CancellationToken ct = default);
}

internal sealed class MessageService(IAppDbContext db, ICurrentUser currentUser, IRealtimeNotifier realtime) : IMessageService
{
    public const int MaxPerMinute = 30;

    private static (int A, int B) Pair(int x, int y) => x < y ? (x, y) : (y, x);

    public async Task<IReadOnlyList<ConversationDto>> ConversationsAsync(CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var rows = await db.Conversations.AsNoTracking().Where(c => c.UserAId == me || c.UserBId == me).OrderByDescending(c => c.LastMessageAt).Take(100)
            .Select(c => new
            {
                c.Id, c.LastMessageAt,
                OtherId = c.UserAId == me ? c.UserBId : c.UserAId,
                LastBody = c.Messages.OrderByDescending(m => m.Id).Select(m => m.Body).FirstOrDefault(),
                LastSender = c.Messages.OrderByDescending(m => m.Id).Select(m => (int?)m.SenderId).FirstOrDefault(),
                Unread = c.Messages.Count(m => m.SenderId != me && m.ReadAt == null),
            })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.OtherId).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(SocialService.Author).ToDictionaryAsync(p => p.Id, ct);
        return rows.Where(r => people.ContainsKey(r.OtherId)).Select(r => new ConversationDto(r.Id, people[r.OtherId],
            r.LastBody is { } b ? (b.Length > 80 ? b[..80] + "…" : b) : null, r.LastSender == me, r.LastMessageAt, r.Unread)).ToList();
    }

    public Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        return db.DirectMessages.CountAsync(m => m.SenderId != me && m.ReadAt == null && (m.Conversation.UserAId == me || m.Conversation.UserBId == me), ct);
    }

    public async Task<IReadOnlyList<DirectMessageDto>> WithAsync(int otherUserId, long? beforeId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var (a, b) = Pair(me, otherUserId);
        var q = db.DirectMessages.AsNoTracking().Where(m => m.Conversation.UserAId == a && m.Conversation.UserBId == b);
        if (beforeId is not null) q = q.Where(m => m.Id < beforeId);
        var page = await q.OrderByDescending(m => m.Id).Take(50).Select(m => new DirectMessageDto(m.Id, m.SenderId, m.Body, m.CreatedAt, m.ReadAt, m.SenderId == me)).ToListAsync(ct);
        page.Reverse();
        return page;
    }

    public async Task<DirectMessageDto> SendAsync(int toUserId, string body, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        if (toUserId == me) throw new DomainException("Kendinize mesaj gönderemezsiniz.");
        if (!await db.Users.AnyAsync(u => u.Id == toUserId && u.IsActive, ct)) throw NotFoundException.For("Çalışan", toUserId);
        var text = Text.Clean(body);
        if (text.Length == 0) throw new DomainException("Mesaj boş olamaz.");
        var since = DateTime.UtcNow.AddMinutes(-1);
        if (await db.DirectMessages.CountAsync(m => m.SenderId == me && m.CreatedAt >= since, ct) >= MaxPerMinute)
            throw new DomainException("Çok hızlı mesaj gönderiyorsunuz. Biraz bekleyip tekrar deneyin.");
        var (a, b) = Pair(me, toUserId);
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.UserAId == a && c.UserBId == b, ct)
                           ?? db.Conversations.Add(new Conversation { UserAId = a, UserBId = b }).Entity;
        var message = new DirectMessage { Conversation = conversation, SenderId = me, Body = text };
        conversation.LastMessageAt = message.CreatedAt;
        db.DirectMessages.Add(message);
        await db.SaveChangesAsync(ct);
        await realtime.NotifyAsync([toUserId], "message", currentUser.FullName, ct);
        return new DirectMessageDto(message.Id, me, message.Body, message.CreatedAt, null, true);
    }

    public async Task MarkReadAsync(int otherUserId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var (a, b) = Pair(me, otherUserId);
        var unread = await db.DirectMessages.Where(m => m.Conversation.UserAId == a && m.Conversation.UserBId == b && m.SenderId != me && m.ReadAt == null).ToListAsync(ct);
        if (unread.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var m in unread) m.ReadAt = now;
        await db.SaveChangesAsync(ct);
        await realtime.NotifyAsync([otherUserId], "message-read", null, ct); // gönderen "okundu" işaretini görsün
    }
}

/* =========================================================
   Sık sorulan sorular: herkes okur; faq.manage yönetir.
   ========================================================= */

public sealed record FaqItemDto(int Id, string Category, string Question, string Answer, int SortOrder, bool IsPublished);
public sealed record SaveFaqRequest(string Category, string Question, string Answer, int SortOrder, bool IsPublished);

public interface IFaqService
{
    Task<IReadOnlyList<FaqItemDto>> ListAsync(string? search, CancellationToken ct = default);
    Task<FaqItemDto> SaveAsync(int? id, SaveFaqRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

internal sealed class FaqService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit) : IFaqService
{
    private static FaqItemDto ToDto(FaqItem f) => new(f.Id, f.Category, f.Question, f.Answer, f.SortOrder, f.IsPublished);

    public async Task<IReadOnlyList<FaqItemDto>> ListAsync(string? search, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var q = db.FaqItems.AsNoTracking();
        if (!currentUser.Has(Permissions.FaqManage)) q = q.Where(f => f.IsPublished);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(f => f.Question.ToLower().Contains(s) || f.Answer.ToLower().Contains(s) || f.Category.ToLower().Contains(s));
        }
        return (await q.OrderBy(f => f.Category).ThenBy(f => f.SortOrder).ThenBy(f => f.Id).Take(300).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<FaqItemDto> SaveAsync(int? id, SaveFaqRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.FaqManage);
        var f = id is null ? db.FaqItems.Add(new FaqItem()).Entity : await db.FaqItems.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Soru", id);
        f.Category = Text.Clean(r.Category);
        f.Question = Text.Clean(r.Question);
        f.Answer = Text.Clean(r.Answer);
        f.SortOrder = r.SortOrder;
        f.IsPublished = r.IsPublished;
        if (id is not null) f.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        audit.Add(id is null ? "faq.created" : "faq.updated", "faq", f.Id);
        await db.SaveChangesAsync(ct);
        return ToDto(f);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.FaqManage);
        var f = await db.FaqItems.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Soru", id);
        db.FaqItems.Remove(f);
        audit.Add("faq.deleted", "faq", id);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator() => RuleFor(x => x.Body).NotEmpty().WithMessage("Yorum boş olamaz.").MaximumLength(1000).NoMarkup();
}
public sealed class ReportPostRequestValidator : AbstractValidator<ReportPostRequest>
{
    public ReportPostRequestValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Şikayet nedenini yazın.").MaximumLength(500).NoMarkup();
}
public sealed class RemoveContentRequestValidator : AbstractValidator<RemoveContentRequest>
{
    public RemoveContentRequestValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Kaldırma gerekçesi yazın.").MaximumLength(500).NoMarkup();
}
public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator() => RuleFor(x => x.Body).NotEmpty().WithMessage("Mesaj boş olamaz.").MaximumLength(2000).NoMarkup();
}
public sealed class SaveFaqRequestValidator : AbstractValidator<SaveFaqRequest>
{
    public SaveFaqRequestValidator()
    {
        RuleFor(x => x.Category).NotEmpty().WithMessage("Kategori zorunludur.").MaximumLength(60).NoMarkup();
        RuleFor(x => x.Question).NotEmpty().WithMessage("Soru zorunludur.").MaximumLength(300).NoMarkup();
        RuleFor(x => x.Answer).NotEmpty().WithMessage("Cevap zorunludur.").MaximumLength(5000).NoMarkup();
    }
}
