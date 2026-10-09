
using StajDesk.Api.Models;
using System.Linq;

namespace StajDesk.Api.Repositories;

public class InMemoryTicketRepository : ITicketRepository
{
    private readonly List<Ticket> tickets = new()
    {
        new Ticket
        {
            Id = 1,
            Title = "Bilgisayar sorunu",
            Description = "Bilgisayar açılmıyor",
            IsCompleted = false
        },
        new Ticket
        {
            Id = 2,
            Title = "E-posta sorunu",
            Description = "E-postalar açılmıyor",
            IsCompleted = true
        }
    };

    public List<Ticket> GetAll()
    {
        return tickets;
    }

    public void Add(Ticket ticket)
    {
        ticket.Id = tickets.Count == 0
            ? 1
            : tickets.Max(t => t.Id) + 1;

        tickets.Add(ticket);
    }

    public bool Update(int id, Ticket updatedTicket)
    {
        var ticket = tickets.FirstOrDefault(t => t.Id == id);

        if (ticket == null)
        {
            return false;
        }

        ticket.Title = updatedTicket.Title;
        ticket.Description = updatedTicket.Description;
        ticket.IsCompleted = updatedTicket.IsCompleted;

        return true;
    }

    public bool Delete(int id)
    {
        var ticket = tickets.FirstOrDefault(t => t.Id == id);

        if (ticket == null)
        {
            return false;
        }

        tickets.Remove(ticket);

        return true;
    }
}
