
using StajDesk.Api.Models;
using StajDesk.Api.Repositories;

namespace StajDesk.Api.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;

    public TicketService(ITicketRepository repository)
    {
        _repository = repository;
    }

    public List<Ticket> GetAll()
    {
        return _repository.GetAll();
    }

    public void Add(Ticket ticket)
    {
        _repository.Add(ticket);
    }

    public bool Update(int id, Ticket updatedTicket)
    {
        return _repository.Update(id, updatedTicket);
    }

    public bool Delete(int id)
    {
        return _repository.Delete(id);
    }
}
