using System.Text;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Tasks;
using StajDesk.Application.Features.Workspace;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Aşama 6.2: görev atama kuralları ve görünürlük; duyuru hedef departmanı, okundu bilgisi, ek dosya.</summary>
public class TasksAnnouncementsTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly MemoryStorage _storage = new();
    private readonly User _director, _manager, _employee, _peer, _admin;
    private readonly Department _it, _sales;

    private sealed class FixedClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    public TasksAnnouncementsTests()
    {
        var roles = _db.SeedRoles();
        User U(string name, string role)
        {
            var u = new User { FullName = name, Email = $"{name}@test" };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }
        _it = new Department { Code = "BT", Name = "Bilgi Teknolojileri" };
        _sales = new Department { Code = "MG", Name = "Mağazacılık" };
        _director = U("Direktör", RoleNames.User);
        _manager = U("Müdür", RoleNames.User);
        _employee = U("Çalışan", RoleNames.User);
        _peer = U("Diğer", RoleNames.User);
        _admin = U("Admin", RoleNames.SystemAdmin);
        _db.AddRange(_it, _sales, _director, _manager, _employee, _peer, _admin);
        _db.SaveChanges();
        _manager.ManagerId = _director.Id;
        _employee.ManagerId = _manager.Id;
        _employee.DepartmentId = _it.Id;
        _peer.DepartmentId = _sales.Id;
        _db.SaveChanges();
    }

    private TaskService Tasks() => new(_db, _current, new NoAudit(), new FixedClock(Now));
    private WorkspaceService Workspace() => new(_db, _current, new NoAudit(), new FixedClock(Now), _storage);
    private static SaveTaskRequest T(string title, int? assignee, DateOnly? due = null) => new(title, null, assignee, due, WorkTaskPriority.Normal);

    // ---------- Görevler ----------

    [Fact]
    public async Task Managers_assign_down_their_line_but_not_to_others()
    {
        _current.As(_director, RoleNames.User);
        var t = await Tasks().SaveAsync(null, T("Envanter sayımı", _employee.Id)); // dolaylı rapor (iki kademe aşağı)
        Assert.Equal(_employee.Id, t.Assignee.Id);
        Assert.Contains(_db.Notifications, n => n.UserId == _employee.Id && n.Link == $"/tasks?task={t.Id}");

        await Assert.ThrowsAsync<ForbiddenException>(() => Tasks().SaveAsync(null, T("Yetkisiz", _peer.Id)));
        _current.As(_employee, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => Tasks().SaveAsync(null, T("Yukarı atama", _manager.Id)));
        var own = await Tasks().SaveAsync(null, T("Kendime not", null));
        Assert.Equal(_employee.Id, own.Assignee.Id);

        _current.As(_admin, RoleNames.SystemAdmin); // tasks.manage: herkese
        await Tasks().SaveAsync(null, T("Genel görev", _peer.Id));
    }

    [Fact]
    public async Task Only_creator_assignee_or_task_manager_sees_a_task()
    {
        _current.As(_manager, RoleNames.User);
        var t = await Tasks().SaveAsync(null, T("Rapor hazırla", _employee.Id, new DateOnly(2026, 11, 10)));
        Assert.True(t.IsOverdue);

        _current.As(_peer, RoleNames.User);
        await Assert.ThrowsAsync<NotFoundException>(() => Tasks().GetAsync(t.Id));
        Assert.Equal(0, (await Tasks().ListAsync(new TaskQuery { Scope = "all" })).TotalCount); // all yalnızca yöneticiye

        _current.As(_employee, RoleNames.User);
        Assert.Equal(1, (await Tasks().ListAsync(new TaskQuery())).TotalCount);
        Assert.Equal((1, 1, 0), ((await Tasks().SummaryAsync()).Open, (await Tasks().SummaryAsync()).Overdue, (await Tasks().SummaryAsync()).DueToday));
        // Atanan içeriği değiştiremez, durumu değiştirebilir; bitirince açan kişiye bildirim gider
        await Assert.ThrowsAsync<ForbiddenException>(() => Tasks().SaveAsync(t.Id, T("Başlık değişti", _employee.Id)));
        var done = await Tasks().ChangeStatusAsync(t.Id, WorkTaskStatus.Done);
        Assert.NotNull(done.CompletedAt);
        Assert.False(done.IsOverdue);
        Assert.Contains(_db.Notifications, n => n.UserId == _manager.Id && n.Title.Contains("tamamlandı"));
        await Assert.ThrowsAsync<ForbiddenException>(() => Tasks().DeleteAsync(t.Id));
    }

    // ---------- Duyurular ----------

    private async Task<int> Publish(string title, int? department = null)
    {
        _current.As(_admin, RoleNames.SystemAdmin);
        return (await Workspace().SaveAnnouncementAsync(null, new UpsertAnnouncementRequest(title, "Metin", PublishAt: Now.AddHours(-1), TargetDepartmentId: department))).Id;
    }

    [Fact]
    public async Task Department_targeted_announcement_is_only_visible_to_that_department()
    {
        var general = await Publish("Herkese");
        var itOnly = await Publish("Yalnızca BT", _it.Id);

        _current.As(_employee, RoleNames.User); // BT
        Assert.Equal(2, (await Workspace().GetAnnouncementsAsync(false)).Count);
        _current.As(_peer, RoleNames.User);     // Mağazacılık
        var peerSees = await Workspace().GetAnnouncementsAsync(false);
        Assert.Equal(general, Assert.Single(peerSees).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => Workspace().MarkAnnouncementReadAsync(itOnly));
        Assert.False(_db.Announcements.Single(a => a.Id == itOnly).IsPublic); // hedeflenen duyuru kurumsal sitede gösterilmez
    }

    [Fact]
    public async Task Read_receipts_are_counted_once_and_visible_to_managers()
    {
        var id = await Publish("Bakım", _it.Id);
        _current.As(_employee, RoleNames.User);
        await Workspace().MarkAnnouncementReadAsync(id);
        await Workspace().MarkAnnouncementReadAsync(id); // ikinci kez sayılmaz
        Assert.True((await Workspace().GetAnnouncementsAsync(false)).Single().IsRead);
        Assert.Null((await Workspace().GetAnnouncementsAsync(false)).Single().ReadCount); // çalışan sayıları görmez
        await Assert.ThrowsAsync<ForbiddenException>(() => Workspace().AnnouncementReadersAsync(id));

        _current.As(_admin, RoleNames.SystemAdmin);
        var readers = await Workspace().AnnouncementReadersAsync(id);
        Assert.Equal(1, readers.AudienceCount); // yalnızca BT'deki aktif çalışan
        Assert.Equal(_employee.Id, Assert.Single(readers.Readers).UserId);
    }

    [Fact]
    public async Task Attachments_are_validated_and_only_downloadable_by_the_audience()
    {
        var id = await Publish("Prosedür", _it.Id);
        _current.As(_admin, RoleNames.SystemAdmin);
        await Assert.ThrowsAsync<DomainException>(() => Workspace().SetAnnouncementAttachmentAsync(id, "virus.exe", 3, new MemoryStream([1, 2, 3])));
        await Assert.ThrowsAsync<DomainException>(() => Workspace().SetAnnouncementAttachmentAsync(id, "sahte.png", 4, new MemoryStream(Encoding.UTF8.GetBytes("text"))));
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n");
        var dto = await Workspace().SetAnnouncementAttachmentAsync(id, "prosedur.pdf", pdf.Length, new MemoryStream(pdf));
        Assert.Equal("prosedur.pdf", dto.AttachmentName);

        _current.As(_employee, RoleNames.User);
        Assert.Equal("application/pdf", (await Workspace().OpenAnnouncementAttachmentAsync(id)).ContentType);
        _current.As(_peer, RoleNames.User);
        await Assert.ThrowsAsync<NotFoundException>(() => Workspace().OpenAnnouncementAttachmentAsync(id));
    }
}
