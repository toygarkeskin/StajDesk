
using StajDesk.Api.Models;

namespace StajDesk.Api.Services;

public interface ITicketService
{
    List<Ticket> GetAll();

    void Add(Ticket ticket);

    bool Update(int id, Ticket updatedTicket);

    bool Delete(int id);
}
