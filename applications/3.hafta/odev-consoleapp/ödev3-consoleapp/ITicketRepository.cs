public interface ITicketRepository
{
    Task AddAsync(Ticket ticket);
    Task<List<Ticket>> GetAllAsync();
    Task<Ticket> GetByIdAsync(int id);
    Task DeleteAsync(int id);
}