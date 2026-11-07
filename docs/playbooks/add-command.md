# Yeni CQRS komutu veya sorgusu

Talepler modülü `src/backend/StajDesk.Application/Features/Tickets` altında CQRS ile yazılmıştır.

## Komut (veriyi değiştirir)

```csharp
public sealed record ReopenTicket(int Id, string Reason) : ICommand<TicketDetailDto>;

public sealed class ReopenTicketValidator : AbstractValidator<ReopenTicket>
{
    public ReopenTicketValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

internal sealed class ReopenTicketHandler(IAppDbContext db, ICurrentUser user, IUnitOfWork uow, IEventPublisher events, IDispatcher dispatcher)
    : IRequestHandler<ReopenTicket, TicketDetailDto>
{
    public async Task<TicketDetailDto> Handle(ReopenTicket r, CancellationToken ct)
    {
        // 1) kaydı bul  2) TicketAccess.EnsureCanSee / CanEdit ile kapsamı ve izni kontrol et
        // 3) iş kuralını domain metodunda uygula (ticket.Reopen(...))
        // 4) olayı Enqueue et  5) uow.SaveChangesAsync  6) güncel detayı dön
    }
}
```

Kurallar:

- İş kuralı (hangi durumdan hangisine geçilir) **Domain**'de; handler yalnızca orkestrasyon yapar.
- Doğrulayıcı aynı dosyada; dispatcher otomatik çalıştırır, handler'da tekrar doğrulama yazma.
- Kapsam ve izin kontrolü handler'da (`TicketAccess`, `user.Require`). Controller'daki `[RequirePermission]` tek başına yeterli değildir.
- Önbellek: talep değiştiyse `stats:*` silinir.

## Sorgu (yalnızca okur)

- `IQuery<T>` + handler; `AsNoTracking()` ve `.Visible(user)` (`TicketAccess`) ile kullanıcının görebileceği kayıtlar.
- Projeksiyon (`Select`) ile yalnızca gereken alanlar; varlık döndürme.

## Controller

```csharp
[HttpPost("{id:int}/reopen"), RequirePermission(Permissions.TicketsUpdateOwn, Permissions.TicketsManage)]
public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Reopen(int id, ReopenRequest body, CancellationToken ct) =>
    Envelope(await dispatcher.Send(new ReopenTicket(id, body.Reason), ct));
```

Handler'lar `AddRequestHandlers()` ile otomatik kaydedilir.

## Test

`tests/StajDesk.UnitTests/TicketServiceTests.cs` örneklerini izle: `FakeCurrentUser.As(user, RoleNames.X)` ile
her rol için izinli ve izinsiz senaryo.
