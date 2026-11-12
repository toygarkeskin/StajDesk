using System.Text;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Admin;
using StajDesk.Application.Features.Social;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Aşama 6.3: sosyal akış (paylaşım, beğeni, yorum, şikayet, moderasyon), birebir mesajlaşma, SSS.</summary>
public class SocialMessagingTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly MemoryStorage _storage = new();
    private readonly RecordingNotifier _realtime = new();
    private readonly User _ayse, _zeynep, _mod, _inactive, _agent;

    private sealed class RecordingNotifier : IRealtimeNotifier
    {
        public List<(int[] Users, string Kind)> Sent { get; } = [];
        public Task NotifyAsync(IReadOnlyCollection<int> userIds, string kind, string? title, CancellationToken ct = default)
        {
            Sent.Add((userIds.ToArray(), kind));
            return Task.CompletedTask;
        }
    }

    public SocialMessagingTests()
    {
        var roles = _db.SeedRoles();
        User U(string name, string role, bool active = true)
        {
            var u = new User { FullName = name, Email = $"{name}@test", IsActive = active };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        _ayse = U("Ayşe", RoleNames.User);
        _zeynep = U("Zeynep", RoleNames.User);
        _mod = U("Moderatör", RoleNames.SystemAdmin);
        _inactive = U("Ayrılan", RoleNames.User, active: false);
        _agent = U("Elif", RoleNames.Agent);
        _db.AddRange(_ayse, _zeynep, _mod, _inactive, _agent);
        _db.SaveChanges();
    }

    private SocialService Social() => new(_db, _current, new NoAudit(), _storage);
    private MessageService Messages() => new(_db, _current, _realtime);
    private FaqService Faq() => new(_db, _current, new NoAudit());

    private async Task<int> Post(User u, string text)
    {
        _current.As(u, RoleNames.User);
        return (await Social().CreateAsync(text, null, null, 0)).Id;
    }

    [Fact]
    public async Task Posts_likes_and_comments_work_and_notify_the_author()
    {
        var id = await Post(_ayse, "Yeni mağazamız açıldı!");
        _current.As(_zeynep, RoleNames.User);
        var liked = await Social().ToggleLikeAsync(id);
        Assert.Equal((1, true), (liked.LikeCount, liked.LikedByMe));
        Assert.Equal(0, (await Social().ToggleLikeAsync(id)).LikeCount); // ikinci kez geri alır
        await Social().CommentAsync(id, "Tebrikler");
        Assert.Contains(_db.Notifications, n => n.UserId == _ayse.Id && n.Link == $"/social?post={id}");

        var feed = await Social().FeedAsync(new SimplePagedQuery());
        var post = Assert.Single(feed.Items);
        Assert.Equal(1, post.CommentCount);
        Assert.False(post.CanDelete); // başkasının paylaşımı
        await Assert.ThrowsAsync<ForbiddenException>(() => Social().DeleteAsync(id));
    }

    [Fact]
    public async Task Markup_empty_and_flood_posts_are_rejected()
    {
        _current.As(_ayse, RoleNames.User);
        await Assert.ThrowsAsync<DomainException>(() => Social().CreateAsync("   ", null, null, 0));
        // HTML etiketleri temizlenir (saklanan metinde etiket kalmaz)
        var clean = await Social().CreateAsync("<script>alert(1)</script>Merhaba", null, null, 0);
        Assert.DoesNotContain("<script>", clean.Body);
        for (var i = 1; i < SocialService.MaxPostsPerHour; i++) await Social().CreateAsync($"Paylaşım {i}", null, null, 0);
        await Assert.ThrowsAsync<DomainException>(() => Social().CreateAsync("Bir tane daha", null, null, 0));
    }

    [Fact]
    public async Task Images_must_be_real_images()
    {
        _current.As(_ayse, RoleNames.User);
        await Assert.ThrowsAsync<DomainException>(() => Social().CreateAsync("Belge", "rapor.pdf", new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4")), 8));
        await Assert.ThrowsAsync<DomainException>(() => Social().CreateAsync("Sahte", "foto.png", new MemoryStream(Encoding.ASCII.GetBytes("not an image")), 12));
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var p = await Social().CreateAsync("Fotoğraf", "foto.png", new MemoryStream(png), png.Length);
        Assert.True(p.HasImage);
        _current.As(_zeynep, RoleNames.User);
        Assert.Equal("image/png", (await Social().ImageAsync(p.Id)).ContentType);
    }

    [Fact]
    public async Task Reports_reach_moderators_and_removal_needs_permission_and_reason()
    {
        var id = await Post(_ayse, "Uygunsuz içerik");
        _current.As(_zeynep, RoleNames.User);
        await Social().ReportAsync(id, "Kurumsal değil");
        await Social().ReportAsync(id, "Tekrar"); // aynı kişiden ikinci şikayet sayılmaz
        Assert.Single(_db.SocialReports);
        Assert.Contains(_db.Notifications, n => n.UserId == _mod.Id && n.Link == "/social?tab=moderation");
        await Assert.ThrowsAsync<ForbiddenException>(() => Social().RemoveAsync(id, "x"));
        await Assert.ThrowsAsync<ForbiddenException>(() => Social().PendingReportsAsync());

        _current.As(_mod, RoleNames.SystemAdmin);
        var report = Assert.Single(await Social().PendingReportsAsync());
        await Social().ResolveReportAsync(report.Id, removePost: true, "Kurumsal kullanım kuralına aykırı");
        Assert.Empty(await Social().PendingReportsAsync());
        Assert.Contains(_db.Notifications, n => n.UserId == _ayse.Id && n.Title == "Paylaşımınız kaldırıldı" && n.Message.Contains("aykırı"));

        _current.As(_zeynep, RoleNames.User);
        Assert.Empty((await Social().FeedAsync(new SimplePagedQuery())).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => Social().CommentAsync(id, "Hâlâ orada mı?"));
    }

    [Fact]
    public async Task Dismissed_report_keeps_the_post_and_moderators_can_remove_comments()
    {
        var id = await Post(_ayse, "Normal paylaşım");
        _current.As(_zeynep, RoleNames.User);
        await Social().ReportAsync(id, "Yanlışlıkla");
        var comment = await Social().CommentAsync(id, "Yorum");
        await Assert.ThrowsAsync<ForbiddenException>(() => { _current.As(_agent, RoleNames.Agent); return Social().DeleteCommentAsync(comment.Id); });

        _current.As(_mod, RoleNames.SystemAdmin);
        await Social().ResolveReportAsync((await Social().PendingReportsAsync()).Single().Id, removePost: false, null);
        await Social().DeleteCommentAsync(comment.Id);
        Assert.Single((await Social().FeedAsync(new SimplePagedQuery())).Items);
        Assert.Empty(await Social().CommentsAsync(id));
    }

    // ---------- Mesajlaşma ----------

    [Fact]
    public async Task Direct_messages_are_private_counted_and_pushed_in_realtime()
    {
        _current.As(_ayse, RoleNames.User);
        await Messages().SendAsync(_zeynep.Id, "Merhaba Zeynep");
        await Messages().SendAsync(_zeynep.Id, "Toplantı 14:00'te");
        Assert.Equal(2, _realtime.Sent.Count(s => s.Kind == "message" && s.Users.SequenceEqual([_zeynep.Id])));
        Assert.Single(_db.Conversations); // tekil konuşma

        _current.As(_zeynep, RoleNames.User);
        Assert.Equal(2, await Messages().UnreadCountAsync());
        var conv = Assert.Single(await Messages().ConversationsAsync());
        Assert.Equal(("Ayşe", 2, false), (conv.Other.FullName, conv.Unread, conv.LastIsMine));
        Assert.Equal(["Merhaba Zeynep", "Toplantı 14:00'te"], (await Messages().WithAsync(_ayse.Id, null)).Select(m => m.Body).ToArray());
        await Messages().MarkReadAsync(_ayse.Id);
        Assert.Equal(0, await Messages().UnreadCountAsync());
        Assert.Contains(_realtime.Sent, s => s.Kind == "message-read" && s.Users.SequenceEqual([_ayse.Id]));

        // Üçüncü kişi bu konuşmayı göremez
        _current.As(_agent, RoleNames.Agent);
        Assert.Empty(await Messages().ConversationsAsync());
        Assert.Empty(await Messages().WithAsync(_ayse.Id, null));
    }

    [Fact]
    public async Task Messages_to_self_or_inactive_users_and_floods_are_rejected()
    {
        _current.As(_ayse, RoleNames.User);
        await Assert.ThrowsAsync<DomainException>(() => Messages().SendAsync(_ayse.Id, "Ben"));
        await Assert.ThrowsAsync<NotFoundException>(() => Messages().SendAsync(_inactive.Id, "Selam"));
        for (var i = 0; i < MessageService.MaxPerMinute; i++) await Messages().SendAsync(_zeynep.Id, $"Mesaj {i}");
        await Assert.ThrowsAsync<DomainException>(() => Messages().SendAsync(_zeynep.Id, "Fazla"));
    }

    // ---------- SSS ----------

    [Fact]
    public async Task Faq_is_searchable_and_only_managers_see_drafts_or_edit()
    {
        _current.As(_agent, RoleNames.Agent); // destek personeli SSS yönetir
        await Faq().SaveAsync(null, new SaveFaqRequest("İzin", "İzin nasıl alınır?", "İzin talepleri ekranından.", 1, true));
        await Faq().SaveAsync(null, new SaveFaqRequest("İzin", "Taslak soru", "Henüz yayında değil.", 2, false));

        _current.As(_ayse, RoleNames.User);
        Assert.Equal(["İzin nasıl alınır?"], (await Faq().ListAsync(null)).Select(f => f.Question).ToArray());
        Assert.Single(await Faq().ListAsync("talepleri"));
        Assert.Empty(await Faq().ListAsync("taslak"));
        await Assert.ThrowsAsync<ForbiddenException>(() => Faq().SaveAsync(null, new SaveFaqRequest("x", "y", "z", 1, true)));
    }
}
