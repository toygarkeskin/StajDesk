public class InMemoryTicketRepository : ITicketRepository
{
    private List<Ticket> tickets = new List<Ticket>();

    public async Task AddAsync(Ticket ticket)
    {
        tickets.Add(ticket);
        await Task.CompletedTask;
    }

    public async Task<List<Ticket>> GetAllAsync()
    {
        return await Task.FromResult(tickets);
    }

    public async Task<Ticket> GetByIdAsync(int id)
    {
        Ticket? ticket = tickets.FirstOrDefault(x => x.Id == id);

        if (ticket == null)
        {
            throw new Exception("Bu ID ile kayıtlı ticket bulunamadı.");
        }

        return await Task.FromResult(ticket);
    }

    public async Task DeleteAsync(int id)
    {
        Ticket ticket = await GetByIdAsync(id);
        tickets.Remove(ticket);
    }
}