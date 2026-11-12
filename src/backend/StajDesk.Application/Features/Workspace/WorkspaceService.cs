using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Tickets;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Workspace;

/* =========================================================
   Ana sayfa paneli (portal): karşılama, duyurular, nöbetçi personel,
   görevlerim, onay bekleyenler, son hareketler.
   ========================================================= */

public sealed record WelcomeDto(int UserId, string FullName, string? Department, string? StoreName, IReadOnlyList<string> Roles, DateTime MemberSince);
public sealed record AnnouncementDto(int Id, string Title, string Body, AnnouncementLevel Level, bool IsPinned, DateTime PublishAt, DateTime? ExpiresAt,
    int? StoreId, string? StoreName, string AuthorName,
    int? TargetDepartmentId = null, string? TargetDepartment = null, bool IsRead = false,
    string? AttachmentName = null, string? AttachmentContentType = null, long? AttachmentSize = null,
    /// <summary>Yalnızca duyuru yöneticisine: okuyan sayısı / hedef kitle</summary>
    int? ReadCount = null, int? AudienceCount = null);
public sealed record AnnouncementReaderDto(int UserId, string FullName, string? Department, DateTime ReadAt);
public sealed record AnnouncementReadersDto(int AudienceCount, IReadOnlyList<AnnouncementReaderDto> Readers);
public sealed record AnnouncementFile(Stream Content, string ContentType, string FileName);
public sealed record OnCallShiftDto(int Id, int UserId, string FullName, string Email, string? Phone, string? Department, DateOnly StartsOn, DateOnly EndsOn, string? Note, bool IsCurrent);
public sealed record ActivityDto(int TicketId, string TicketTitle, TicketHistoryAction Action, string? OldValue, string? NewValue, string? ActorName, DateTime CreatedAt);
public sealed record HomeCountersDto(int MyOpenTickets, int AwaitingMyApproval, int AssignedToMe, int MyAssets, int UnreadNotifications);

public sealed record HomeDto(
    WelcomeDto Welcome,
    HomeCountersDto Counters,
    IReadOnlyList<AnnouncementDto> Announcements,
    IReadOnlyList<OnCallShiftDto> OnCall,
    IReadOnlyList<TicketListItemDto> MyTasks,
    IReadOnlyList<TicketListItemDto> AwaitingApproval,
    IReadOnlyList<ActivityDto> Activity);

public sealed record UpsertAnnouncementRequest(string Title, string Body, AnnouncementLevel Level = AnnouncementLevel.Info, bool IsPinned = false,
    DateTime? PublishAt = null, DateTime? ExpiresAt = null, int? StoreId = null, int? TargetDepartmentId = null);
public sealed record CreateOnCallShiftRequest(int UserId, DateOnly StartsOn, DateOnly EndsOn, string? Note);

public interface IWorkspaceService
{
    Task<HomeDto> GetHomeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(bool includeInactive, CancellationToken ct = default);
    Task<AnnouncementDto> SaveAnnouncementAsync(int? id, UpsertAnnouncementRequest request, CancellationToken ct = default);
    Task DeleteAnnouncementAsync(int id, CancellationToken ct = default);
    Task MarkAnnouncementReadAsync(int id, CancellationToken ct = default);
    Task<AnnouncementReadersDto> AnnouncementReadersAsync(int id, CancellationToken ct = default);
    Task<AnnouncementDto> SetAnnouncementAttachmentAsync(int id, string fileName, long length, Stream content, CancellationToken ct = default);
    Task RemoveAnnouncementAttachmentAsync(int id, CancellationToken ct = default);
    Task<AnnouncementFile> OpenAnnouncementAttachmentAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<OnCallShiftDto>> GetOnCallAsync(CancellationToken ct = default);
    Task<OnCallShiftDto> CreateOnCallAsync(CreateOnCallShiftRequest request, CancellationToken ct = default);
    Task DeleteOnCallAsync(int id, CancellationToken ct = default);
}

internal sealed class WorkspaceService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, TimeProvider clock, IFileStorage? storage = null) : IWorkspaceService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    // Nöbet günleri şirketin saat dilimine göre (Docker konteyneri UTC çalışır).
    private static readonly TimeZoneInfo Zone = FindZone();
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Now, Zone));

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Türkiye", "Türkiye"); }
    }

    public async Task<HomeDto> GetHomeAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var me = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id, u.FullName, Department = u.Department == null ? null : u.Department.Name, StoreName = u.Store == null ? null : u.Store.Name, u.CreatedAt,
                Roles = u.Roles.Select(r => r.Role.DisplayName).ToList(),
            }).FirstAsync(ct);

        var canManage = currentUser.Has(Permissions.TicketsManage);
        var includeInternal = currentUser.Has(Permissions.CommentsInternal);
        var visible = db.Tickets.AsNoTracking().Visible(currentUser);

        var myTasks = canManage
            ? await db.Tickets.AsNoTracking()
                .Where(t => t.AssigneeId == userId && (t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected) && t.Status != TicketStatus.Resolved)
                .OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).Take(6).ToListItems(includeInternal).ToListAsync(ct)
            : [];
        var awaiting = await db.Tickets.AsNoTracking()
            .Where(t => t.RequesterId == userId && t.Status == TicketStatus.Resolved)
            .OrderByDescending(t => t.ResolvedAt).Take(5).ToListItems(includeInternal).ToListAsync(ct);

        var activity = await db.TicketHistories.AsNoTracking()
            .Where(h => visible.Any(t => t.Id == h.TicketId))
            .OrderByDescending(h => h.CreatedAt).Take(8)
            .Select(h => new ActivityDto(h.TicketId, h.Ticket.Title, h.Action, h.OldValue, h.NewValue, h.Actor == null ? null : h.Actor.FullName, h.CreatedAt))
            .ToListAsync(ct);

        var counters = new HomeCountersDto(
            await db.Tickets.CountAsync(t => t.RequesterId == userId && (t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected), ct),
            await db.Tickets.CountAsync(t => t.RequesterId == userId && t.Status == TicketStatus.Resolved, ct),
            canManage ? await db.Tickets.CountAsync(t => t.AssigneeId == userId && (t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected) && t.Status != TicketStatus.Resolved, ct) : 0,
            await db.Assets.CountAsync(a => a.AssignedUserId == userId, ct),
            await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct));

        return new HomeDto(
            new WelcomeDto(me.Id, me.FullName, me.Department, me.StoreName, me.Roles, me.CreatedAt),
            counters,
            (await GetAnnouncementsAsync(false, ct)).Take(5).ToList(),
            (await GetOnCallAsync(ct)).Where(s => s.EndsOn >= Today).Take(4).ToList(),
            myTasks, awaiting, activity);
    }

    // ---------- Duyurular ----------

    /// <summary>Kullanıcının görebileceği duyurular: yayında + mağaza hedefi + departman hedefi. Duyuru yöneticisi hepsini görür.</summary>
    private async Task<IQueryable<Announcement>> VisibleAnnouncementsAsync(bool includeInactive, CancellationToken ct)
    {
        var me = currentUser.RequireUserId();
        var canManage = currentUser.Has(Permissions.AnnouncementsManage);
        var now = Now;
        var q = db.Announcements.AsNoTracking();
        if (!(includeInactive && canManage))
            q = q.Where(a => a.PublishAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now));
        if (!canManage)
        {
            var myDepartment = await db.Users.Where(u => u.Id == me).Select(u => u.DepartmentId).FirstOrDefaultAsync(ct);
            q = q.Where(a => (a.StoreId == null || a.StoreId == currentUser.StoreId)
                             && (a.TargetDepartmentId == null || a.TargetDepartmentId == myDepartment));
        }
        return q;
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(bool includeInactive, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var canManage = currentUser.Has(Permissions.AnnouncementsManage);
        var q = await VisibleAnnouncementsAsync(includeInactive, ct);
        return await q.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.Level).ThenByDescending(a => a.PublishAt).Take(100)
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.Level, a.IsPinned, a.PublishAt, a.ExpiresAt,
                a.StoreId, a.Store == null ? null : a.Store.Name, a.Author.FullName,
                a.TargetDepartmentId, a.TargetDepartment == null ? null : a.TargetDepartment.Name, a.Reads.Any(r => r.UserId == me),
                a.AttachmentName, a.AttachmentContentType, a.AttachmentSize,
                canManage ? a.Reads.Count : null,
                canManage ? db.Users.Count(u => u.IsActive && (a.StoreId == null || u.StoreId == a.StoreId) && (a.TargetDepartmentId == null || u.DepartmentId == a.TargetDepartmentId)) : null))
            .ToListAsync(ct);
    }

    public async Task MarkAnnouncementReadAsync(int id, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        if (!await (await VisibleAnnouncementsAsync(false, ct)).AnyAsync(a => a.Id == id, ct)) throw NotFoundException.For("Duyuru", id);
        if (await db.AnnouncementReads.AnyAsync(r => r.AnnouncementId == id && r.UserId == me, ct)) return;
        db.AnnouncementReads.Add(new AnnouncementRead { AnnouncementId = id, UserId = me });
        await db.SaveChangesAsync(ct);
    }

    public async Task<AnnouncementReadersDto> AnnouncementReadersAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AnnouncementsManage);
        var a = await db.Announcements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Duyuru", id);
        var audience = await db.Users.CountAsync(u => u.IsActive && (a.StoreId == null || u.StoreId == a.StoreId) && (a.TargetDepartmentId == null || u.DepartmentId == a.TargetDepartmentId), ct);
        var readers = await db.AnnouncementReads.AsNoTracking().Where(r => r.AnnouncementId == id).OrderByDescending(r => r.ReadAt).Take(500)
            .Select(r => new AnnouncementReaderDto(r.UserId, r.User.FullName, r.User.Department == null ? null : r.User.Department.Name, r.ReadAt)).ToListAsync(ct);
        return new AnnouncementReadersDto(audience, readers);
    }

    public async Task<AnnouncementDto> SetAnnouncementAttachmentAsync(int id, string fileName, long length, Stream content, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AnnouncementsManage);
        if (storage is null) throw new InvalidOperationException("Dosya deposu yapılandırılmadı.");
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Duyuru", id);
        var name = Tickets.AttachmentRules.SafeFileName(fileName);
        var ext = Path.GetExtension(name);
        if (!Tickets.AttachmentRules.Types.TryGetValue(ext, out var contentType))
            throw new DomainException($"Bu dosya türü desteklenmiyor. İzin verilenler: {Tickets.AttachmentRules.AllowedList}");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new DomainException("Dosya boş görünüyor.");
        if (buffer.Length > Tickets.AttachmentRules.MaxBytes) throw new DomainException("Dosya 5 MB'tan büyük olamaz.");
        if (!Tickets.AttachmentRules.SignatureMatches(ext, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 4096))))
            throw new DomainException("Dosyanın içeriği uzantısıyla uyuşmuyor. Dosyayı orijinal biçiminde yükleyin.");
        var key = Guid.NewGuid().ToString("N");
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, ct);
        var old = a.AttachmentKey;
        (a.AttachmentKey, a.AttachmentName, a.AttachmentContentType, a.AttachmentSize) = (key, name, contentType, buffer.Length);
        a.UpdatedAt = Now;
        audit.Add("announcement.attachment_set", "announcement", id, new { name, size = buffer.Length });
        await db.SaveChangesAsync(ct);
        if (old is not null) await storage.DeleteAsync(old, ct);
        return (await GetAnnouncementsAsync(true, ct)).First(x => x.Id == id);
    }

    public async Task RemoveAnnouncementAttachmentAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AnnouncementsManage);
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Duyuru", id);
        var old = a.AttachmentKey;
        (a.AttachmentKey, a.AttachmentName, a.AttachmentContentType, a.AttachmentSize) = (null, null, null, null);
        audit.Add("announcement.attachment_removed", "announcement", id);
        await db.SaveChangesAsync(ct);
        if (old is not null && storage is not null) await storage.DeleteAsync(old, ct);
    }

    public async Task<AnnouncementFile> OpenAnnouncementAttachmentAsync(int id, CancellationToken ct = default)
    {
        var a = await (await VisibleAnnouncementsAsync(true, ct)).Where(x => x.Id == id)
            .Select(x => new { x.AttachmentKey, x.AttachmentName, x.AttachmentContentType }).FirstOrDefaultAsync(ct);
        if (a?.AttachmentKey is null || storage is null) throw NotFoundException.For("Duyuru eki", id);
        var stream = await storage.OpenReadAsync(a.AttachmentKey, ct) ?? throw NotFoundException.For("Duyuru eki", id);
        return new AnnouncementFile(stream, a.AttachmentContentType ?? "application/octet-stream", a.AttachmentName ?? "ek");
    }

    public async Task<AnnouncementDto> SaveAnnouncementAsync(int? id, UpsertAnnouncementRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AnnouncementsManage);
        var userId = currentUser.RequireUserId();
        if (r.StoreId is { } sid && !await db.Stores.AnyAsync(s => s.Id == sid, ct)) throw NotFoundException.For("Mağaza", sid);
        if (r.TargetDepartmentId is { } did && !await db.Departments.AnyAsync(d => d.Id == did && d.IsActive, ct)) throw NotFoundException.For("Departman", did);

        Announcement a;
        if (id is null)
        {
            a = new Announcement { AuthorId = userId };
            db.Announcements.Add(a);
        }
        else
        {
            a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Duyuru", id);
            a.UpdatedAt = Now;
        }
        a.Title = Text.Clean(r.Title);
        a.Body = Text.Clean(r.Body);
        a.Level = r.Level;
        a.IsPinned = r.IsPinned;
        a.PublishAt = r.PublishAt?.ToUniversalTime() ?? (id is null ? Now : a.PublishAt);
        a.ExpiresAt = r.ExpiresAt?.ToUniversalTime();
        a.StoreId = r.StoreId;
        a.TargetDepartmentId = r.TargetDepartmentId;
        // Hedeflenmiş duyuru kurumsal sitede gösterilmez
        if (a.StoreId is not null || a.TargetDepartmentId is not null) a.IsPublic = false;
        audit.Add(id is null ? "announcement.created" : "announcement.updated", "announcement", id, new { a.Title, a.StoreId });
        await db.SaveChangesAsync(ct);

        return (await GetAnnouncementsAsync(true, ct)).First(x => x.Id == a.Id);
    }

    public async Task DeleteAnnouncementAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.AnnouncementsManage);
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Duyuru", id);
        db.Announcements.Remove(a);
        audit.Add("announcement.deleted", "announcement", id, new { a.Title });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Nöbet çizelgesi ----------

    public async Task<IReadOnlyList<OnCallShiftDto>> GetOnCallAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var today = Today;
        var from = today.AddDays(-7);
        var to = today.AddDays(60);
        var rows = await db.OnCallShifts.AsNoTracking()
            .Where(s => s.EndsOn >= from && s.StartsOn <= to)
            .OrderBy(s => s.StartsOn).ThenBy(s => s.User.FullName)
            .Select(s => new { s.Id, s.UserId, s.User.FullName, s.User.Email, s.User.Phone, Department = s.User.Department == null ? null : s.User.Department.Name, s.StartsOn, s.EndsOn, s.Note })
            .ToListAsync(ct);
        return rows.Select(s => new OnCallShiftDto(s.Id, s.UserId, s.FullName, s.Email, s.Phone, s.Department, s.StartsOn, s.EndsOn, s.Note,
                today >= s.StartsOn && today <= s.EndsOn))
            .OrderByDescending(s => s.IsCurrent).ThenBy(s => s.StartsOn).ToList();
    }

    public async Task<OnCallShiftDto> CreateOnCallAsync(CreateOnCallShiftRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.OnCallManage);
        var person = await db.Users.AsNoTracking().Where(u => u.Id == r.UserId && u.IsActive)
                         .Select(u => new { u.Id, IsStaff = u.Roles.Any(x => x.Role.Permissions.Any(p => p.Permission.Key == Permissions.TicketsManage)) })
                         .FirstOrDefaultAsync(ct)
                     ?? throw NotFoundException.For("Kullanıcı", r.UserId);
        if (!person.IsStaff) throw new DomainException("Nöbete yalnızca destek ekibinden (talepleri yönetebilen) biri yazılabilir.");
        if (await db.OnCallShifts.AnyAsync(s => s.UserId == r.UserId && s.StartsOn <= r.EndsOn && s.EndsOn >= r.StartsOn, ct))
            throw new ConflictException("Bu kişinin seçilen tarihlerle çakışan bir nöbeti var. Tarihleri değiştirin veya eski nöbeti silin.");

        var shift = new OnCallShift { UserId = r.UserId, StartsOn = r.StartsOn, EndsOn = r.EndsOn, Note = Text.CleanOrNull(r.Note) };
        db.OnCallShifts.Add(shift);
        audit.Add("oncall.created", "on_call_shift", null, new { r.UserId, r.StartsOn, r.EndsOn });
        await db.SaveChangesAsync(ct);
        return (await GetOnCallAsync(ct)).FirstOrDefault(s => s.Id == shift.Id)
               ?? new OnCallShiftDto(shift.Id, r.UserId, "", "", null, null, r.StartsOn, r.EndsOn, shift.Note, false);
    }

    public async Task DeleteOnCallAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.OnCallManage);
        var s = await db.OnCallShifts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Nöbet", id);
        db.OnCallShifts.Remove(s);
        audit.Add("oncall.deleted", "on_call_shift", id, new { s.UserId, s.StartsOn, s.EndsOn });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class UpsertAnnouncementRequestValidator : AbstractValidator<UpsertAnnouncementRequest>
{
    public UpsertAnnouncementRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MinimumLength(3).WithMessage("Başlık en az 3 karakter olmalıdır.")
            .MaximumLength(150).WithMessage("Başlık en fazla 150 karakter olabilir.").NoMarkup();
        RuleFor(x => x.Body).NotEmpty().WithMessage("Duyuru metni zorunludur.").MaximumLength(2000).WithMessage("Duyuru metni en fazla 2000 karakter olabilir.").NoMarkup();
        RuleFor(x => x.Level).IsInEnum();
        RuleFor(x => x.ExpiresAt).Must((r, exp) => exp is null || exp > (r.PublishAt ?? DateTime.UtcNow))
            .WithMessage("Bitiş tarihi yayın tarihinden sonra olmalıdır.");
    }
}

public sealed class CreateOnCallShiftRequestValidator : AbstractValidator<CreateOnCallShiftRequest>
{
    public CreateOnCallShiftRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("Nöbetçi kişiyi seçin.");
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn).WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
        RuleFor(x => x).Must(x => x.EndsOn.DayNumber - x.StartsOn.DayNumber <= 31).WithName("EndsOn").WithMessage("Bir nöbet en fazla 31 gün sürebilir.");
        RuleFor(x => x.Note).MaximumLength(200).NoMarkup();
    }
}
