using FluentValidation;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Users;
using StajDesk.Domain.Enums;

namespace StajDesk.Application.Features.Tickets;

public sealed record TicketQuery : PagedQuery
{
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public TicketType? Type { get; init; }
    public int? CategoryId { get; init; }
    public int? StoreId { get; init; }
    public int? AssigneeId { get; init; }
    public int? AssetId { get; init; }
    public bool? Unassigned { get; init; }
    public bool? ActiveOnly { get; init; }
    /// <summary>newest | oldest | priority | updated</summary>
    public string? Sort { get; init; }
}

public sealed record CategoryRefDto(int Id, string Name, string Color);
public sealed record AssetRefDto(int Id, string Name, string InventoryNumber, AssetType Type);

public sealed record TicketListItemDto(int Id, string Title, TicketStatus Status, TicketPriority Priority, TicketType Type,
    CategoryRefDto Category, StoreRefDto? Store, UserSummaryDto Requester, UserSummaryDto? Assignee, int CommentCount,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CommentAuthorDto(int Id, string FullName, bool IsStaff);
public sealed record TicketCommentDto(int Id, string Body, bool IsInternal, CommentAuthorDto Author, DateTime CreatedAt);
public sealed record TicketHistoryDto(int Id, TicketHistoryAction Action, string? OldValue, string? NewValue, UserSummaryDto? Actor, DateTime CreatedAt);

/// <summary>Arayüzün hangi butonları göstereceğini sunucu söyler (kural tek yerde).</summary>
public sealed record TicketPermissionsDto(bool CanEdit, bool CanManage, bool CanDelete, bool CanComment, bool CanInternal, bool CanAttach);

public sealed record TicketDetailDto(int Id, string Title, string Description, TicketStatus Status, TicketPriority Priority,
    TicketType Type, CategoryRefDto Category, StoreRefDto? Store, UserSummaryDto Requester, UserSummaryDto? Assignee,
    AssetRefDto? Asset, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? ResolvedAt, DateTime? ClosedAt,
    IReadOnlyList<TicketCommentDto> Comments, IReadOnlyList<TicketHistoryDto> History,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    IReadOnlyList<TicketStatus> AllowedStatuses, TicketPermissionsDto Permissions);

public sealed record CreateTicketRequest(string Title, string Description, int CategoryId,
    TicketPriority Priority = TicketPriority.Medium, TicketType Type = TicketType.Incident, int? AssetId = null);
public sealed record UpdateTicketRequest(string Title, string Description, int CategoryId, TicketType Type, int? AssetId);
public sealed record ChangeStatusRequest(TicketStatus Status);
public sealed record ChangePriorityRequest(TicketPriority Priority);
public sealed record AssignTicketRequest(int? AssigneeId);
public sealed record AddCommentRequest(string Body, bool IsInternal = false);

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.")
            .MinimumLength(5).WithMessage("Başlık en az 5 karakter olmalıdır.")
            .MaximumLength(200).WithMessage("Başlık en fazla 200 karakter olabilir.").NoMarkup();
        RuleFor(x => x.Description).NotEmpty().WithMessage("Açıklama zorunludur.")
            .MinimumLength(10).WithMessage("Açıklama en az 10 karakter olmalıdır.")
            .MaximumLength(5000).WithMessage("Açıklama en fazla 5000 karakter olabilir.").NoMarkup();
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Kategori seçin.");
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MinimumLength(5).MaximumLength(200).NoMarkup();
        RuleFor(x => x.Description).NotEmpty().WithMessage("Açıklama zorunludur.").MinimumLength(10).MaximumLength(5000).NoMarkup();
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Kategori seçin.");
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}

public sealed class ChangePriorityRequestValidator : AbstractValidator<ChangePriorityRequest>
{
    public ChangePriorityRequestValidator() => RuleFor(x => x.Priority).IsInEnum();
}

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().WithMessage("Yorum boş olamaz.")
            .MaximumLength(5000).WithMessage("Yorum en fazla 5000 karakter olabilir.").NoMarkup();
    }
}
