public class StajDeskMenu
{
    private readonly InMemoryTicketRepository repository;

    public StajDeskMenu(InMemoryTicketRepository repository)
    {
        this.repository = repository;
    }

    public async Task Calistir()
    {
        bool devam = true;

        while (devam)
        {
            Console.WriteLine();
            Console.WriteLine("===== STAJDESK v0 =====");
            Console.WriteLine("1 - Talep Ekle");
            Console.WriteLine("2 - Talepleri Listele");
            Console.WriteLine("3 - Talep Durumunu Güncelle");
            Console.WriteLine("4 - Talep Bul");
            Console.WriteLine("5 - Talep Sil");
            Console.WriteLine("0 - Çıkış");
            Console.Write("Seçiminiz: ");

            string secim = Console.ReadLine()!;

            switch (secim)
            {
                case "1":
                    await TalepEkle();
                    break;

                case "2":
                    await TalepleriListele();
                    break;

                case "3":
                    await DurumGuncelle();
                    break;

                case "4":
                    await TalepBul();
                    break;

                case "5":
                    await TalepSil();
                    break;

                case "0":
                    devam = false;
                    Console.WriteLine("StajDesk kapatıldı.");
                    break;

                default:
                    Console.WriteLine("Geçersiz seçim.");
                    break;
            }
        }
    }

    private async Task TalepEkle()
    {
        Console.Write("Başlık: ");
        string title = Console.ReadLine()!;

        Console.Write("Açıklama: ");
        string description = Console.ReadLine()!;

        List<Ticket> tickets = await repository.GetAllAsync();

        int id = tickets.Count == 0 ? 1 : tickets.Max(x => x.Id) + 1;

        Ticket ticket = new Ticket(id, title, description);

        await repository.AddAsync(ticket);

        Console.WriteLine("Talep başarıyla eklendi.");
    }

    private async Task TalepleriListele()
    {
        List<Ticket> tickets = await repository.GetAllAsync();

        Console.WriteLine();
        Console.WriteLine("===== TALEPLER =====");

        if (tickets.Count == 0)
        {
            Console.WriteLine("Henüz talep yok.");
            return;
        }

        foreach (Ticket ticket in tickets)
        {
            Console.WriteLine(
                $"{ticket.Id} - {ticket.Title} - {ticket.Status}"
            );
        }
    }

    private async Task DurumGuncelle()
    {
        Console.Write("Ticket ID: ");
        int id = int.Parse(Console.ReadLine()!);

        try
        {
            Ticket ticket = await repository.GetByIdAsync(id);

            Console.WriteLine("1 - Open");
            Console.WriteLine("2 - InProgress");
            Console.WriteLine("3 - Resolved");
            Console.WriteLine("4 - Closed");
            Console.Write("Yeni durum: ");

            int secim = int.Parse(Console.ReadLine()!);

            TicketStatus yeniDurum;

            switch (secim)
            {
                case 1:
                    yeniDurum = TicketStatus.Open;
                    break;

                case 2:
                    yeniDurum = TicketStatus.InProgress;
                    break;

                case 3:
                    yeniDurum = TicketStatus.Resolved;
                    break;

                case 4:
                    yeniDurum = TicketStatus.Closed;
                    break;

                default:
                    Console.WriteLine("Geçersiz durum.");
                    return;
            }

            ticket.UpdateStatus(yeniDurum);

            Console.WriteLine("Talep durumu güncellendi.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Hata: " + ex.Message);
        }
    }

    private async Task TalepBul()
    {
        Console.Write("Ticket ID: ");
        int id = int.Parse(Console.ReadLine()!);

        try
        {
            Ticket ticket = await repository.GetByIdAsync(id);

            Console.WriteLine();
            Console.WriteLine("===== TICKET =====");
            Console.WriteLine("ID: " + ticket.Id);
            Console.WriteLine("Başlık: " + ticket.Title);
            Console.WriteLine("Açıklama: " + ticket.Description);
            Console.WriteLine("Durum: " + ticket.Status);
            Console.WriteLine("Oluşturulma: " + ticket.CreatedAt);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Hata: " + ex.Message);
        }
    }

    private async Task TalepSil()
    {
        Console.Write("Silinecek Ticket ID: ");
        int id = int.Parse(Console.ReadLine()!);

        try
        {
            await repository.DeleteAsync(id);

            Console.WriteLine("Talep başarıyla silindi.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Hata: " + ex.Message);
        }
    }
}