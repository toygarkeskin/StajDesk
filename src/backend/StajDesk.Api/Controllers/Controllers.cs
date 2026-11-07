using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StajDesk.Api.Infrastructure;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Assets;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Categories;
using StajDesk.Application.Features.Notifications;
using StajDesk.Application.Features.Stats;
using StajDesk.Application.Features.Tickets;
using StajDesk.Application.Features.Users;

namespace StajDesk.Api.Controllers;

/// <summary>Controller'lar incedir: iş mantığı yok, yalnızca HTTP ↔ servis çevirisi.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Envelope<T>(T data, string? message = null) => Ok(ApiResponse<T>.Ok(data, message));
}

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [AllowAnonymous, HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponse>.Ok(result, "Kaydınız başarıyla oluşturuldu."));
    }

    [AllowAnonymous, HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken ct) =>
        Envelope(await auth.LoginAsync(request, ct));

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct) => Envelope(await auth.MeAsync(ct));
}

[Route("api/tickets")]
public sealed class TicketsController(ITicketService tickets) : ApiControllerBase
{
    /// <summary>GET /api/tickets?status=Open&amp;categoryId=2&amp;page=1&amp;pageSize=10</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketListItemDto>>>> List([FromQuery] TicketQuery query, CancellationToken ct) =>
        Envelope(await tickets.GetListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Get(int id, CancellationToken ct) =>
        Envelope(await tickets.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var created = await tickets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, ApiResponse<TicketDetailDto>.Ok(created, "Talebiniz oluşturuldu."));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Update(int id, UpdateTicketRequest request, CancellationToken ct) =>
        Envelope(await tickets.UpdateAsync(id, request, ct), "Talep güncellendi.");

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken ct) =>
        Envelope(await tickets.ChangeStatusAsync(id, request.Status, ct), "Talep durumu güncellendi.");

    [HttpPatch("{id:int}/priority"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> ChangePriority(int id, ChangePriorityRequest request, CancellationToken ct) =>
        Envelope(await tickets.ChangePriorityAsync(id, request.Priority, ct), "Öncelik güncellendi.");

    [HttpPatch("{id:int}/assign"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Assign(int id, AssignTicketRequest request, CancellationToken ct) =>
        Envelope(await tickets.AssignAsync(id, request.AssigneeId, ct), "Atama güncellendi.");

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<ApiResponse<TicketCommentDto>>> AddComment(int id, AddCommentRequest request, CancellationToken ct)
    {
        var comment = await tickets.AddCommentAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<TicketCommentDto>.Ok(comment, "Yorumunuz eklendi."));
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await tickets.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categories) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryDto>>>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Envelope(await categories.GetAllAsync(includeInactive && User.IsInRole(Roles.Admin), ct));

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create(UpsertCategoryRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<CategoryDto>.Ok(await categories.CreateAsync(request, ct), "Kategori oluşturuldu."));

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(int id, UpsertCategoryRequest request, CancellationToken ct) =>
        Envelope(await categories.UpdateAsync(id, request, ct), "Kategori güncellendi.");

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await categories.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/assets")]
public sealed class AssetsController(IAssetService assets) : ApiControllerBase
{
    [HttpGet, Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetListItemDto>>>> List([FromQuery] AssetQuery query, CancellationToken ct) =>
        Envelope(await assets.GetListAsync(query, ct));

    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AssetListItemDto>>>> Mine(CancellationToken ct) =>
        Envelope(await assets.GetMineAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Get(int id, CancellationToken ct) =>
        Envelope(await assets.GetByIdAsync(id, ct));

    [HttpPost, Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Create(UpsertAssetRequest request, CancellationToken ct)
    {
        var created = await assets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, ApiResponse<AssetDetailDto>.Ok(created, "Envanter kaydı oluşturuldu."));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Update(int id, UpsertAssetRequest request, CancellationToken ct) =>
        Envelope(await assets.UpdateAsync(id, request, ct), "Envanter kaydı güncellendi.");

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await assets.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/users")]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserListItemDto>>>> List([FromQuery] UserQuery query, CancellationToken ct) =>
        Envelope(await users.GetListAsync(query, ct));

    [HttpGet("agents"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserSummaryDto>>>> Agents(CancellationToken ct) =>
        Envelope(await users.GetAgentsAsync(ct));

    [HttpGet("lookup"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserSummaryDto>>>> Lookup([FromQuery] string? search, CancellationToken ct) =>
        Envelope(await users.LookupAsync(search, ct));

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(CreateUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<UserDto>.Ok(await users.CreateAsync(request, ct), "Kullanıcı oluşturuldu."));

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(int id, UpdateUserRequest request, CancellationToken ct) =>
        Envelope(await users.UpdateAsync(id, request, ct), "Kullanıcı güncellendi.");
}

[Route("api/stats")]
public sealed class StatsController(IStatsService stats) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> Dashboard(CancellationToken ct) =>
        Envelope(await stats.GetDashboardAsync(ct));
}

[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> List([FromQuery] NotificationQuery query, CancellationToken ct) =>
        Envelope(await notifications.GetMineAsync(query, ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> UnreadCount(CancellationToken ct) =>
        Envelope(await notifications.GetUnreadCountAsync(ct));

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllRead(CancellationToken ct) =>
        Envelope(await notifications.MarkAllReadAsync(ct));
}
