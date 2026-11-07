using Microsoft.Extensions.Logging.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Tickets;
using StajDesk.Contracts;
using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.UnitTests;

public class TicketServiceTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly FakeCurrentUser _current = new();
    private readonly FakeEventPublisher _events = new();
    private readonly NoCache _cache = new();
    private readonly TicketService _sut;
    private readonly User _user, _other, _agent;
    private readonly Category _category;

    public TicketServiceTests()
    {
        _user = new User { FullName = "Ayşe", Email = "ayse@test", Role = UserRole.User };
        _other = new User { FullName = "Mehmet", Email = "mehmet@test", Role = UserRole.User };
        _agent = new User { FullName = "Elif", Email = "elif@test", Role = UserRole.Agent };
        _category = new Category { Name = "Donanım" };
        _db.AddRange(_user, _other, _agent, _category);
        _db.SaveChanges();
        _sut = new TicketService(_db, _current, _events, _cache, NullLogger<TicketService>.Instance);
    }

    private async Task<TicketDetailDto> CreateAsUser(User u, string title = "Monitör görüntü vermiyor")
    {
        _current.As(u);
        return await _sut.CreateAsync(new CreateTicketRequest(title, "Sabahtan beri ekran siyah kalıyor.", _category.Id));
    }

    [Fact]
    public async Task Create_publishes_TicketCreated_event_and_writes_history()
    {
        var t = await CreateAsUser(_user);

        Assert.Equal(TicketStatus.Open, t.Status);
        Assert.Single(t.History);
        var (evt, key) = Assert.Single(_events.Published);
        Assert.Equal(Messaging.RoutingKeys.TicketCreated, key);
        Assert.Equal(t.Id, ((TicketCreatedEvent)evt).TicketId);
        Assert.True(_cache.Invalidations > 0);
    }

    [Fact]
    public async Task Create_with_unknown_category_throws_not_found()
    {
        _current.As(_user);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.CreateAsync(new CreateTicketRequest("Başlık burada", "Açıklama metni burada", 999)));
    }

    [Fact]
    public async Task User_only_sees_own_tickets_in_list()
    {
        await CreateAsUser(_user, "Benim talebim 1");
        await CreateAsUser(_other, "Başkasının talebi");

        _current.As(_user);
        var page = await _sut.GetListAsync(new TicketQuery());
        Assert.Single(page.Items);
        Assert.Equal("Benim talebim 1", page.Items[0].Title);

        _current.As(_agent);
        Assert.Equal(2, (await _sut.GetListAsync(new TicketQuery())).TotalCount);
    }

    [Fact]
    public async Task User_cannot_view_someone_elses_ticket()
    {
        var t = await CreateAsUser(_other);
        _current.As(_user);
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetByIdAsync(t.Id));
    }

    [Fact]
    public async Task User_cannot_change_status_of_open_ticket()
    {
        var t = await CreateAsUser(_user);
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ChangeStatusAsync(t.Id, TicketStatus.Resolved));
    }

    [Fact]
    public async Task Agent_can_resolve_and_requester_can_close()
    {
        var t = await CreateAsUser(_user);
        _current.As(_agent);
        var resolved = await _sut.ChangeStatusAsync(t.Id, TicketStatus.Resolved);
        Assert.Equal(TicketStatus.Resolved, resolved.Status);

        _current.As(_user);
        var detail = await _sut.GetByIdAsync(t.Id);
        Assert.Contains(TicketStatus.Closed, detail.AllowedStatuses);
        var closed = await _sut.ChangeStatusAsync(t.Id, TicketStatus.Closed);
        Assert.Equal(TicketStatus.Closed, closed.Status);
        Assert.Equal(2, _events.Published.Count(p => p.RoutingKey == Messaging.RoutingKeys.TicketStatusChanged));
    }

    [Fact]
    public async Task Assigning_open_ticket_moves_it_to_in_progress()
    {
        var t = await CreateAsUser(_user);
        _current.As(_agent);
        var assigned = await _sut.AssignAsync(t.Id, _agent.Id);

        Assert.Equal(TicketStatus.InProgress, assigned.Status);
        Assert.Equal(_agent.Id, assigned.Assignee?.Id);
    }

    [Fact]
    public async Task Cannot_assign_to_regular_user()
    {
        var t = await CreateAsUser(_user);
        _current.As(_agent);
        await Assert.ThrowsAsync<DomainException>(() => _sut.AssignAsync(t.Id, _other.Id));
    }

    [Fact]
    public async Task Internal_comments_are_hidden_from_requester()
    {
        var t = await CreateAsUser(_user);
        _current.As(_agent);
        await _sut.AddCommentAsync(t.Id, new AddCommentRequest("Kullanıcıya görünmeyen not", IsInternal: true));
        await _sut.AddCommentAsync(t.Id, new AddCommentRequest("Merhaba, inceliyoruz."));

        Assert.Equal(2, (await _sut.GetByIdAsync(t.Id)).Comments.Count);
        _current.As(_user);
        var visible = (await _sut.GetByIdAsync(t.Id)).Comments;
        Assert.Single(visible);
        Assert.False(visible[0].IsInternal);
    }

    [Fact]
    public async Task Regular_user_cannot_add_internal_comment()
    {
        var t = await CreateAsUser(_user);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.AddCommentAsync(t.Id, new AddCommentRequest("Gizli", IsInternal: true)));
    }

    [Fact]
    public async Task Filter_by_status_and_search_work_together()
    {
        await CreateAsUser(_user, "Yazıcı arızası");
        var second = await CreateAsUser(_user, "VPN bağlantısı yok");
        _current.As(_agent);
        await _sut.ChangeStatusAsync(second.Id, TicketStatus.InProgress);

        var result = await _sut.GetListAsync(new TicketQuery { Status = TicketStatus.InProgress, Search = "vpn" });
        Assert.Single(result.Items);
        Assert.Equal(second.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task Paging_returns_correct_slice()
    {
        for (var i = 1; i <= 12; i++) await CreateAsUser(_user, $"Talep numarası {i:00}");
        _current.As(_user);
        var page2 = await _sut.GetListAsync(new TicketQuery { Page = 2, PageSize = 5, Sort = "oldest" });
        Assert.Equal(12, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal("Talep numarası 06", page2.Items[0].Title);
    }
}
