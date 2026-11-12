using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StajDesk.Api.Infrastructure;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Admin;
using StajDesk.Application.Features.Assets;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Categories;
using StajDesk.Application.Features.Notifications;
using StajDesk.Application.Features.Stats;
using StajDesk.Application.Features.Tickets;
using StajDesk.Application.Features.Users;
using StajDesk.Application.Features.Workspace;
using StajDesk.Application.Features.Employees;
using StajDesk.Application.Features.Account;
using StajDesk.Application.Features.Site;
using StajDesk.Application.Features.Portal;
using StajDesk.Application.Features.Workflows;
using StajDesk.Application.Features.Leave;
using StajDesk.Application.Features.Tasks;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Api.Controllers;

/// <summary>Controller'lar incedir: iş mantığı yok, yalnızca HTTP ↔ uygulama katmanı çevirisi.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Envelope<T>(T data, string? message = null) => Ok(ApiResponse<T>.Ok(data, message));
}

[Route("api/auth")]
public sealed class AuthController(IAuthService auth, IUserService users) : ApiControllerBase
{
    [AllowAnonymous, HttpPost("forgot-password"), EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.RequestPasswordResetAsync(request.Email, ct);
        return Envelope<object>(new { }, "Bu adrese kayıtlı bir hesap varsa şifre sıfırlama bağlantısı gönderildi. Gelen kutunuzu kontrol edin.");
    }

    [AllowAnonymous, HttpPost("reset-password"), EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return Envelope<object>(new { }, "Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.");
    }

    /// <summary>Oturumdaki kullanıcı + rolleri + izinleri (arayüz menüsü bu listeye göre çizilir).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<MeDto>>> Me(CancellationToken ct) => Envelope(await users.MeAsync(ct));
}

[Route("api/tickets")]
public sealed class TicketsController(IDispatcher dispatcher) : ApiControllerBase
{
    /// <summary>GET /api/tickets?status=Open&amp;categoryId=2&amp;page=1&amp;pageSize=10</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketListItemDto>>>> List([FromQuery] TicketQuery query, CancellationToken ct) =>
        Envelope(await dispatcher.SendAsync(new ListTicketsQuery(query), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Get(int id, CancellationToken ct) =>
        Envelope(await dispatcher.SendAsync(new GetTicketQuery(id), ct));

    [HttpPost, RequirePermission(Permissions.TicketsCreate)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var id = await dispatcher.SendAsync(new CreateTicketCommand(request), ct);
        var created = await dispatcher.SendAsync(new GetTicketQuery(id), ct);
        return CreatedAtAction(nameof(Get), new { id }, ApiResponse<TicketDetailDto>.Ok(created, "Talebiniz oluşturuldu."));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Update(int id, UpdateTicketRequest request, CancellationToken ct)
    {
        await dispatcher.SendAsync(new UpdateTicketCommand(id, request), ct);
        return Envelope(await dispatcher.SendAsync(new GetTicketQuery(id), ct), "Talep güncellendi.");
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken ct)
    {
        await dispatcher.SendAsync(new ChangeTicketStatusCommand(id, request.Status, request.Reason), ct);
        return Envelope(await dispatcher.SendAsync(new GetTicketQuery(id), ct), "Talep durumu güncellendi.");
    }

    [HttpPatch("{id:int}/priority"), RequirePermission(Permissions.TicketsManage)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> ChangePriority(int id, ChangePriorityRequest request, CancellationToken ct)
    {
        await dispatcher.SendAsync(new ChangeTicketPriorityCommand(id, request.Priority), ct);
        return Envelope(await dispatcher.SendAsync(new GetTicketQuery(id), ct), "Öncelik güncellendi.");
    }

    [HttpPatch("{id:int}/assign"), RequirePermission(Permissions.TicketsManage)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Assign(int id, AssignTicketRequest request, CancellationToken ct)
    {
        await dispatcher.SendAsync(new AssignTicketCommand(id, request.AssigneeId), ct);
        return Envelope(await dispatcher.SendAsync(new GetTicketQuery(id), ct), "Atama güncellendi.");
    }

    [HttpPost("{id:int}/comments"), RequirePermission(Permissions.CommentsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> AddComment(int id, AddCommentRequest request, CancellationToken ct)
    {
        var commentId = await dispatcher.SendAsync(new AddCommentCommand(id, request), ct);
        return StatusCode(201, ApiResponse<object>.Ok(new { id = commentId }, "Yorumunuz eklendi."));
    }

    [HttpDelete("{id:int}"), RequirePermission(Permissions.TicketsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await dispatcher.SendAsync(new DeleteTicketCommand(id), ct);
        return NoContent();
    }

    /// <summary>Dosya ekle (multipart/form-data, alan adı "file"). En fazla 5 MB; izin verilen türler sunucuda kontrol edilir.</summary>
    [HttpPost("{id:int}/attachments"), RequestSizeLimit(6 * 1024 * 1024), Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<TicketAttachmentDto>>> Upload(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null) throw new StajDesk.Domain.Common.DomainException("Yüklenecek dosyayı seçin.");
        await using var stream = file.OpenReadStream();
        var dto = await dispatcher.SendAsync(new UploadAttachmentCommand(id, file.FileName, file.Length, stream), ct);
        return StatusCode(201, ApiResponse<TicketAttachmentDto>.Ok(dto, "Dosya eklendi."));
    }

    /// <summary>Dosyayı indir. Her zaman "attachment" olarak gönderilir; tarayıcıda çalıştırılmaz.</summary>
    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> Download(int id, int attachmentId, CancellationToken ct)
    {
        var file = await dispatcher.SendAsync(new DownloadAttachmentQuery(id, attachmentId), ct);
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId, CancellationToken ct)
    {
        await dispatcher.SendAsync(new DeleteAttachmentCommand(id, attachmentId), ct);
        return NoContent();
    }
}

[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categories) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryDto>>>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Envelope(await categories.GetAllAsync(includeInactive, ct));

    [HttpPost, RequirePermission(Permissions.CategoriesManage)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create(UpsertCategoryRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<CategoryDto>.Ok(await categories.CreateAsync(request, ct), "Kategori oluşturuldu."));

    [HttpPut("{id:int}"), RequirePermission(Permissions.CategoriesManage)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(int id, UpsertCategoryRequest request, CancellationToken ct) =>
        Envelope(await categories.UpdateAsync(id, request, ct), "Kategori güncellendi.");

    [HttpDelete("{id:int}"), RequirePermission(Permissions.CategoriesManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await categories.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/assets")]
public sealed class AssetsController(IAssetService assets) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetListItemDto>>>> List([FromQuery] AssetQuery query, CancellationToken ct) =>
        Envelope(await assets.GetListAsync(query, ct));

    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AssetListItemDto>>>> Mine(CancellationToken ct) => Envelope(await assets.GetMineAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Get(int id, CancellationToken ct) => Envelope(await assets.GetByIdAsync(id, ct));

    [HttpPost, RequirePermission(Permissions.AssetsManage)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Create(UpsertAssetRequest request, CancellationToken ct)
    {
        var created = await assets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, ApiResponse<AssetDetailDto>.Ok(created, "Cihaz envantere eklendi."));
    }

    [HttpPut("{id:int}"), RequirePermission(Permissions.AssetsManage)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Update(int id, UpsertAssetRequest request, CancellationToken ct) =>
        Envelope(await assets.UpdateAsync(id, request, ct), "Cihaz güncellendi.");

    [HttpDelete("{id:int}"), RequirePermission(Permissions.AssetsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await assets.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/users")]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet, RequirePermission(Permissions.UsersRead, Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserListItemDto>>>> List([FromQuery] UserQuery query, CancellationToken ct) =>
        Envelope(await users.GetListAsync(query, ct));

    [HttpGet("agents"), RequirePermission(Permissions.TicketsManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserSummaryDto>>>> Agents(CancellationToken ct) => Envelope(await users.GetAgentsAsync(ct));

    [HttpGet("lookup"), RequirePermission(Permissions.AssetsManage, Permissions.UsersManage, Permissions.TicketsManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserSummaryDto>>>> Lookup([FromQuery] string? search, CancellationToken ct) =>
        Envelope(await users.LookupAsync(search, ct));

    [HttpPost, RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var id = await users.CreateAsync(request, ct);
        return StatusCode(201, ApiResponse<object>.Ok(new { id }, "Kullanıcı oluşturuldu."));
    }

    [HttpPut("{id:int}"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, UpdateUserRequest request, CancellationToken ct)
    {
        await users.UpdateAsync(id, request, ct);
        return Envelope<object>(new { id }, "Kullanıcı güncellendi.");
    }

    [HttpPost("{id:int}/lock"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> Lock(int id, LockUserRequest request, CancellationToken ct)
    {
        await users.LockAsync(id, request, ct);
        return Envelope<object>(new { id }, "Hesap kilitlendi; açık oturumları kapatıldı.");
    }

    [HttpPost("{id:int}/unlock"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> Unlock(int id, CancellationToken ct)
    {
        await users.UnlockAsync(id, ct);
        return Envelope<object>(new { id }, "Hesap kilidi açıldı.");
    }

    [HttpPost("{id:int}/invite"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> ResendInvite(int id, CancellationToken ct)
    {
        await users.ResendInvitationAsync(id, ct);
        return Envelope<object>(new { id }, "İlk şifre bağlantısı yeniden gönderildi.");
    }
}

[Route("api/stores")]
public sealed class StoresController(IAdminService admin) : ApiControllerBase
{
    [HttpGet, RequirePermission(Permissions.StoresRead, Permissions.StoresManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StoreDto>>>> List(CancellationToken ct) => Envelope(await admin.GetStoresAsync(ct));

    [HttpPost, RequirePermission(Permissions.StoresManage)]
    public async Task<ActionResult<ApiResponse<StoreDto>>> Create(UpsertStoreRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<StoreDto>.Ok(await admin.SaveStoreAsync(null, request, ct), "Mağaza eklendi."));

    [HttpPut("{id:int}"), RequirePermission(Permissions.StoresManage)]
    public async Task<ActionResult<ApiResponse<StoreDto>>> Update(int id, UpsertStoreRequest request, CancellationToken ct) =>
        Envelope(await admin.SaveStoreAsync(id, request, ct), "Mağaza güncellendi.");
}

[Route("api/roles")]
public sealed class RolesController(IAdminService admin) : ApiControllerBase
{
    [HttpGet, RequirePermission(Permissions.UsersManage, Permissions.RolesManage)]
    public async Task<ActionResult<ApiResponse<RolesOverviewDto>>> List(CancellationToken ct) => Envelope(await admin.GetRolesAsync(ct));

    [HttpPut("{id:int}/permissions"), RequirePermission(Permissions.RolesManage)]
    public async Task<ActionResult<ApiResponse<object>>> SetPermissions(int id, SetRolePermissionsRequest request, CancellationToken ct)
    {
        await admin.SetRolePermissionsAsync(id, request.Permissions, ct);
        return Envelope<object>(new { id }, "Rol izinleri güncellendi.");
    }
}

[Route("api/system")]
public sealed class SystemController(IAdminService admin) : ApiControllerBase
{
    [HttpGet("audit"), RequirePermission(Permissions.AuditRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> Audit([FromQuery] SimplePagedQuery query, CancellationToken ct) =>
        Envelope(await admin.GetAuditAsync(query, ct));

    [HttpGet("emails"), RequirePermission(Permissions.MailRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<SentEmailDto>>>> Emails([FromQuery] SimplePagedQuery query, CancellationToken ct) =>
        Envelope(await admin.GetSentEmailsAsync(query, ct));
}

[Route("api/stats")]
public sealed class StatsController(IStatsService stats) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> Dashboard(CancellationToken ct) => Envelope(await stats.GetDashboardAsync(ct));
}

[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> List([FromQuery] NotificationQuery query, CancellationToken ct) =>
        Envelope(await notifications.GetMineAsync(query, ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> UnreadCount(CancellationToken ct) => Envelope(await notifications.GetUnreadCountAsync(ct));

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllRead(CancellationToken ct) => Envelope(await notifications.MarkAllReadAsync(ct));
}

[Route("api")]
public sealed class WorkspaceController(IWorkspaceService workspace) : ApiControllerBase
{
    /// <summary>Ana sayfa paneli: karşılama, duyurular, nöbetçi personel, görevlerim, onay bekleyenler, son hareketler.</summary>
    [HttpGet("home")]
    public async Task<ActionResult<ApiResponse<HomeDto>>> Home(CancellationToken ct) => Envelope(await workspace.GetHomeAsync(ct));

    /// <summary>Yayındaki duyurular. Yönetici <c>?all=true</c> ile zamanlanmış ve süresi dolmuşları da görür.</summary>
    [HttpGet("announcements")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AnnouncementDto>>>> Announcements([FromQuery] bool all, CancellationToken ct) =>
        Envelope(await workspace.GetAnnouncementsAsync(all, ct));

    [HttpPost("announcements"), RequirePermission(Permissions.AnnouncementsManage)]
    public async Task<ActionResult<ApiResponse<AnnouncementDto>>> CreateAnnouncement(UpsertAnnouncementRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<AnnouncementDto>.Ok(await workspace.SaveAnnouncementAsync(null, request, ct), "Duyuru yayınlandı."));

    [HttpPut("announcements/{id:int}"), RequirePermission(Permissions.AnnouncementsManage)]
    public async Task<ActionResult<ApiResponse<AnnouncementDto>>> UpdateAnnouncement(int id, UpsertAnnouncementRequest request, CancellationToken ct) =>
        Envelope(await workspace.SaveAnnouncementAsync(id, request, ct), "Duyuru güncellendi.");

    [HttpDelete("announcements/{id:int}"), RequirePermission(Permissions.AnnouncementsManage)]
    public async Task<IActionResult> DeleteAnnouncement(int id, CancellationToken ct)
    {
        await workspace.DeleteAnnouncementAsync(id, ct);
        return NoContent();
    }

    /// <summary>Duyuruyu okudum olarak işaretle (yalnızca görebildiği duyuru).</summary>
    [HttpPost("announcements/{id:int}/read")]
    public async Task<IActionResult> ReadAnnouncement(int id, CancellationToken ct)
    {
        await workspace.MarkAnnouncementReadAsync(id, ct);
        return NoContent();
    }

    [HttpGet("announcements/{id:int}/readers"), RequirePermission(Permissions.AnnouncementsManage)]
    public async Task<ActionResult<ApiResponse<AnnouncementReadersDto>>> AnnouncementReaders(int id, CancellationToken ct) => Envelope(await workspace.AnnouncementReadersAsync(id, ct));

    [HttpPut("announcements/{id:int}/attachment"), RequirePermission(Permissions.AnnouncementsManage), RequestSizeLimit(6 * 1024 * 1024), Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<AnnouncementDto>>> SetAnnouncementAttachment(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null) throw new StajDesk.Domain.Common.DomainException("Eklenecek dosyayı seçin.");
        await using var stream = file.OpenReadStream();
        return Envelope(await workspace.SetAnnouncementAttachmentAsync(id, file.FileName, file.Length, stream, ct), "Ek dosya yüklendi.");
    }

    [HttpDelete("announcements/{id:int}/attachment"), RequirePermission(Permissions.AnnouncementsManage)]
    public async Task<IActionResult> RemoveAnnouncementAttachment(int id, CancellationToken ct)
    {
        await workspace.RemoveAnnouncementAttachmentAsync(id, ct);
        return NoContent();
    }

    /// <summary>Ek dosya: yalnızca duyuruyu görebilen indirir. Görseller satır içi, diğerleri "attachment" olarak.</summary>
    [HttpGet("announcements/{id:int}/attachment")]
    public async Task<IActionResult> AnnouncementAttachment(int id, CancellationToken ct)
    {
        var file = await workspace.OpenAnnouncementAttachmentAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return file.ContentType.StartsWith("image/") ? File(file.Content, file.ContentType) : File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Nöbet çizelgesi: geçen haftadan 60 gün sonrasına kadar.</summary>
    [HttpGet("oncall")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OnCallShiftDto>>>> OnCall(CancellationToken ct) => Envelope(await workspace.GetOnCallAsync(ct));

    [HttpPost("oncall"), RequirePermission(Permissions.OnCallManage)]
    public async Task<ActionResult<ApiResponse<OnCallShiftDto>>> CreateOnCall(CreateOnCallShiftRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<OnCallShiftDto>.Ok(await workspace.CreateOnCallAsync(request, ct), "Nöbet eklendi."));

    [HttpDelete("oncall/{id:int}"), RequirePermission(Permissions.OnCallManage)]
    public async Task<IActionResult> DeleteOnCall(int id, CancellationToken ct)
    {
        await workspace.DeleteOnCallAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Çalışan rehberi ve personel kartı. Okuma herkese açık (alan bazlı gizlilikle); değişiklik kişinin kendisi veya İK.</summary>
[Route("api/employees")]
public sealed class EmployeesController(IEmployeeService employees) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<EmployeeCardDto>>>> List([FromQuery] EmployeeQuery query, CancellationToken ct) =>
        Envelope(await employees.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> Get(int id, CancellationToken ct) => Envelope(await employees.GetAsync(id, ct));

    [HttpPut("{id:int}/personal")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> UpdatePersonal(int id, UpdatePersonalRequest request, CancellationToken ct)
    {
        await employees.UpdatePersonalAsync(id, request, ct);
        return Envelope(await employees.GetAsync(id, ct), "Kişisel bilgiler güncellendi.");
    }

    [HttpPut("{id:int}/work"), RequirePermission(Permissions.EmployeesManage)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> UpdateWork(int id, UpdateWorkRequest request, CancellationToken ct)
    {
        await employees.UpdateWorkAsync(id, request, ct);
        return Envelope(await employees.GetAsync(id, ct), "İş bilgileri güncellendi.");
    }

    /// <summary>Profil (photo) veya kapak (cover) fotoğrafı. Oturum gerektirir; tarayıcı yetkili istekle alıp gösterir.</summary>
    [HttpGet("{id:int}/{kind:regex(^(photo|cover)$)}")]
    public async Task<IActionResult> Image(int id, string kind, CancellationToken ct)
    {
        var file = await employees.GetImageAsync(id, kind, ct);
        if (file is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=86400"; // sürüm anahtarı URL'de, değişince yeni adres
        return File(file.Content, file.ContentType);
    }

    [HttpPut("{id:int}/{kind:regex(^(photo|cover)$)}"), RequestSizeLimit(3 * 1024 * 1024), Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> SetImage(int id, string kind, IFormFile? file, CancellationToken ct)
    {
        if (file is null) throw new StajDesk.Domain.Common.DomainException("Yüklenecek fotoğrafı seçin.");
        await using var stream = file.OpenReadStream();
        await employees.SetImageAsync(id, kind, file.FileName, stream, ct);
        return Envelope(await employees.GetAsync(id, ct), "Fotoğraf güncellendi.");
    }

    [HttpDelete("{id:int}/{kind:regex(^(photo|cover)$)}")]
    public async Task<IActionResult> RemoveImage(int id, string kind, CancellationToken ct)
    {
        await employees.RemoveImageAsync(id, kind, ct);
        return NoContent();
    }
}

/// <summary>Departmanlar, unvanlar ve organizasyon şeması.</summary>
[Route("api/org")]
public sealed class OrgController(IOrgService org) : ApiControllerBase
{
    [HttpGet("chart")]
    public async Task<ActionResult<ApiResponse<OrgChartDto>>> Chart(CancellationToken ct) => Envelope(await org.GetChartAsync(ct));

    [HttpGet("departments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DepartmentDto>>>> Departments(CancellationToken ct) => Envelope(await org.GetDepartmentsAsync(ct));

    [HttpPost("departments"), RequirePermission(Permissions.OrgManage)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> CreateDepartment(UpsertDepartmentRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<DepartmentDto>.Ok(await org.SaveDepartmentAsync(null, request, ct), "Departman eklendi."));

    [HttpPut("departments/{id:int}"), RequirePermission(Permissions.OrgManage)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> UpdateDepartment(int id, UpsertDepartmentRequest request, CancellationToken ct) =>
        Envelope(await org.SaveDepartmentAsync(id, request, ct), "Departman güncellendi.");

    [HttpDelete("departments/{id:int}"), RequirePermission(Permissions.OrgManage)]
    public async Task<IActionResult> DeleteDepartment(int id, CancellationToken ct)
    {
        await org.DeleteDepartmentAsync(id, ct);
        return NoContent();
    }

    [HttpGet("positions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PositionDto>>>> Positions(CancellationToken ct) => Envelope(await org.GetPositionsAsync(ct));

    [HttpPost("positions"), RequirePermission(Permissions.OrgManage)]
    public async Task<ActionResult<ApiResponse<PositionDto>>> CreatePosition(UpsertPositionRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<PositionDto>.Ok(await org.SavePositionAsync(null, request, ct), "Unvan eklendi."));

    [HttpPut("positions/{id:int}"), RequirePermission(Permissions.OrgManage)]
    public async Task<ActionResult<ApiResponse<PositionDto>>> UpdatePosition(int id, UpsertPositionRequest request, CancellationToken ct) =>
        Envelope(await org.SavePositionAsync(id, request, ct), "Unvan güncellendi.");

    [HttpDelete("positions/{id:int}"), RequirePermission(Permissions.OrgManage)]
    public async Task<IActionResult> DeletePosition(int id, CancellationToken ct)
    {
        await org.DeletePositionAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Oturumdaki kullanıcının hesap ayarları.</summary>
[Route("api/me")]
public sealed class AccountController(IAccountService account) : ApiControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<ApiResponse<SettingsDto>>> Settings(CancellationToken ct) => Envelope(await account.GetSettingsAsync(ct));

    [HttpPut("settings")]
    public async Task<ActionResult<ApiResponse<SettingsDto>>> SaveSettings(SettingsDto request, CancellationToken ct) =>
        Envelope(await account.SaveSettingsAsync(request, ct), "Tercihleriniz kaydedildi.");

    [HttpPut("password"), EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await account.ChangePasswordAsync(request, ct);
        return Envelope<object>(new { }, "Şifreniz değiştirildi.");
    }

    [HttpGet("security")]
    public async Task<ActionResult<ApiResponse<SecurityDto>>> Security(CancellationToken ct) => Envelope(await account.GetSecurityAsync(ct));

    [HttpPost("2fa/start"), EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<OtpStartedDto>>> StartTwoFactor(CancellationToken ct) =>
        Envelope(await account.StartTwoFactorAsync(ct), "Doğrulama kodu telefonunuza gönderildi.");

    [HttpPost("2fa/confirm"), EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmTwoFactor(ConfirmOtpRequest request, CancellationToken ct)
    {
        await account.ConfirmTwoFactorAsync(request, ct);
        return Envelope<object>(new { }, "İki adımlı doğrulama açıldı.");
    }

    [HttpPost("2fa/disable"), EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> DisableTwoFactor(ConfirmPasswordRequest request, CancellationToken ct)
    {
        await account.DisableTwoFactorAsync(request, ct);
        return Envelope<object>(new { }, "İki adımlı doğrulama kapatıldı.");
    }
}

/// <summary>Kurumsal hesap talepleri: İK açar (accounts.request), BT / yönetici onaylar veya reddeder (users.manage).</summary>
[Route("api/account-requests")]
public sealed class AccountRequestsController(IAccountRequestService requests, IAccountImportService import) : ApiControllerBase
{
    /// <summary>
    /// CSV ile toplu hesap talebi. dryRun=true önizlemedir (hiçbir şey kaydedilmez). Hesap AÇILMAZ; her geçerli satır
    /// BT/Admin onayına düşen bir "yeni hesap talebi" olur. Eksik departman/unvan yalnızca org.manage ile oluşturulur.
    /// </summary>
    [HttpPost("import"), RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage), RequestSizeLimit(6 * 1024 * 1024), Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<AccountImportResultDto>>> Import(IFormFile? file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new StajDesk.Domain.Common.DomainException("İçe aktarılacak CSV dosyasını seçin.");
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && !file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            throw new StajDesk.Domain.Common.DomainException("Yalnızca .csv dosyası yüklenebilir.");
        await using var stream = file.OpenReadStream();
        var result = await import.ImportAsync(stream, dryRun, ct);
        return Envelope(result, dryRun ? null : $"{result.CreatedCount} hesap talebi oluşturuldu ve onaya gönderildi.");
    }

    /// <summary>Örnek CSV şablonu (Excel'de Türkçe karakterler bozulmasın diye BOM'lu UTF-8, ayırıcı ";").</summary>
    [HttpGet("import/template"), RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage)]
    public IActionResult ImportTemplate()
    {
        const string csv = "Ad Soyad;E-posta;Personel Kodu;Departman;Unvan;Personel Türü;İşe Başlama;Telefon;Not\r\n" +
                           "Ayşe Yılmaz;ayse.yilmaz@stajdesk.local;P1001;Mağazacılık;Satış Danışmanı;Tam zamanlı;01.12.2026;05320000000;\r\n" +
                           "Mehmet Demir;mehmet.demir@stajdesk.local;P1002;Bilgi Teknolojileri ve Süreç Geliştirme;Uzman;Stajyer;15.12.2026;;Dönem stajı\r\n";
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "calisan-hesap-sablonu.csv");
    }

    [HttpGet, RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<PagedResult<AccountRequestDto>>>> List([FromQuery] AccountRequestQuery query, CancellationToken ct) =>
        Envelope(await requests.ListAsync(query, ct));

    [HttpGet("pending-count"), RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<int>>> PendingCount(CancellationToken ct) => Envelope(await requests.PendingCountAsync(ct));

    [HttpGet("{id:int}"), RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<AccountRequestDto>>> Get(int id, CancellationToken ct) => Envelope(await requests.GetAsync(id, ct));

    [HttpPost("new-account"), RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> NewAccount(CreateAccountRequest request, CancellationToken ct)
    {
        var id = await requests.CreateNewAccountAsync(request, ct);
        return StatusCode(201, ApiResponse<object>.Ok(new { id }, "Hesap talebi Bilgi Teknolojileri'ne iletildi."));
    }

    [HttpPost("deactivation"), RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivation(CreateDeactivationRequest request, CancellationToken ct)
    {
        var id = await requests.CreateDeactivationAsync(request, ct);
        return StatusCode(201, ApiResponse<object>.Ok(new { id }, "Hesap kapatma talebi Bilgi Teknolojileri'ne iletildi."));
    }

    [HttpPost("{id:int}/cancel"), RequirePermission(Permissions.AccountsRequest, Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(int id, CancellationToken ct)
    {
        await requests.CancelAsync(id, ct);
        return Envelope<object>(new { id }, "Talep geri çekildi.");
    }

    [HttpPost("{id:int}/approve"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<AccountRequestDto>>> Approve(int id, ApproveAccountRequest request, CancellationToken ct) =>
        Envelope(await requests.ApproveAsync(id, request, ct), "Talep onaylandı.");

    /// <summary>Seçilen bekleyen talepleri toplu onaylar (users.manage). Her talep tek onaydaki kurallarla işlenir; başarısızlar sebebiyle döner.</summary>
    [HttpGet("pending-refs"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PendingRequestRefDto>>>> PendingRefs([FromQuery] AccountRequestType? type, CancellationToken ct) =>
        Envelope(await requests.PendingRefsAsync(type, ct));

    [HttpPost("bulk-approve"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<BulkApproveResultDto>>> BulkApprove(BulkApproveAccountRequests request, CancellationToken ct)
    {
        var result = await requests.BulkApproveAsync(request, ct);
        return Envelope(result, $"{result.Approved.Count} talep onaylandı." + (result.Failed.Count > 0 ? $" {result.Failed.Count} talep onaylanamadı." : ""));
    }

    [HttpPost("{id:int}/reject"), RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<AccountRequestDto>>> Reject(int id, RejectAccountRequest request, CancellationToken ct) =>
        Envelope(await requests.RejectAsync(id, request, ct), "Talep reddedildi.");
}

/// <summary>
/// Herkese açık kurumsal site (giriş gerektirmez). Kişisel veri dönmez (KVKK kararı: docs/PORTAL_ANALIZ.md §8).
/// </summary>
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public sealed class PublicSiteController(IPublicSiteService site, ILiveDataService live) : ApiControllerBase
{
    [HttpGet("landing")]
    public async Task<ActionResult<ApiResponse<LandingDto>>> Landing(CancellationToken ct) => Envelope(await site.GetLandingAsync(ct));

    [HttpGet("site")]
    public async Task<ActionResult<ApiResponse<SiteInfoDto>>> Site(CancellationToken ct) => Envelope(await site.GetSiteInfoAsync(ct));

    [HttpGet("news")]
    public async Task<ActionResult<ApiResponse<PagedResult<NewsCardDto>>>> News([FromQuery] SimplePagedQuery query, CancellationToken ct) => Envelope(await site.GetNewsAsync(query, ct));

    [HttpGet("news/{slug}")]
    public async Task<ActionResult<ApiResponse<NewsDetailDto>>> NewsDetail(string slug, CancellationToken ct) => Envelope(await site.GetNewsAsync(slug, ct));

    [HttpGet("jobs")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<JobCardDto>>>> Jobs(CancellationToken ct) => Envelope(await site.GetJobsAsync(ct));

    [HttpGet("jobs/{id:int}")]
    public async Task<ActionResult<ApiResponse<JobDetailDto>>> Job(int id, CancellationToken ct) => Envelope(await site.GetJobAsync(id, ct));

    [HttpGet("departments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicDepartmentDto>>>> Departments(CancellationToken ct) => Envelope(await site.GetDepartmentsAsync(ct));

    [HttpGet("announcements")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicAnnouncementDto>>>> Announcements(CancellationToken ct) => Envelope(await site.GetAnnouncementsAsync(ct));

    /// <summary>Yayındaki reklamlar. Portal reklamları da buradan gelir (page=Portal); oturum gerekmez çünkü içerik herkese açık reklamdır.</summary>
    [HttpGet("ads")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdDto>>>> Ads([FromQuery] AdPage page, CancellationToken ct) => Envelope(await site.GetAdsAsync(page, ct));

    public sealed record ImpressionRequest(IReadOnlyList<int> Ids);

    [HttpPost("ads/impressions")]
    public async Task<IActionResult> Impressions(ImpressionRequest request, CancellationToken ct)
    {
        await site.RecordImpressionsAsync(request.Ids ?? [], ct);
        return NoContent();
    }

    /// <summary>Tıklamayı sayar ve reklamın kayıtlı adresine yönlendirir (adres yalnızca yöneticinin kaydettiği http/https olabilir).</summary>
    [HttpGet("ads/{id:int}/click")]
    public async Task<IActionResult> Click(int id, CancellationToken ct)
    {
        var url = await site.RecordClickAsync(id, ct);
        return url is not null && UrlRules.IsSafeHttpUrl(url) ? Redirect(url) : Redirect("/");
    }

    [HttpGet("media/{key:regex(^[[0-9a-f]]{{32}}$)}")]
    public async Task<IActionResult> Media(string key, CancellationToken ct)
    {
        var media = await site.GetMediaAsync(key, ct);
        if (media is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable"; // anahtar rastgele ve değişmez
        return File(media.Content, media.ContentType);
    }

    [HttpGet("cities")]
    public ActionResult<ApiResponse<IReadOnlyList<CityDto>>> Cities() => Envelope(live.Cities);

    [HttpGet("weather")]
    public async Task<ActionResult<ApiResponse<LiveValue<WeatherDto>>>> Weather([FromQuery] string? city, CancellationToken ct) => Envelope(await live.WeatherAsync(city, ct));

    [HttpGet("rates")]
    public async Task<ActionResult<ApiResponse<LiveValue<RatesDto>>>> Rates(CancellationToken ct) => Envelope(await live.RatesAsync(ct));

    [HttpGet("gold")]
    public async Task<ActionResult<ApiResponse<LiveValue<GoldDto>>>> Gold(CancellationToken ct) => Envelope(await live.GoldAsync(ct));
}

/// <summary>Kurumsal site içerik yönetimi (portal içinden): haber, açık pozisyon, site metinleri (content.manage), reklam (ads.manage).</summary>
[Route("api/content")]
public sealed class ContentController(IContentAdminService content) : ApiControllerBase
{
    [HttpGet("ads"), RequirePermission(Permissions.AdsManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdAdminDto>>>> Ads(CancellationToken ct) => Envelope(await content.GetAdsAsync(ct));

    [HttpGet("ads/{id:int}/stats"), RequirePermission(Permissions.AdsManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdDailyStatDto>>>> AdStats(int id, CancellationToken ct) => Envelope(await content.GetAdStatsAsync(id, ct));

    [HttpPost("ads"), RequirePermission(Permissions.AdsManage)]
    public async Task<ActionResult<ApiResponse<AdAdminDto>>> CreateAd(UpsertAdRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<AdAdminDto>.Ok(await content.SaveAdAsync(null, request, ct), "Reklam kaydedildi."));

    [HttpPut("ads/{id:int}"), RequirePermission(Permissions.AdsManage)]
    public async Task<ActionResult<ApiResponse<AdAdminDto>>> UpdateAd(int id, UpsertAdRequest request, CancellationToken ct) =>
        Envelope(await content.SaveAdAsync(id, request, ct), "Reklam güncellendi.");

    [HttpDelete("ads/{id:int}"), RequirePermission(Permissions.AdsManage)]
    public async Task<IActionResult> DeleteAd(int id, CancellationToken ct)
    {
        await content.DeleteAdAsync(id, ct);
        return NoContent();
    }

    [HttpGet("news"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NewsAdminDto>>>> News(CancellationToken ct) => Envelope(await content.GetNewsAsync(ct));

    [HttpPost("news"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<NewsAdminDto>>> CreateNews(UpsertNewsRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<NewsAdminDto>.Ok(await content.SaveNewsAsync(null, request, ct), "Haber kaydedildi."));

    [HttpPut("news/{id:int}"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<NewsAdminDto>>> UpdateNews(int id, UpsertNewsRequest request, CancellationToken ct) =>
        Envelope(await content.SaveNewsAsync(id, request, ct), "Haber güncellendi.");

    [HttpDelete("news/{id:int}"), RequirePermission(Permissions.ContentManage)]
    public async Task<IActionResult> DeleteNews(int id, CancellationToken ct)
    {
        await content.DeleteNewsAsync(id, ct);
        return NoContent();
    }

    [HttpGet("jobs"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<JobAdminDto>>>> Jobs(CancellationToken ct) => Envelope(await content.GetJobsAsync(ct));

    [HttpPost("jobs"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<JobAdminDto>>> CreateJob(UpsertJobRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<JobAdminDto>.Ok(await content.SaveJobAsync(null, request, ct), "İlan kaydedildi."));

    [HttpPut("jobs/{id:int}"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<JobAdminDto>>> UpdateJob(int id, UpsertJobRequest request, CancellationToken ct) =>
        Envelope(await content.SaveJobAsync(id, request, ct), "İlan güncellendi.");

    [HttpDelete("jobs/{id:int}"), RequirePermission(Permissions.ContentManage)]
    public async Task<IActionResult> DeleteJob(int id, CancellationToken ct)
    {
        await content.DeleteJobAsync(id, ct);
        return NoContent();
    }

    [HttpPut("settings"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<SiteInfoDto>>> SaveSettings(SaveSiteSettingsRequest request, CancellationToken ct) =>
        Envelope(await content.SaveSettingsAsync(request, ct), "Site metinleri kaydedildi.");

    [HttpPost("media"), RequirePermission(Permissions.ContentManage, Permissions.AdsManage), RequestSizeLimit(4 * 1024 * 1024), Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<MediaUploadedDto>>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null) throw new StajDesk.Domain.Common.DomainException("Yüklenecek görseli seçin.");
        await using var stream = file.OpenReadStream();
        return StatusCode(201, ApiResponse<MediaUploadedDto>.Ok(await content.UploadMediaAsync(file.FileName, stream, ct), "Görsel yüklendi."));
    }
}

/// <summary>Kişisel panel düzeni: görülebilir kutucuklar (izin + rol + yönetici ayarı) ve kullanıcının sırası / gizledikleri.</summary>
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ApiControllerBase
{
    [HttpGet("layout")]
    public async Task<ActionResult<ApiResponse<DashboardLayoutDto>>> Layout(CancellationToken ct) => Envelope(await dashboard.GetLayoutAsync(ct));

    [HttpPut("preferences")]
    public async Task<ActionResult<ApiResponse<DashboardLayoutDto>>> Save(SaveDashboardPreferencesRequest request, CancellationToken ct) =>
        Envelope(await dashboard.SavePreferencesAsync(request, ct), "Panel düzeniniz kaydedildi.");

    [HttpDelete("preferences")]
    public async Task<ActionResult<ApiResponse<DashboardLayoutDto>>> Reset(CancellationToken ct) =>
        Envelope(await dashboard.ResetPreferencesAsync(ct), "Panel varsayılan düzene döndü.");
}

/// <summary>Şirket genelinde kutucuk yönetimi (widgets.manage).</summary>
[Route("api/admin/widgets")]
public sealed class WidgetAdminController(IDashboardService dashboard) : ApiControllerBase
{
    [HttpGet, RequirePermission(Permissions.WidgetsManage)]
    public async Task<ActionResult<ApiResponse<WidgetAdminListDto>>> List(CancellationToken ct) => Envelope(await dashboard.GetWidgetsAsync(ct));

    [HttpPut("{key}"), RequirePermission(Permissions.WidgetsManage)]
    public async Task<ActionResult<ApiResponse<WidgetAdminDto>>> Save(string key, SaveWidgetRequest request, CancellationToken ct) =>
        Envelope(await dashboard.SaveWidgetAsync(key, request, ct), "Kutucuk ayarı kaydedildi.");
}

/// <summary>Giriş sonrası portal akışları: doğum günleri, aramıza katılanlar, etkinlikler, takdirler.</summary>
[Route("api/portal")]
public sealed class PortalController(IPortalService portal) : ApiControllerBase
{
    [HttpGet("birthdays")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BirthdayDto>>>> Birthdays(CancellationToken ct) => Envelope(await portal.BirthdaysAsync(ct));

    [HttpGet("new-hires")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NewHireDto>>>> NewHires(CancellationToken ct) => Envelope(await portal.NewHiresAsync(ct));

    [HttpGet("events")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CompanyEventDto>>>> Events(CancellationToken ct) => Envelope(await portal.UpcomingEventsAsync(ct));

    [HttpGet("recognitions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecognitionDto>>>> Recognitions(CancellationToken ct) => Envelope(await portal.RecentRecognitionsAsync(ct));
}

/// <summary>Genel arama: kişi, departman, duyuru, talep (her grup kullanıcının görebildiğiyle sınırlı).</summary>
[Route("api/search")]
public sealed class SearchController(IPortalService portal) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<SearchResultDto>>> Search([FromQuery] string? q, CancellationToken ct) => Envelope(await portal.SearchAsync(q ?? "", ct));
}

/// <summary>Portal içerik yönetimi: etkinlikler (content.manage), takdirler (content.manage veya İK).</summary>
[Route("api/content/portal")]
public sealed class PortalContentController(IPortalService portal) : ApiControllerBase
{
    [HttpGet("events"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CompanyEventDto>>>> Events(CancellationToken ct) => Envelope(await portal.AllEventsAsync(ct));

    [HttpPost("events"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<CompanyEventDto>>> CreateEvent(UpsertCompanyEventRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<CompanyEventDto>.Ok(await portal.SaveEventAsync(null, request, ct), "Etkinlik kaydedildi."));

    [HttpPut("events/{id:int}"), RequirePermission(Permissions.ContentManage)]
    public async Task<ActionResult<ApiResponse<CompanyEventDto>>> UpdateEvent(int id, UpsertCompanyEventRequest request, CancellationToken ct) =>
        Envelope(await portal.SaveEventAsync(id, request, ct), "Etkinlik güncellendi.");

    [HttpDelete("events/{id:int}"), RequirePermission(Permissions.ContentManage)]
    public async Task<IActionResult> DeleteEvent(int id, CancellationToken ct)
    {
        await portal.DeleteEventAsync(id, ct);
        return NoContent();
    }

    [HttpGet("recognitions"), RequirePermission(Permissions.ContentManage, Permissions.EmployeesManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecognitionDto>>>> Recognitions(CancellationToken ct) => Envelope(await portal.AllRecognitionsAsync(ct));

    [HttpPost("recognitions"), RequirePermission(Permissions.ContentManage, Permissions.EmployeesManage)]
    public async Task<ActionResult<ApiResponse<RecognitionDto>>> CreateRecognition(UpsertRecognitionRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<RecognitionDto>.Ok(await portal.SaveRecognitionAsync(null, request, ct), "Takdir kaydedildi."));

    [HttpPut("recognitions/{id:int}"), RequirePermission(Permissions.ContentManage, Permissions.EmployeesManage)]
    public async Task<ActionResult<ApiResponse<RecognitionDto>>> UpdateRecognition(int id, UpsertRecognitionRequest request, CancellationToken ct) =>
        Envelope(await portal.SaveRecognitionAsync(id, request, ct), "Takdir güncellendi.");

    [HttpDelete("recognitions/{id:int}"), RequirePermission(Permissions.ContentManage, Permissions.EmployeesManage)]
    public async Task<IActionResult> DeleteRecognition(int id, CancellationToken ct)
    {
        await portal.DeleteRecognitionAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Onay zinciri tanımları (workflows.manage).</summary>
[Route("api/workflows")]
public sealed class WorkflowsController(IWorkflowService workflows) : ApiControllerBase
{
    [HttpGet, RequirePermission(Permissions.WorkflowsManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowDefinitionDto>>>> List(CancellationToken ct) => Envelope(await workflows.DefinitionsAsync(ct));

    [HttpGet("roles"), RequirePermission(Permissions.WorkflowsManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleOptionDto>>>> Roles(CancellationToken ct) => Envelope(await workflows.RoleOptionsAsync(ct));

    [HttpPut("{id:int}"), RequirePermission(Permissions.WorkflowsManage)]
    public async Task<ActionResult<ApiResponse<WorkflowDefinitionDto>>> Save(int id, SaveWorkflowRequest request, CancellationToken ct) =>
        Envelope(await workflows.SaveDefinitionAsync(id, request, ct), "Onay zinciri kaydedildi. Yeni talepler bu zincirle ilerler.");
}

/// <summary>Onay kutusu: kararımı bekleyen adımlar. Karar yetkisi adım bazında serviste denetlenir.</summary>
[Route("api/approvals")]
public sealed class ApprovalsController(IWorkflowService workflows) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ApprovalInboxItemDto>>>> Inbox([FromQuery] SimplePagedQuery query, CancellationToken ct) => Envelope(await workflows.InboxAsync(query, ct));

    [HttpGet("count")]
    public async Task<ActionResult<ApiResponse<int>>> Count(CancellationToken ct) => Envelope(await workflows.InboxCountAsync(ct));

    [HttpGet("instances/{id:int}")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto>>> Instance(int id, CancellationToken ct) => Envelope(await workflows.GetAsync(id, ct));

    [HttpPost("{approvalId:int}/approve")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto>>> Approve(int approvalId, DecideRequest request, CancellationToken ct) =>
        Envelope(await workflows.DecideAsync(approvalId, true, request.Comment, ct), "Onaylandı.");

    [HttpPost("{approvalId:int}/reject")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto>>> Reject(int approvalId, DecideRequest request, CancellationToken ct) =>
        Envelope(await workflows.DecideAsync(approvalId, false, request.Comment, ct), "Reddedildi.");
}

/// <summary>İzin sistemi. Talep: leave.request; tür/hak yönetimi ve tüm talepler: leave.manage (İK rolünden ayrı).</summary>
[Route("api/leave")]
public sealed class LeaveController(ILeaveService leave, IWorkflowService workflows) : ApiControllerBase
{
    [HttpGet("types")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveTypeDto>>>> Types([FromQuery] bool includeInactive, CancellationToken ct) => Envelope(await leave.TypesAsync(includeInactive, ct));

    [HttpPost("types"), RequirePermission(Permissions.LeaveManage)]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> CreateType(SaveLeaveTypeRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<LeaveTypeDto>.Ok(await leave.SaveTypeAsync(null, request, ct), "İzin türü eklendi."));

    [HttpPut("types/{id:int}"), RequirePermission(Permissions.LeaveManage)]
    public async Task<ActionResult<ApiResponse<LeaveTypeDto>>> UpdateType(int id, SaveLeaveTypeRequest request, CancellationToken ct) =>
        Envelope(await leave.SaveTypeAsync(id, request, ct), "İzin türü güncellendi.");

    [HttpGet("balances")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaveBalanceDto>>>> Balances([FromQuery] int? userId, [FromQuery] int? year, CancellationToken ct) =>
        Envelope(await leave.BalancesAsync(userId, year, ct));

    [HttpPut("entitlements"), RequirePermission(Permissions.LeaveManage)]
    public async Task<ActionResult<ApiResponse<object>>> SetEntitlement(SetEntitlementRequest request, CancellationToken ct)
    {
        await leave.SetEntitlementAsync(request, ct);
        return Envelope<object>(new { request.UserId }, "İzin hakkı kaydedildi.");
    }

    [HttpGet("days")]
    public async Task<ActionResult<ApiResponse<LeaveDaysDto>>> Days([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken ct) => Envelope(await leave.CountDaysAsync(start, end, ct));

    [HttpGet("requests")]
    public async Task<ActionResult<ApiResponse<PagedResult<LeaveRequestDto>>>> Requests([FromQuery] LeaveRequestQuery query, CancellationToken ct) => Envelope(await leave.ListAsync(query, ct));

    [HttpPost("requests"), RequirePermission(Permissions.LeaveRequest)]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> Create(CreateLeaveRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<LeaveRequestDto>.Ok(await leave.CreateAsync(request, ct), "İzin talebiniz onaya gönderildi."));

    [HttpGet("requests/{id:int}")]
    public async Task<ActionResult<ApiResponse<LeaveRequestDto>>> Get(int id, CancellationToken ct) => Envelope(await leave.GetAsync(id, ct));

    /// <summary>Talebin onay geçmişi (talebi görebilen herkes).</summary>
    [HttpGet("requests/{id:int}/workflow")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto?>>> Workflow(int id, CancellationToken ct)
    {
        await leave.GetAsync(id, ct); // erişim denetimi
        return Envelope(await workflows.FindBySubjectAsync(LeaveSubjects.Request, id, ct));
    }

    [HttpPost("requests/{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(int id, CancellationToken ct)
    {
        await leave.CancelAsync(id, ct);
        return Envelope<object>(new { id }, "İzin talebi geri çekildi.");
    }
}

/// <summary>Görev yönetimi. Kendine herkes; başkasına yalnızca ekibine (organizasyon) veya tasks.manage ile.</summary>
[Route("api/tasks")]
public sealed class TasksController(ITaskService tasks) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkTaskDto>>>> List([FromQuery] TaskQuery query, CancellationToken ct) => Envelope(await tasks.ListAsync(query, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<TaskSummaryDto>>> Summary(CancellationToken ct) => Envelope(await tasks.SummaryAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<WorkTaskDto>>> Get(int id, CancellationToken ct) => Envelope(await tasks.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkTaskDto>>> Create(SaveTaskRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<WorkTaskDto>.Ok(await tasks.SaveAsync(null, request, ct), "Görev oluşturuldu."));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<WorkTaskDto>>> Update(int id, SaveTaskRequest request, CancellationToken ct) =>
        Envelope(await tasks.SaveAsync(id, request, ct), "Görev güncellendi.");

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<WorkTaskDto>>> Status(int id, ChangeTaskStatusRequest request, CancellationToken ct) =>
        Envelope(await tasks.ChangeStatusAsync(id, request.Status, ct), "Görev durumu güncellendi.");

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await tasks.DeleteAsync(id, ct);
        return NoContent();
    }
}
