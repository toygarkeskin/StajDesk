public class Ticket
{
    public int Id { get; set; }
    public string Title { get; private set; }
    public string Description { get; set; }
    public TicketStatus Status { get; private set; }
    public DateTime CreatedAt { get; set; }

    public Ticket(int id, string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Başlık boş olamaz.");
        }

        Id = id;
        Title = title;
        Description = description;
        Status = TicketStatus.Open;
        CreatedAt = DateTime.Now;
    }

    public void UpdateStatus(TicketStatus status)
    {
        Status = status;
    }
}