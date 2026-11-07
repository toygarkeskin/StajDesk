using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;

namespace StajDesk.Infrastructure.Persistence;

/// <summary>
/// Uygulama açılışında migration'ları uygular ve başlangıç verisini ekler.
/// Yönetici bilgileri ve demo şifresi yapılandırmadan (.env / user-secrets) okunur; koda yazılmaz.
/// </summary>
public sealed class DbSeeder(AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DbSeeder> logger)
{
    public async Task MigrateAndSeedAsync(CancellationToken ct = default)
    {
        // PostgreSQL konteyneri henüz hazır değilse birkaç kez tekrar dene.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning("Veritabanına bağlanılamadı (deneme {Attempt}/10): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        await SeedCategoriesAsync(ct);
        await SeedAdminAsync(ct);
        if (config.GetValue("Seed:DemoData", false))
            await SeedDemoAsync(ct);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;
        db.Categories.AddRange(
            new Category { Name = "Donanım", Description = "Bilgisayar, monitör, klavye vb. arızalar", Color = "violet" },
            new Category { Name = "Yazılım", Description = "Uygulama kurulumu, lisans ve hata bildirimleri", Color = "sky" },
            new Category { Name = "Ağ ve İnternet", Description = "Bağlantı, VPN ve Wi-Fi sorunları", Color = "emerald" },
            new Category { Name = "E-posta", Description = "Outlook, posta kutusu ve takvim", Color = "amber" },
            new Category { Name = "Hesap ve Erişim", Description = "Şifre sıfırlama, yetki talepleri", Color = "rose" },
            new Category { Name = "Yazıcı", Description = "Yazıcı ve tarayıcı sorunları", Color = "indigo" });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Varsayılan kategoriler eklendi.");
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:AdminEmail / Seed:AdminPassword tanımlı değil; yönetici hesabı oluşturulmadı.");
            return;
        }
        email = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;

        db.Users.Add(new User { FullName = "Sistem Yöneticisi", Email = email, Role = UserRole.Admin, Department = "Bilgi İşlem", PasswordHash = hasher.Hash(password) });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Yönetici hesabı oluşturuldu: {Email}", email);
    }

    private async Task SeedDemoAsync(CancellationToken ct)
    {
        if (await db.Tickets.AnyAsync(ct)) return;
        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DemoPassword tanımlı değil; demo verisi eklenmedi.");
            return;
        }
        var hash = hasher.Hash(password);

        User U(string name, string email, UserRole role, string dept) =>
            new() { FullName = name, Email = email, Role = role, Department = dept, PasswordHash = hash };

        var agent1 = U("Elif Yılmaz", "elif.destek@stajdesk.local", UserRole.Agent, "Bilgi İşlem");
        var agent2 = U("Burak Demir", "burak.destek@stajdesk.local", UserRole.Agent, "Bilgi İşlem");
        var user1 = U("Ayşe Kaya", "ayse@stajdesk.local", UserRole.User, "Muhasebe");
        var user2 = U("Mehmet Çelik", "mehmet@stajdesk.local", UserRole.User, "Satış");
        var user3 = U("Zeynep Arslan", "zeynep@stajdesk.local", UserRole.User, "İnsan Kaynakları");
        db.Users.AddRange(agent1, agent2, user1, user2, user3);
        await db.SaveChangesAsync(ct);

        var assets = new[]
        {
            new Asset { Name = "Dell Latitude 5440", Type = AssetType.Laptop, Status = AssetStatus.InUse, InventoryNumber = "INV-0001", SerialNumber = "DL5440-7781", Manufacturer = "Dell", Model = "Latitude 5440", Location = "Merkez Ofis / 2. Kat", AssignedUserId = user1.Id, PurchaseDate = new DateOnly(2024, 3, 12), WarrantyEndDate = new DateOnly(2027, 3, 12) },
            new Asset { Name = "HP EliteDesk 800", Type = AssetType.Computer, Status = AssetStatus.InUse, InventoryNumber = "INV-0002", SerialNumber = "HPED-55120", Manufacturer = "HP", Model = "EliteDesk 800 G9", Location = "Merkez Ofis / 1. Kat", AssignedUserId = user2.Id, PurchaseDate = new DateOnly(2023, 9, 1), WarrantyEndDate = new DateOnly(2026, 9, 1) },
            new Asset { Name = "Lenovo ThinkPad T14", Type = AssetType.Laptop, Status = AssetStatus.InUse, InventoryNumber = "INV-0003", SerialNumber = "LNV-T14-3390", Manufacturer = "Lenovo", Model = "ThinkPad T14 Gen 4", Location = "Merkez Ofis / 3. Kat", AssignedUserId = user3.Id, PurchaseDate = new DateOnly(2025, 1, 20), WarrantyEndDate = new DateOnly(2028, 1, 20) },
            new Asset { Name = "Dell P2423 Monitör", Type = AssetType.Monitor, Status = AssetStatus.InUse, InventoryNumber = "INV-0004", Manufacturer = "Dell", Model = "P2423", Location = "Merkez Ofis / 2. Kat", AssignedUserId = user1.Id },
            new Asset { Name = "HP LaserJet Pro (2. Kat)", Type = AssetType.Printer, Status = AssetStatus.InUse, InventoryNumber = "INV-0005", SerialNumber = "HPLJ-9921", Manufacturer = "HP", Model = "LaserJet Pro M404", Location = "Merkez Ofis / 2. Kat" },
            new Asset { Name = "Cisco Catalyst Switch", Type = AssetType.NetworkDevice, Status = AssetStatus.InUse, InventoryNumber = "INV-0006", Manufacturer = "Cisco", Model = "Catalyst 9200", Location = "Sistem Odası" },
            new Asset { Name = "iPhone 15", Type = AssetType.Phone, Status = AssetStatus.InStock, InventoryNumber = "INV-0007", Manufacturer = "Apple", Model = "iPhone 15", Location = "Depo" },
            new Asset { Name = "Lenovo ThinkCentre (eski)", Type = AssetType.Computer, Status = AssetStatus.InRepair, InventoryNumber = "INV-0008", Manufacturer = "Lenovo", Model = "ThinkCentre M70", Location = "Bilgi İşlem" }
        };
        db.Assets.AddRange(assets);
        await db.SaveChangesAsync(ct);

        var cats = await db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);
        var rnd = new Random(42);
        var samples = new (string Title, string Desc, string Cat, TicketPriority P, User Req, Asset? A)[]
        {
            ("Dizüstü bilgisayar açılmıyor", "Sabah bilgisayarı açmaya çalıştığımda güç ışığı yanıyor ama ekran gelmiyor.", "Donanım", TicketPriority.High, user1, assets[0]),
            ("VPN bağlantısı sürekli kopuyor", "Evden çalışırken VPN 10-15 dakikada bir kopuyor, tekrar bağlanmam gerekiyor.", "Ağ ve İnternet", TicketPriority.Medium, user2, null),
            ("Outlook şifre soruyor", "Outlook her açılışta şifre soruyor ve kabul etmiyor.", "E-posta", TicketPriority.Medium, user3, null),
            ("Yazıcı kâğıt sıkıştırıyor", "2. kattaki yazıcı her çıktıda kâğıt sıkıştırıyor.", "Yazıcı", TicketPriority.Low, user1, assets[4]),
            ("ERP programına erişim talebi", "Muhasebe modülüne okuma yetkisi verilmesini rica ederim.", "Hesap ve Erişim", TicketPriority.Medium, user1, null),
            ("Excel sürekli donuyor", "Büyük dosyaları açarken Excel yanıt vermiyor.", "Yazılım", TicketPriority.Medium, user2, assets[1]),
            ("Wi-Fi toplantı odasında çekmiyor", "B toplantı odasında kablosuz ağ sinyali çok zayıf.", "Ağ ve İnternet", TicketPriority.High, user3, null),
            ("İkinci monitör talebi", "Rapor hazırlarken ikinci bir monitöre ihtiyacım var.", "Donanım", TicketPriority.Low, user2, null),
            ("Şifremi unuttum", "Windows oturum şifremi unuttum, sıfırlanmasını rica ederim.", "Hesap ve Erişim", TicketPriority.Critical, user3, null),
            ("Adobe Reader kurulumu", "PDF dosyalarını açabilmek için Adobe Reader kurulmasını istiyorum.", "Yazılım", TicketPriority.Low, user1, assets[0]),
            ("E-posta eki gönderilemiyor", "20 MB üzeri ekler gönderilemiyor, hata alıyorum.", "E-posta", TicketPriority.Medium, user2, null),
            ("Klavyenin bazı tuşları çalışmıyor", "Laptop klavyesinde E ve R tuşları basmıyor.", "Donanım", TicketPriority.Medium, user3, assets[2]),
            ("Sunucuya erişilemiyor", "Ortak klasöre (\\\\fileserver\\ortak) erişemiyorum.", "Ağ ve İnternet", TicketPriority.Critical, user1, null),
            ("Teams kamera görüntüsü gelmiyor", "Toplantılarda kameram siyah görünüyor.", "Yazılım", TicketPriority.Medium, user3, assets[2]),
            ("Toner değişimi", "Yazıcıda toner bitti uyarısı veriyor.", "Yazıcı", TicketPriority.Low, user2, assets[4]),
        };

        var statuses = new[] { TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed };
        var tickets = new List<Ticket>();
        for (var i = 0; i < samples.Length; i++)
        {
            var s = samples[i];
            var t = new Ticket(s.Title, s.Desc, s.Req.Id, cats[s.Cat], s.P, i % 4 == 3 ? TicketType.Request : TicketType.Incident, s.A?.Id);
            var created = DateTime.UtcNow.AddDays(-rnd.Next(0, 14)).AddHours(-rnd.Next(1, 8));
            t.CreatedAt = created;
            t.History.Add(new TicketHistory { ActorId = s.Req.Id, Action = TicketHistoryAction.Created, NewValue = "Open", CreatedAt = created });

            var target = statuses[i % statuses.Length];
            if (target != TicketStatus.Open)
            {
                var agent = i % 2 == 0 ? agent1 : agent2;
                t.AssignTo(agent.Id);
                t.ChangeStatus(TicketStatus.InProgress);
                t.History.Add(new TicketHistory { ActorId = agent.Id, Action = TicketHistoryAction.Assigned, NewValue = agent.FullName, CreatedAt = created.AddHours(1) });
                t.Comments.Add(new TicketComment { AuthorId = agent.Id, Body = "Talebinizi aldım, inceliyorum.", CreatedAt = created.AddHours(1) });
                if (target is TicketStatus.Resolved or TicketStatus.Closed)
                {
                    t.ChangeStatus(TicketStatus.Resolved);
                    t.Comments.Add(new TicketComment { AuthorId = agent.Id, Body = "Sorun giderildi. Tekrar yaşarsanız lütfen bildiriniz.", CreatedAt = created.AddHours(5) });
                    t.History.Add(new TicketHistory { ActorId = agent.Id, Action = TicketHistoryAction.StatusChanged, OldValue = "InProgress", NewValue = "Resolved", CreatedAt = created.AddHours(5) });
                }
                if (target == TicketStatus.Closed)
                {
                    t.ChangeStatus(TicketStatus.Closed);
                    t.History.Add(new TicketHistory { ActorId = s.Req.Id, Action = TicketHistoryAction.StatusChanged, OldValue = "Resolved", NewValue = "Closed", CreatedAt = created.AddHours(8) });
                }
            }
            tickets.Add(t);
        }
        db.Tickets.AddRange(tickets);

        // Demo zaman damgalarını gerçekçi hâle getir (domain metotları "şimdi"yi kullanır).
        foreach (var t in tickets)
        {
            var entry = db.Entry(t);
            if (t.ResolvedAt is not null) entry.Property(x => x.ResolvedAt).CurrentValue = t.CreatedAt.AddHours(5);
            if (t.ClosedAt is not null) entry.Property(x => x.ClosedAt).CurrentValue = t.CreatedAt.AddHours(8);
            entry.Property(x => x.UpdatedAt).CurrentValue = t.Status == TicketStatus.Open ? null : t.CreatedAt.AddHours(5);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo verisi eklendi: {Users} kullanıcı, {Assets} cihaz, {Tickets} talep", 5, assets.Length, tickets.Count);
    }
}
