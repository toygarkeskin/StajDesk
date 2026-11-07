using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StajDesk.Application.Abstractions;
using StajDesk.Domain.Entities;
using StajDesk.Domain.Enums;
using StajDesk.Domain.Security;

namespace StajDesk.Infrastructure.Persistence;

/// <summary>
/// Açılışta: migration'ları uygular, izin/rol matrisini senkronlar, OAuth istemcilerini ve sistem yöneticisini oluşturur,
/// istenirse demo verisini (mağazalar, kullanıcılar, cihazlar, talepler) ekler.
/// Şifreler yapılandırmadan (.env / ortam değişkenleri) okunur; koda yazılmaz.
/// </summary>
public sealed class DbSeeder(AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DbSeeder> logger)
{
    public const string WebClientId = "stajdesk-web";
    public const string ReportingClientId = "stajdesk-reporting";

    public async Task MigrateAndSeedAsync(CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < 15)
            {
                logger.LogWarning("Veritabanına bağlanılamadı (deneme {Attempt}/15): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        await SyncPermissionsAndRolesAsync(ct);
        await SeedClientsAsync(ct);
        await SeedCategoriesAsync(ct);
        await SeedSystemAdminAsync(ct);
        if (config.GetValue("Seed:DemoData", false)) await SeedDemoAsync(ct);
    }

    /// <summary>Kodda tanımlı izinler veritabanına eklenir; Sistem Yöneticisi her zaman tüm izinlere sahip olur.</summary>
    private async Task SyncPermissionsAndRolesAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, ct);
        foreach (var def in Permissions.All)
        {
            if (existing.TryGetValue(def.Key, out var p)) { p.Group = def.Group; p.Description = def.Description; }
            else db.Permissions.Add(new Permission { Key = def.Key, Group = def.Group, Description = def.Description });
        }
        await db.SaveChangesAsync(ct);

        var permissionIds = await db.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id, ct);
        foreach (var def in RoleNames.Defaults)
        {
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == def.Name, ct);
            var isNew = role is null;
            role ??= db.Roles.Add(new Role { Name = def.Name }).Entity;
            role.DisplayName = def.DisplayName;
            role.Description = def.Description;
            role.IsSystem = true;
            // Yeni rolde varsayılan izinler; mevcut rolde yalnızca sistem yöneticisi senkronlanır (diğerleri ekrandan yönetilir).
            if (isNew || def.Name == RoleNames.SystemAdmin)
            {
                role.Permissions.Clear();
                foreach (var key in def.Permissions) role.Permissions.Add(new RolePermission { PermissionId = permissionIds[key] });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedClientsAsync(CancellationToken ct)
    {
        var redirects = (config["OAuth:WebRedirectUris"] ?? "http://localhost:8080/auth/callback,http://localhost:5173/auth/callback")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var web = await db.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == WebClientId, ct);
        if (web is null)
        {
            db.OAuthClients.Add(new OAuthClient
            {
                ClientId = WebClientId, Name = "StajDesk Web Arayüzü", IsFirstParty = true, RedirectUris = redirects,
                GrantTypes = ["authorization_code", "refresh_token"],
            });
        }
        else web.RedirectUris = redirects;

        // Servisten servise erişim örneği (ör. raporlama aracı): client_credentials, yalnızca okuma kapsamları.
        var reportingSecret = config["OAuth:ReportingClientSecret"];
        if (!string.IsNullOrWhiteSpace(reportingSecret) && !await db.OAuthClients.AnyAsync(c => c.ClientId == ReportingClientId, ct))
        {
            db.OAuthClients.Add(new OAuthClient
            {
                ClientId = ReportingClientId, Name = "Raporlama Servisi", SecretHash = hasher.Hash(reportingSecret),
                GrantTypes = ["client_credentials"], Scopes = [Permissions.TicketsReadAll, Permissions.ReportsReadAll, Permissions.CategoriesRead],
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;
        db.Categories.AddRange(
            new Category { Name = "Donanım", Description = "Bilgisayar, monitör, klavye vb. arızalar", Color = "violet" },
            new Category { Name = "Yazılım", Description = "Uygulama kurulumu, lisans ve hata bildirimleri", Color = "sky" },
            new Category { Name = "Ağ ve İnternet", Description = "Bağlantı, VPN ve Wi-Fi sorunları", Color = "emerald" },
            new Category { Name = "E-posta", Description = "Posta kutusu ve takvim", Color = "amber" },
            new Category { Name = "Hesap ve Erişim", Description = "Şifre sıfırlama, yetki talepleri", Color = "rose" },
            new Category { Name = "Yazıcı", Description = "Yazıcı ve tarayıcı sorunları", Color = "indigo" },
            new Category { Name = "Kasa / POS", Description = "Mağaza kasası, POS cihazı ve barkod okuyucu", Color = "orange" });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSystemAdminAsync(CancellationToken ct)
    {
        var email = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:AdminEmail / Seed:AdminPassword tanımlı değil; sistem yöneticisi oluşturulmadı.");
            return;
        }
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;

        var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.SystemAdmin, ct);
        var admin = new User { FullName = "Sistem Yöneticisi", Email = email, Department = "Bilgi İşlem", PasswordHash = hasher.Hash(password) };
        admin.Roles.Add(new UserRole { Role = role });
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("İlk kurulum: sistem yöneticisi oluşturuldu → {Email} (şifre: Seed__AdminPassword)", email);
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
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, ct);

        var kadikoy = new Store { Code = "IST-01", Name = "Kadıköy Mağazası", City = "İstanbul", Address = "Bahariye Cad. No:12", Phone = "0216 000 00 01" };
        var besiktas = new Store { Code = "IST-02", Name = "Beşiktaş Mağazası", City = "İstanbul", Address = "Barbaros Bulv. No:45", Phone = "0212 000 00 02" };
        var kizilay = new Store { Code = "ANK-01", Name = "Kızılay Mağazası", City = "Ankara", Address = "Atatürk Bulv. No:101", Phone = "0312 000 00 03" };
        db.Stores.AddRange(kadikoy, besiktas, kizilay);

        User U(string name, string email, string role, string dept, Store? store)
        {
            var u = new User { FullName = name, Email = email, Department = dept, PasswordHash = hash, Store = store };
            u.Roles.Add(new UserRole { Role = roles[role] });
            return u;
        }

        var agent1 = U("Elif Yılmaz", "elif.destek@stajdesk.local", RoleNames.Agent, "Bilgi İşlem", null);
        var agent2 = U("Burak Demir", "burak.destek@stajdesk.local", RoleNames.Agent, "Bilgi İşlem", null);
        var owner1 = U("Selin Aydın", "selin.magaza@stajdesk.local", RoleNames.StoreOwner, "Mağaza Yönetimi", kadikoy);
        var owner2 = U("Can Öztürk", "can.magaza@stajdesk.local", RoleNames.StoreOwner, "Mağaza Yönetimi", besiktas);
        var user1 = U("Ayşe Kaya", "ayse@stajdesk.local", RoleNames.User, "Satış", kadikoy);
        var user2 = U("Mehmet Çelik", "mehmet@stajdesk.local", RoleNames.User, "Kasa", kadikoy);
        var user3 = U("Zeynep Arslan", "zeynep@stajdesk.local", RoleNames.User, "Satış", besiktas);
        var user4 = U("Emre Şahin", "emre@stajdesk.local", RoleNames.User, "Depo", kizilay);
        db.Users.AddRange(agent1, agent2, owner1, owner2, user1, user2, user3, user4);
        await db.SaveChangesAsync(ct);

        Asset A(string name, AssetType type, AssetStatus status, string inv, string? manuf, string? model, Store? store, User? user, string? loc = null) =>
            new() { Name = name, Type = type, Status = status, InventoryNumber = inv, Manufacturer = manuf, Model = model, StoreId = store?.Id, AssignedUserId = user?.Id, Location = loc ?? store?.Name, PurchaseDate = new DateOnly(2024, 3, 12), WarrantyEndDate = new DateOnly(2027, 3, 12) };
        var assets = new[]
        {
            A("Dell Latitude 5440", AssetType.Laptop, AssetStatus.InUse, "INV-0001", "Dell", "Latitude 5440", kadikoy, user1),
            A("POS Terminali 1", AssetType.PointOfSale, AssetStatus.InUse, "INV-0002", "Ingenico", "Move 5000", kadikoy, user2, "Kadıköy / Kasa 1"),
            A("HP LaserJet Pro", AssetType.Printer, AssetStatus.InUse, "INV-0003", "HP", "LaserJet Pro M404", kadikoy, null, "Kadıköy / Ofis"),
            A("Lenovo ThinkPad T14", AssetType.Laptop, AssetStatus.InUse, "INV-0004", "Lenovo", "ThinkPad T14", besiktas, user3),
            A("POS Terminali 2", AssetType.PointOfSale, AssetStatus.InRepair, "INV-0005", "Ingenico", "Move 5000", besiktas, null, "Beşiktaş / Kasa 1"),
            A("Cisco Switch", AssetType.NetworkDevice, AssetStatus.InUse, "INV-0006", "Cisco", "Catalyst 9200", kizilay, null, "Kızılay / Sistem odası"),
            A("HP EliteDesk 800", AssetType.Computer, AssetStatus.InUse, "INV-0007", "HP", "EliteDesk 800 G9", kizilay, user4),
            A("iPhone 15", AssetType.Phone, AssetStatus.InStock, "INV-0008", "Apple", "iPhone 15", null, null, "Merkez depo"),
        };
        db.Assets.AddRange(assets);
        await db.SaveChangesAsync(ct);

        var cats = await db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);
        var rnd = new Random(42);
        var samples = new (string Title, string Desc, string Cat, TicketPriority P, User Req, Asset? A)[]
        {
            ("Dizüstü bilgisayar açılmıyor", "Sabah bilgisayarı açmaya çalıştığımda güç ışığı yanıyor ama ekran gelmiyor.", "Donanım", TicketPriority.High, user1, assets[0]),
            ("POS cihazı kart okumuyor", "Kasa 1'deki POS cihazı temassız kartları okumuyor, müşteri kuyruğu oluşuyor.", "Kasa / POS", TicketPriority.Critical, user2, assets[1]),
            ("Yazıcı kâğıt sıkıştırıyor", "Ofisteki yazıcı her çıktıda kâğıt sıkıştırıyor.", "Yazıcı", TicketPriority.Low, owner1, assets[2]),
            ("Mağaza Wi-Fi çok yavaş", "Öğleden sonra mağaza kablosuz ağı çok yavaşlıyor, el terminalleri bağlantı kopartıyor.", "Ağ ve İnternet", TicketPriority.High, user3, null),
            ("E-posta eki gönderilemiyor", "20 MB üzeri ekler gönderilemiyor, hata alıyorum.", "E-posta", TicketPriority.Medium, user3, null),
            ("Stok programına erişim talebi", "Depo stok modülüne okuma yetkisi verilmesini rica ederim.", "Hesap ve Erişim", TicketPriority.Medium, user4, null),
            ("Excel sürekli donuyor", "Büyük dosyaları açarken Excel yanıt vermiyor.", "Yazılım", TicketPriority.Medium, user1, assets[0]),
            ("Şifremi unuttum", "Windows oturum şifremi unuttum, sıfırlanmasını rica ederim.", "Hesap ve Erişim", TicketPriority.Critical, user2, null),
            ("Kasa 1 barkod okuyucu bozuk", "Barkod okuyucu ürünleri okumuyor, elle giriş yapıyoruz.", "Kasa / POS", TicketPriority.High, owner2, assets[4]),
            ("Klavyenin bazı tuşları çalışmıyor", "Laptop klavyesinde E ve R tuşları basmıyor.", "Donanım", TicketPriority.Medium, user3, assets[3]),
            ("Sunucuya erişilemiyor", "Ortak klasöre erişemiyorum, zaman aşımı hatası alıyorum.", "Ağ ve İnternet", TicketPriority.Critical, user4, assets[6]),
            ("Toner değişimi", "Yazıcıda toner bitti uyarısı veriyor.", "Yazıcı", TicketPriority.Low, user1, assets[2]),
        };

        var statuses = new[] { TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Pending, TicketStatus.Resolved, TicketStatus.Closed };
        var tickets = new List<Ticket>();
        for (var i = 0; i < samples.Length; i++)
        {
            var s = samples[i];
            var t = new Ticket(s.Title, s.Desc, s.Req.Id, cats[s.Cat], s.P, i % 4 == 3 ? TicketType.Request : TicketType.Incident, s.A?.Id, s.Req.StoreId);
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
                if (target == TicketStatus.Pending)
                {
                    t.ChangeStatus(TicketStatus.Pending);
                    t.Comments.Add(new TicketComment { AuthorId = agent.Id, Body = "Yedek parça sipariş edildi, gelince değişim yapılacak.", CreatedAt = created.AddHours(2) });
                    t.Comments.Add(new TicketComment { AuthorId = agent.Id, Body = "Tedarikçi teslimat tarihi: 2 iş günü.", IsInternal = true, CreatedAt = created.AddHours(2) });
                }
                if (target is TicketStatus.Resolved or TicketStatus.Closed)
                {
                    t.ChangeStatus(TicketStatus.Resolved);
                    t.Comments.Add(new TicketComment { AuthorId = agent.Id, Body = "Sorun giderildi. Tekrar yaşarsanız lütfen bildirin.", CreatedAt = created.AddHours(5) });
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
        foreach (var t in tickets)
        {
            var entry = db.Entry(t);
            if (t.ResolvedAt is not null) entry.Property(x => x.ResolvedAt).CurrentValue = t.CreatedAt.AddHours(5);
            if (t.ClosedAt is not null) entry.Property(x => x.ClosedAt).CurrentValue = t.CreatedAt.AddHours(8);
            entry.Property(x => x.UpdatedAt).CurrentValue = t.Status == TicketStatus.Open ? null : t.CreatedAt.AddHours(5);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo verisi eklendi: 3 mağaza, 8 kullanıcı, {Assets} cihaz, {Tickets} talep", assets.Length, tickets.Count);
    }
}
