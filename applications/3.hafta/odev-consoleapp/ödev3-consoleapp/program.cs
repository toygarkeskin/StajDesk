Console.WriteLine("===== GÜN 8 ÇALIŞMALARI =====");
Console.WriteLine("1 - Not Hesaplayıcı");
Console.WriteLine("2 - Switch");
Console.WriteLine("3 - For");
Console.WriteLine("4 - While");
Console.WriteLine("5 - Foreach");
Console.WriteLine("6 - Sayı Tahmin Oyunu");
Console.WriteLine("7 - Asal Sayılar");
Console.WriteLine("8 - FizzBuzz");
Console.WriteLine("9 - Çıkış");

Console.Write("Seçiminiz: ");

int secim = int.Parse(Console.ReadLine()!);

switch (secim)
{
    case 1:
        NotHesaplayici.Calistir();
        break;

    case 2:
        SwitchOrnekleri.Calistir();
        break;

    case 3:
        ForOrnekleri.Calistir();
        break;

    case 4:
        WhileOrnekleri.Calistir();
        break;

    case 5:
        ForeachOrnekleri.Calistir();
        break;

    case 6:
        SayiTahminOyunu.Calistir();
        break;

    case 7:
        AsalSayilar.Calistir();
        break;

    case 8:
        FizzBuzz.Calistir();
        break;

    case 9:
        Console.WriteLine("Program kapatıldı.");
        return;

    default:
        Console.WriteLine("Geçersiz seçim.");
        break;
    }



Ticket ticket = new Ticket(
    1,
    "Bilgisayar açılmıyor",
    "Kullanıcının bilgisayarı açılmıyor."
);

Console.WriteLine("\n===== TICKET =====");
Console.WriteLine("ID: " + ticket.Id);
Console.WriteLine("Başlık: " + ticket.Title);
Console.WriteLine("Açıklama: " + ticket.Description);
Console.WriteLine("Durum: " + ticket.Status);
Console.WriteLine("Oluşturulma: " + ticket.CreatedAt);



int sayi1 = 10;
int sayi2 = sayi1;

sayi2 = 20;

Console.WriteLine("\nsayi1: " + sayi1);
Console.WriteLine("sayi2: " + sayi2);



Ticket ticket1 = new Ticket(
    2,
    "İnternet çalışmıyor",
    "Ofiste internet bağlantısı yok."
);

Ticket ticket2 = ticket1;

Console.WriteLine("\nTicket 1: " + ticket1.Title);
Console.WriteLine("Ticket 2: " + ticket2.Title);



InMemoryTicketRepository repository = new InMemoryTicketRepository();

await repository.AddAsync(ticket1);
await repository.AddAsync(ticket2);

Console.WriteLine("\n===== REPOSITORY =====");

List<Ticket> tickets = await repository.GetAllAsync();

foreach (Ticket item in tickets)
{
    Console.WriteLine(
        $"{item.Id} - {item.Title} - {item.Status}"
    );
}


Console.WriteLine("\n===== TICKET ARAMA =====");

try
{
    Ticket bulunanTicket = await repository.GetByIdAsync(99);

    Console.WriteLine("Ticket bulundu: " + bulunanTicket.Title);
}
catch (Exception ex)
{
    Console.WriteLine("Hata: " + ex.Message);
}


Console.WriteLine();
Console.WriteLine("===== STAJDESK BAŞLIYOR =====");

InMemoryTicketRepository stajDeskRepository = new InMemoryTicketRepository();

StajDeskMenu menu = new StajDeskMenu(stajDeskRepository);

await menu.Calistir();