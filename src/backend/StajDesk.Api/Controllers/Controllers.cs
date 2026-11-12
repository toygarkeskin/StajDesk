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
    [AllowAnonymous, HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> Register(RegisterRequest request, CancellationToken ct)
    {
        await auth.RegisterAsync(request, ct);
        return StatusCode(201, ApiResponse<object>.Ok(new { }, "Hesabınız oluşturuldu. Şimdi giriş yapabilirsiniz."));
    }

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
        await dispatcher.SendAsync(new ChangeTicketStatusCommand(id, request.Status), ct);
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
