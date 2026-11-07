using Microsoft.Extensions.Logging.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Tickets;
using StajDesk.Contracts;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.UnitTests;

/// <summary>Talep komut ve sorgularının (CQRS) iş kuralları ve rol/izin davranışları.</summary>
public class TicketServiceTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly FakeEventPublisher _events = new();
    private readonly NoCache _cache = new();
    private readonly User _user, _colleague, _otherStoreUser, _agent, _owner, _admin;
    private readonly Category _category;
    private readonly Store _storeA, _storeB;

    public TicketServiceTests()
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
        _colleague = U("mehmet", RoleNames.User, _storeA);
        _otherStoreUser = U("zeynep", RoleNames.User, _storeB);
        _agent = U("elif", RoleNames.Agent, null);
        _owner = U("selin", RoleNames.StoreOwner, _storeA);
        _admin = U("admin", RoleNames.SystemAdmin, null);
        _category = new Category { Name = "Donanım" };
        _db.AddRange(_user, _colleague, _otherStoreUser, _agent, _owner, _admin, _category);
        _db.SaveChanges();
    }

    private async Task<int> CreateAs(User u, string role, string title = "Monitör görüntü vermiyor")
    {
        _current.As(u, role);
        return await new CreateTicketHandler(_db, _current, _cache, _events, new DirectUnitOfWork(), new NoAudit(), NullLogger<CreateTicketHandler>.Instance)
            .HandleAsync(new CreateTicketCommand(new CreateTicketRequest(title, "Sabahtan beri ekran siyah kalıyor.", _category.Id)), default);
    }

    private Task<PagedResult<TicketListItemDto>> ListAs(User u, string role, TicketQuery? q = null)
    {
        _current.As(u, role);
        return new ListTicketsHandler(_db, _current).HandleAsync(new ListTicketsQuery(q ?? new TicketQuery()), default);
    }

    private Task<TicketDetailDto> GetAs(User u, string role, int id)
    {
        _current.As(u, role);
        return new GetTicketHandler(_db, _current).HandleAsync(new GetTicketQuery(id), default);
    }

    private Task ChangeStatusAs(User u, string role, int id, TicketStatus s)
    {
        _current.As(u, role);
        return new ChangeTicketStatusHandler(_db, _current, _cache, _events, NullLogger<ChangeTicketStatusHandler>.Instance)
            .HandleAsync(new ChangeTicketStatusCommand(id, s), default);
    }

    [Fact]
    public async Task Create_records_store_publishes_event_and_writes_history()
    {
        var id = await CreateAs(_user, RoleNames.User);
        var t = await GetAs(_user, RoleNames.User, id);

        Assert.Equal(TicketStatus.Open, t.Status);
        Assert.Equal(_storeA.Id, t.Store?.Id);
        Assert.Single(t.History);
        var (evt, key) = Assert.Single(_events.Published);
        Assert.Equal(Messaging.RoutingKeys.TicketCreated, key);
        Assert.Equal(_storeA.Id, ((TicketCreatedEvent)evt).StoreId);
        Assert.True(_cache.Invalidations > 0);
    }

    [Fact]
    public async Task Create_with_unknown_category_throws_not_found()
    {
        _current.As(_user, RoleNames.User);
        var handler = new CreateTicketHandler(_db, _current, _cache, _events, new DirectUnitOfWork(), new NoAudit(), NullLogger<CreateTicketHandler>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new CreateTicketCommand(new CreateTicketRequest("Başlık burada", "Açıklama metni burada", 999)), default));
    }

    [Fact]
    public async Task User_sees_only_own_tickets()
    {
        await CreateAs(_user, RoleNames.User, "Benim talebim");
        await CreateAs(_colleague, RoleNames.User, "Aynı mağazadan iş arkadaşı");
        await CreateAs(_otherStoreUser, RoleNames.User, "Başka mağaza");

        var mine = await ListAs(_user, RoleNames.User);
        Assert.Equal("Benim talebim", Assert.Single(mine.Items).Title);
    }

    [Fact]
    public async Task Store_owner_sees_all_tickets_of_own_store_only()
    {
        await CreateAs(_user, RoleNames.User, "A mağazası 1");
        await CreateAs(_colleague, RoleNames.User, "A mağazası 2");
        await CreateAs(_otherStoreUser, RoleNames.User, "B mağazası");

        var page = await ListAs(_owner, RoleNames.StoreOwner);
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, t => Assert.Equal(_storeA.Id, t.Store?.Id));
    }

    [Fact]
    public async Task Agent_and_admin_see_everything()
    {
        await CreateAs(_user, RoleNames.User);
        await CreateAs(_otherStoreUser, RoleNames.User);
        Assert.Equal(2, (await ListAs(_agent, RoleNames.Agent)).TotalCount);
        Assert.Equal(2, (await ListAs(_admin, RoleNames.SystemAdmin)).TotalCount);
    }

    [Fact]
    public async Task User_cannot_view_another_users_ticket()
    {
        var id = await CreateAs(_otherStoreUser, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => GetAs(_user, RoleNames.User, id));
    }

    [Fact]
    public async Task User_cannot_change_status_of_open_ticket()
    {
        var id = await CreateAs(_user, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() => ChangeStatusAs(_user, RoleNames.User, id, TicketStatus.Resolved));
    }

    [Fact]
    public async Task Agent_resolves_and_requester_closes()
    {
        var id = await CreateAs(_user, RoleNames.User);
        await ChangeStatusAs(_agent, RoleNames.Agent, id, TicketStatus.Resolved);

        var detail = await GetAs(_user, RoleNames.User, id);
        Assert.Contains(TicketStatus.Closed, detail.AllowedStatuses);
        await ChangeStatusAs(_user, RoleNames.User, id, TicketStatus.Closed);
        Assert.Equal(TicketStatus.Closed, (await GetAs(_user, RoleNames.User, id)).Status);
        Assert.Equal(2, _events.Published.Count(p => p.RoutingKey == Messaging.RoutingKeys.TicketStatusChanged));
    }

    [Fact]
    public async Task Agent_can_put_ticket_on_pending()
    {
        var id = await CreateAs(_user, RoleNames.User);
        await ChangeStatusAs(_agent, RoleNames.Agent, id, TicketStatus.Pending);
        Assert.Equal(TicketStatus.Pending, (await GetAs(_agent, RoleNames.Agent, id)).Status);
    }

    [Fact]
    public async Task Assigning_open_ticket_moves_it_to_in_progress()
    {
        var id = await CreateAs(_user, RoleNames.User);
        _current.As(_agent, RoleNames.Agent);
        await new AssignTicketHandler(_db, _current, _cache, _events).HandleAsync(new AssignTicketCommand(id, _agent.Id), default);

        var t = await GetAs(_agent, RoleNames.Agent, id);
        Assert.Equal(TicketStatus.InProgress, t.Status);
        Assert.Equal(_agent.Id, t.Assignee?.Id);
    }

    [Fact]
    public async Task Cannot_assign_to_a_user_without_manage_permission()
    {
        var id = await CreateAs(_user, RoleNames.User);
        _current.As(_agent, RoleNames.Agent);
        await Assert.ThrowsAsync<DomainException>(() =>
            new AssignTicketHandler(_db, _current, _cache, _events).HandleAsync(new AssignTicketCommand(id, _owner.Id), default));
    }

    [Fact]
    public async Task Internal_comments_are_hidden_from_requester_and_store_owner()
    {
        var id = await CreateAs(_user, RoleNames.User);
        _current.As(_agent, RoleNames.Agent);
        var handler = new AddCommentHandler(_db, _current, _cache, _events, new DirectUnitOfWork());
        await handler.HandleAsync(new AddCommentCommand(id, new AddCommentRequest("Kullanıcıya görünmeyen not", IsInternal: true)), default);
        await handler.HandleAsync(new AddCommentCommand(id, new AddCommentRequest("Merhaba, inceliyoruz.")), default);

        Assert.Equal(2, (await GetAs(_agent, RoleNames.Agent, id)).Comments.Count);
        Assert.Single((await GetAs(_user, RoleNames.User, id)).Comments);
        Assert.Single((await GetAs(_owner, RoleNames.StoreOwner, id)).Comments);
    }

    [Fact]
    public async Task Regular_user_cannot_add_internal_comment()
    {
        var id = await CreateAs(_user, RoleNames.User);
        _current.As(_user, RoleNames.User);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new AddCommentHandler(_db, _current, _cache, _events, new DirectUnitOfWork()).HandleAsync(new AddCommentCommand(id, new AddCommentRequest("Gizli", true)), default));
    }

    [Fact]
    public async Task Only_admin_can_delete()
    {
        var id = await CreateAs(_user, RoleNames.User);
        _current.As(_agent, RoleNames.Agent);
        var handler = new DeleteTicketHandler(_db, _current, _cache, new NoAudit(), NullLogger<DeleteTicketHandler>.Instance);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(new DeleteTicketCommand(id), default));
        _current.As(_admin, RoleNames.SystemAdmin);
        Assert.True(await handler.HandleAsync(new DeleteTicketCommand(id), default));
    }

    [Fact]
    public async Task Filter_and_paging_work_together()
    {
        for (var i = 1; i <= 12; i++) await CreateAs(_user, RoleNames.User, $"Talep numarası {i:00}");
        var page2 = await ListAs(_user, RoleNames.User, new TicketQuery { Page = 2, PageSize = 5, Sort = "oldest" });
        Assert.Equal(12, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal("Talep numarası 06", page2.Items[0].Title);

        var search = await ListAs(_user, RoleNames.User, new TicketQuery { Search = "numarası 11" });
        Assert.Single(search.Items);
    }
}
