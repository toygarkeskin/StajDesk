using StajDesk.Domain.Common;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.UnitTests;

public class TicketDomainTests
{
    private static Ticket NewTicket() => new("Yazıcı çalışmıyor", "Kâğıt sıkışıyor ve ışık yanıp sönüyor.", 1, 1);

    [Fact]
    public void New_ticket_starts_as_open()
    {
        var t = NewTicket();
        Assert.Equal(TicketStatus.Open, t.Status);
        Assert.Null(t.ResolvedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_title_is_rejected(string title)
    {
        var ex = Assert.Throws<DomainException>(() => new Ticket(title, "Geçerli bir açıklama metni", 1, 1));
        Assert.Contains("başlığı", ex.Message);
    }

    [Fact]
    public void Resolving_sets_resolved_at_and_closing_sets_closed_at()
    {
        var t = NewTicket();
        t.ChangeStatus(TicketStatus.InProgress);
        t.ChangeStatus(TicketStatus.Resolved);
        Assert.NotNull(t.ResolvedAt);

        t.ChangeStatus(TicketStatus.Closed);
        Assert.NotNull(t.ClosedAt);
        Assert.NotNull(t.ResolvedAt);
    }

    [Fact]
    public void Changing_to_same_status_throws()
    {
        var t = NewTicket();
        Assert.Throws<DomainException>(() => t.ChangeStatus(TicketStatus.Open));
    }

    [Fact]
    public void Closed_ticket_can_only_be_reopened()
    {
        var t = NewTicket();
        t.ChangeStatus(TicketStatus.Closed);
        Assert.Throws<DomainException>(() => t.ChangeStatus(TicketStatus.Resolved));

        t.ChangeStatus(TicketStatus.Open);
        Assert.Null(t.ClosedAt);
        Assert.Null(t.ResolvedAt);
    }

    [Fact]
    public void Closed_ticket_cannot_be_edited_or_assigned()
    {
        var t = NewTicket();
        t.ChangeStatus(TicketStatus.Closed);
        Assert.Throws<DomainException>(() => t.UpdateDetails("Yeni başlık", "Yeni açıklama metni", 1, TicketType.Request, null));
        Assert.Throws<DomainException>(() => t.AssignTo(5));
    }
}
