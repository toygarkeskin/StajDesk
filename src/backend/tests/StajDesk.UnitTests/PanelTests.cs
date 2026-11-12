using System.Text;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Tickets;
using StajDesk.Application.Features.Workspace;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Panel (duyurular, nöbet, ana sayfa), talep ekleri ve arama.</summary>
public class PanelTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly NoAudit _audit = new();
    private readonly MemoryStorage _storage = new();
    private readonly User _user, _otherStoreUser, _agent, _admin;
    private readonly Store _storeA, _storeB;
    private readonly Category _category;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    public PanelTests()
    {
        var roles = _db.SeedRoles();
        _storeA = new Store { Code = "A", Name = "Mağaza A" };
        _storeB = new Store { Code = "B", Name = "Mağaza B" };
        _db.Stores.AddRange(_storeA, _storeB);
        _db.SaveChanges();

        User U(string name, string role, Store? store)
        {
            var u = new User { FullName = name, Email = $"{name}@test", StoreId = store?.Id };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        _user = U("ayse", RoleNames.User, _storeA);
        _otherStoreUser = U("zeynep", RoleNames.User, _storeB);
        _agent = U("elif", RoleNames.Agent, null);
        _admin = U("admin", RoleNames.SystemAdmin, null);
        _category = new Category { Name = "Donanım" };
        _db.AddRange(_user, _otherStoreUser, _agent, _admin, _category);
        _db.SaveChanges();
    }

    private WorkspaceService Workspace() => new(_db, _current, _audit, TimeProvider.System);

    private Ticket NewTicket(User requester, string title = "Yazıcı çalışmıyor", string description = "Ofisteki yazıcı kâğıt sıkıştırıyor.")
    {
        var t = new Ticket(title, description, requester.Id, _category.Id, storeId: requester.StoreId);
        _db.Tickets.Add(t);
        _db.SaveChanges();
        return t;
    }

    private Task<TicketAttachmentDto> Upload(int ticketId, string name, byte[] content) =>
        new UploadAttachmentHandler(_db, _current, _storage, _audit)
            .HandleAsync(new UploadAttachmentCommand(ticketId, name, content.Length, new MemoryStream(content)), default);

    // ---------- Duyurular ----------

    [Fact]
    public async Task Magaza_duyurusu_yalnizca_o_magazaya_gorunur()
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        await Workspace().SaveAnnouncementAsync(null, new UpsertAnnouncementRequest("Genel duyuru", "Herkese açık metin"));
        await Workspace().SaveAnnouncementAsync(null, new UpsertAnnouncementRequest("A mağazası", "Yalnızca A", StoreId: _storeA.Id));

        _current.As(_user, RoleNames.User);
        Assert.Equal(2, (await Workspace().GetAnnouncementsAsync(false)).Count);

        _current.As(_otherStoreUser, RoleNames.User);
        var list = await Workspace().GetAnnouncementsAsync(false);
        Assert.Single(list);
        Assert.Equal("Genel duyuru", list[0].Title);
    }

    [Fact]
    public async Task Suresi_dolmus_ve_ileri_tarihli_duyuru_listelenmez()
    {
        _db.Announcements.AddRange(
            new Announcement { Title = "Eski", Body = "x", AuthorId = _admin.Id, PublishAt = DateTime.UtcNow.AddDays(-5), ExpiresAt = DateTime.UtcNow.AddDays(-1) },
            new Announcement { Title = "İleri tarihli", Body = "x", AuthorId = _admin.Id, PublishAt = DateTime.UtcNow.AddDays(2) },
            new Announcement { Title = "Güncel", Body = "x", AuthorId = _admin.Id, PublishAt = DateTime.UtcNow.AddHours(-1) });
        _db.SaveChanges();

        _current.As(_user, RoleNames.User);
        Assert.Equal(["Güncel"], (await Workspace().GetAnnouncementsAsync(includeInactive: true)).Select(a => a.Title));

        _current.As(_admin, RoleNames.SystemAdmin);
        Assert.Equal(3, (await Workspace().GetAnnouncementsAsync(includeInactive: true)).Count);
    }

    [Fact]
    public async Task Kullanici_duyuru_yayinlayamaz()
    {
        _current.As(_user, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => Workspace().SaveAnnouncementAsync(null, new UpsertAnnouncementRequest("Deneme", "Metin")));
    }

    [Fact]
    public void Duyuru_dogrulamasi_html_ve_ters_tarihi_reddeder()
    {
        var v = new UpsertAnnouncementRequestValidator();
        Assert.False(v.Validate(new UpsertAnnouncementRequest("<script>alert(1)</script>", "Metin")).IsValid);
        Assert.False(v.Validate(new UpsertAnnouncementRequest("Başlık", "Metin", PublishAt: DateTime.UtcNow, ExpiresAt: DateTime.UtcNow.AddDays(-1))).IsValid);
        Assert.True(v.Validate(new UpsertAnnouncementRequest("Başlık", "Metin")).IsValid);
    }

    // ---------- Nöbet ----------

    [Fact]
    public async Task Nobete_yalnizca_destek_personeli_yazilabilir_ve_cakisma_engellenir()
    {
        _current.As(_agent, RoleNames.Agent);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Assert.ThrowsAsync<DomainException>(() => Workspace().CreateOnCallAsync(new CreateOnCallShiftRequest(_user.Id, today, today.AddDays(6), null)));

        var shift = await Workspace().CreateOnCallAsync(new CreateOnCallShiftRequest(_agent.Id, today, today.AddDays(6), "Acil"));
        Assert.Equal(_agent.Id, shift.UserId);

        await Assert.ThrowsAsync<ConflictException>(() => Workspace().CreateOnCallAsync(new CreateOnCallShiftRequest(_agent.Id, today.AddDays(3), today.AddDays(9), null)));
    }

    [Fact]
    public async Task Kullanici_nobet_cizelgesini_degistiremez()
    {
        _current.As(_user, RoleNames.User);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.ThrowsAsync<ForbiddenException>(() => Workspace().CreateOnCallAsync(new CreateOnCallShiftRequest(_agent.Id, today, today, null)));
    }

    // ---------- Ana sayfa ----------

    [Fact]
    public async Task Ana_sayfa_onay_bekleyen_talepleri_ve_gorevleri_gosterir()
    {
        var t = NewTicket(_user);
        t.AssignTo(_agent.Id);
        t.ChangeStatus(TicketStatus.Resolved);
        var open = NewTicket(_user, "Monitör titriyor");
        open.AssignTo(_agent.Id);
        _db.SaveChanges();

        _current.As(_user, RoleNames.User);
        var home = await Workspace().GetHomeAsync();
        Assert.Equal(t.Id, Assert.Single(home.AwaitingApproval).Id);
        Assert.Empty(home.MyTasks); // kullanıcı talep yönetmez
        Assert.Equal(1, home.Counters.AwaitingMyApproval);

        _current.As(_agent, RoleNames.Agent);
        home = await Workspace().GetHomeAsync();
        Assert.Equal(open.Id, Assert.Single(home.MyTasks).Id); // çözülmüş olan görev listesinde değil
    }

    // ---------- Ekler ----------

    [Fact]
    public async Task Gecerli_png_eklenir_ve_gecmise_yazilir()
    {
        var t = NewTicket(_user);
        _current.As(_user, RoleNames.User);
        var dto = await Upload(t.Id, "ekran görüntüsü.png", Png);

        Assert.Equal("image/png", dto.ContentType);
        Assert.Single(_storage.Files);
        Assert.Contains(_db.TicketHistories, h => h.TicketId == t.Id && h.Action == TicketHistoryAction.AttachmentAdded);
    }

    [Fact]
    public async Task Uzantisi_png_olan_calistirilabilir_dosya_reddedilir()
    {
        var t = NewTicket(_user);
        _current.As(_user, RoleNames.User);
        var exe = "MZ\u0090\0\u0003\0\0\0"u8.ToArray();
        await Assert.ThrowsAsync<DomainException>(() => Upload(t.Id, "resim.png", exe));
        await Assert.ThrowsAsync<DomainException>(() => Upload(t.Id, "kurulum.exe", exe));
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Bes_megabayttan_buyuk_dosya_reddedilir()
    {
        var t = NewTicket(_user);
        _current.As(_user, RoleNames.User);
        var big = new byte[AttachmentRules.MaxBytes + 1];
        Encoding.ASCII.GetBytes("plain").CopyTo(big, 0);
        Array.Fill(big, (byte)'a', 5, big.Length - 5);
        await Assert.ThrowsAsync<DomainException>(() => Upload(t.Id, "log.txt", big));
    }

    [Fact]
    public async Task Baska_magazanin_talebine_dosya_eklenemez_ve_indirilemez()
    {
        var t = NewTicket(_user);
        _current.As(_user, RoleNames.User);
        var dto = await Upload(t.Id, "not.txt", "Merhaba"u8.ToArray());

        _current.As(_otherStoreUser, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => Upload(t.Id, "x.txt", "x"u8.ToArray()));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new DownloadAttachmentHandler(_db, _current, _storage).HandleAsync(new DownloadAttachmentQuery(t.Id, dto.Id), default));
    }

    [Fact]
    public void Dosya_adi_yol_ve_ozel_karakterlerden_arindirilir()
    {
        Assert.Equal("passwd", AttachmentRules.SafeFileName("../../etc/passwd"));
        Assert.Equal("rapor.pdf", AttachmentRules.SafeFileName("C:\\Users\\x\\rapor.pdf"));
        Assert.Equal("ab.txt", AttachmentRules.SafeFileName("a<b>.txt"));
    }

    // ---------- Arama ----------

    [Fact]
    public async Task Arama_numara_ve_metinle_calisir()
    {
        var printer = NewTicket(_user, "Yazıcı kâğıt sıkıştırıyor");
        NewTicket(_user, "Monitör görüntü vermiyor", "Ekran siyah kalıyor, kablo değiştirdim.");
        _current.As(_agent, RoleNames.Agent);

        var byId = await new ListTicketsHandler(_db, _current, new ContainsTicketSearchProxy()).HandleAsync(new ListTicketsQuery(new TicketQuery { Search = $"#{printer.Id}" }), default);
        Assert.Equal(printer.Id, Assert.Single(byId.Items).Id);

        var byText = await new ListTicketsHandler(_db, _current).HandleAsync(new ListTicketsQuery(new TicketQuery { Search = "kablo" }), default);
        Assert.Equal("Monitör görüntü vermiyor", Assert.Single(byText.Items).Title);
    }
}

/// <summary>Testte dosyalar bellekte tutulur.</summary>
internal sealed class MemoryStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        Files[key] = ms.ToArray();
    }
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var b) ? new MemoryStream(b) : null);
    public Task DeleteAsync(string key, CancellationToken ct = default) { Files.Remove(key); return Task.CompletedTask; }
}

/// <summary>Arama sözleşmesinin dışarıdan verilebildiğini gösterir (gerçekte PostgreSQL tam metin araması verilir).</summary>
internal sealed class ContainsTicketSearchProxy : ITicketSearch
{
    public IQueryable<Ticket> Apply(IQueryable<Ticket> query, string term)
    {
        var s = term.Trim().TrimStart('#');
        return int.TryParse(s, out var id) ? query.Where(t => t.Id == id) : query.Where(t => t.Title.Contains(s) || t.Description.Contains(s));
    }
}
