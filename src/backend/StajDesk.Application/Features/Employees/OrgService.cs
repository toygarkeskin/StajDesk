using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Security;

namespace StajDesk.Application.Features.Employees;

/* =========================================================
   Organizasyon: departmanlar (ağaç), unvanlar ve organizasyon şeması.
   Okuma herkese açık (şirket içi rehber); değişiklik org.manage ister.
   ========================================================= */

public sealed record DepartmentDto(int Id, string Code, string Name, string? Description, bool IsActive,
    int? ParentId, string? ParentName, UserSummaryDto? Manager, int MemberCount);
public sealed record PositionDto(int Id, string Title, int Level, string? Description, int MemberCount);
public sealed record UpsertDepartmentRequest(string Code, string Name, string? Description, int? ParentId, int? ManagerId, bool IsActive = true);
public sealed record UpsertPositionRequest(string Title, int Level, string? Description);

public sealed record OrgPersonDto(int Id, string FullName, string? Title, int? Level, int? ManagerId, int? DepartmentId, bool HasPhoto, string? PhotoVersion);
public sealed record OrgChartDto(IReadOnlyList<DepartmentDto> Departments, IReadOnlyList<OrgPersonDto> People);

public interface IOrgService
{
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default);
    Task<DepartmentDto> SaveDepartmentAsync(int? id, UpsertDepartmentRequest request, CancellationToken ct = default);
    Task DeleteDepartmentAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken ct = default);
    Task<PositionDto> SavePositionAsync(int? id, UpsertPositionRequest request, CancellationToken ct = default);
    Task DeletePositionAsync(int id, CancellationToken ct = default);
    Task<OrgChartDto> GetChartAsync(CancellationToken ct = default);
}

internal sealed class OrgService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit) : IOrgService
{
    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        return await db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name, d.Description, d.IsActive, d.ParentId, d.Parent == null ? null : d.Parent.Name,
                d.Manager == null ? null : new UserSummaryDto(d.Manager.Id, d.Manager.FullName, d.Manager.Email),
                d.Members.Count(m => m.IsActive)))
            .ToListAsync(ct);
    }

    public async Task<DepartmentDto> SaveDepartmentAsync(int? id, UpsertDepartmentRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.OrgManage);
        var code = r.Code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(d => d.Code == code && d.Id != id, ct)) throw new ConflictException($"{code} kodlu bir departman zaten var.");
        if (r.ManagerId is { } m && !await db.Users.AnyAsync(u => u.Id == m && u.IsActive, ct)) throw NotFoundException.For("Yönetici", m);

        Department d;
        if (id is null) d = db.Departments.Add(new Department()).Entity;
        else d = await db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Departman", id);

        if (r.ParentId is { } parentId)
        {
            if (!await db.Departments.AnyAsync(x => x.Id == parentId, ct)) throw NotFoundException.For("Üst departman", parentId);
            if (id is { } self) await EnsureNoCycleAsync(self, parentId, ct);
        }
        if (id is { } existing && !r.IsActive && await db.Users.AnyAsync(u => u.DepartmentId == existing && u.IsActive, ct))
            throw new DomainException("Aktif çalışanı olan departman pasifleştirilemez. Önce çalışanları başka departmana taşıyın.");

        d.Code = code;
        d.Name = Text.Clean(r.Name);
        d.Description = Text.CleanOrNull(r.Description);
        d.ParentId = r.ParentId;
        d.ManagerId = r.ManagerId;
        d.IsActive = r.IsActive;
        d.UpdatedAt = id is null ? null : DateTime.UtcNow;
        audit.Add(id is null ? "department.created" : "department.updated", "department", id, new { code, r.ParentId, r.ManagerId });
        await db.SaveChangesAsync(ct);
        return (await GetDepartmentsAsync(ct)).First(x => x.Id == d.Id);
    }

    public async Task DeleteDepartmentAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.OrgManage);
        var d = await db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Departman", id);
        if (await db.Departments.AnyAsync(x => x.ParentId == id, ct)) throw new DomainException("Alt departmanı olan departman silinemez. Önce alt departmanları taşıyın.");
        if (await db.Users.AnyAsync(u => u.DepartmentId == id, ct)) throw new DomainException("Çalışanı olan departman silinemez. Pasifleştirin veya çalışanları taşıyın.");
        db.Departments.Remove(d);
        audit.Add("department.deleted", "department", id, new { d.Code });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        return await db.Positions.AsNoTracking().OrderBy(p => p.Level).ThenBy(p => p.Title)
            .Select(p => new PositionDto(p.Id, p.Title, p.Level, p.Description, db.Users.Count(u => u.PositionId == p.Id && u.IsActive)))
            .ToListAsync(ct);
    }

    public async Task<PositionDto> SavePositionAsync(int? id, UpsertPositionRequest r, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.OrgManage);
        var title = Text.Clean(r.Title);
        if (await db.Positions.AnyAsync(p => p.Title == title && p.Id != id, ct)) throw new ConflictException($"\"{title}\" unvanı zaten var.");
        Position p;
        if (id is null) p = db.Positions.Add(new Position()).Entity;
        else p = await db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Unvan", id);
        p.Title = title;
        p.Level = r.Level;
        p.Description = Text.CleanOrNull(r.Description);
        audit.Add(id is null ? "position.created" : "position.updated", "position", id, new { title, r.Level });
        await db.SaveChangesAsync(ct);
        return (await GetPositionsAsync(ct)).First(x => x.Id == p.Id);
    }

    public async Task DeletePositionAsync(int id, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.OrgManage);
        var p = await db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Unvan", id);
        if (await db.Users.AnyAsync(u => u.PositionId == id, ct)) throw new DomainException("Bu unvanı taşıyan çalışanlar var. Önce unvanlarını değiştirin.");
        db.Positions.Remove(p);
        audit.Add("position.deleted", "position", id, new { p.Title });
        await db.SaveChangesAsync(ct);
    }

    public async Task<OrgChartDto> GetChartAsync(CancellationToken ct = default)
    {
        var departments = (await GetDepartmentsAsync(ct)).Where(d => d.IsActive).ToList();
        var people = await db.Users.AsNoTracking().Where(u => u.IsActive)
            .OrderBy(u => u.Position == null ? 99 : u.Position.Level).ThenBy(u => u.FullName)
            .Select(u => new OrgPersonDto(u.Id, u.FullName, u.Position == null ? null : u.Position.Title, u.Position == null ? null : u.Position.Level,
                u.ManagerId, u.DepartmentId, u.Profile != null && u.Profile.PhotoKey != null, u.Profile == null ? null : u.Profile.PhotoKey))
            .ToListAsync(ct);
        return new OrgChartDto(departments, people);
    }

    private async Task EnsureNoCycleAsync(int departmentId, int parentId, CancellationToken ct)
    {
        if (departmentId == parentId) throw new DomainException("Bir departman kendisinin üst departmanı olamaz.");
        var parents = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.ParentId, ct);
        var seen = new HashSet<int>();
        for (int? cur = parentId; cur is { } c; cur = parents.GetValueOrDefault(c))
        {
            if (c == departmentId) throw new DomainException("Bu seçim departman ağacında döngü oluşturur.");
            if (!seen.Add(c)) break;
        }
    }
}

public sealed class UpsertDepartmentRequestValidator : AbstractValidator<UpsertDepartmentRequest>
{
    public UpsertDepartmentRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Departman kodu zorunludur.").Matches("^[A-Za-z0-9-]{2,20}$").WithMessage("Kod 2–20 karakter; harf, rakam ve tire.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Departman adı zorunludur.").MaximumLength(120).NoMarkup();
        RuleFor(x => x.Description).MaximumLength(500).NoMarkup();
    }
}

public sealed class UpsertPositionRequestValidator : AbstractValidator<UpsertPositionRequest>
{
    public UpsertPositionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Unvan zorunludur.").MaximumLength(100).NoMarkup();
        RuleFor(x => x.Level).InclusiveBetween(1, 20).WithMessage("Seviye 1 (en üst) ile 20 arasında olmalıdır.");
        RuleFor(x => x.Description).MaximumLength(300).NoMarkup();
    }
}
