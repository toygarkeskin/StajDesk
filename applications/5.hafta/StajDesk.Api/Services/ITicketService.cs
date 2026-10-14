
using StajDesk.Api.Models;

namespace StajDesk.Api.Services;

public interface ITicketService
{
    List<Ticket> GetAll();

    Ticket? GetById(int id);

    void Add(Ticket ticket);

    bool Update(int id, Ticket updatedTicket);

    bool Delete(int id);
}
