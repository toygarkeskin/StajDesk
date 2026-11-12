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
public sealed class DbSeeder(AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DbSeeder> logger, IFileStorage storage)
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
        await SeedPositionsAsync(ct);
        await SeedSiteSettingsAsync(ct);
        await SeedWorkflowsAndLeaveAsync(ct);
        if (config.GetValue("Seed:DemoData", false))
        {
            await SeedDemoAsync(ct);
            await SeedWorkspaceDemoAsync(ct);
            await SeedOrganizationDemoAsync(ct);
            await SeedItDemoAsync(ct);
            await SeedSiteDemoAsync(ct);
            await SeedPortalDemoAsync(ct);
            await SeedLeaveDemoAsync(ct);
        }
    }

    /// <summary>Kodda tanımlı izinler veritabanına eklenir; Sistem Yöneticisi her zaman tüm izinlere sahip olur.</summary>
    private async Task SyncPermissionsAndRolesAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, ct);
        var firstRun = existing.Count == 0;
        var addedKeys = new HashSet<string>();
        foreach (var def in Permissions.All)
        {
            if (existing.TryGetValue(def.Key, out var p)) { p.Group = def.Group; p.Description = def.Description; }
            else
            {
                db.Permissions.Add(new Permission { Key = def.Key, Group = def.Group, Description = def.Description });
                addedKeys.Add(def.Key);
            }
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
            else if (!firstRun)
            {
                // Sürüm güncellemesiyle gelen YENİ izinler, varsayılanında bu izni içeren rollere bir kez eklenir.
                // (Yöneticinin ekrandan kaldırdığı eski izinlere dokunulmaz.)
                foreach (var key in def.Permissions.Where(addedKeys.Contains))
                    role.Permissions.Add(new RolePermission { PermissionId = permissionIds[key] });
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
        if (await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct) is { } existing)
        {
            // Önceki sürümde açılmış yönetici: kullanıcı adı yoksa (ve boştaysa) "sysadmin" verilir.
            if (existing.Username is null && !await db.Users.AnyAsync(u => u.Username == "sysadmin", ct))
            {
                existing.Username = "sysadmin";
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.SystemAdmin, ct);
        var admin = new User { FullName = "Sistem Yöneticisi", Email = email, Username = "sysadmin", PasswordHash = hasher.Hash(password), EmailVerifiedAt = DateTime.UtcNow, Profile = new EmployeeProfile() };
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
            var u = new User { FullName = name, Email = email, PasswordHash = hash, Store = store, EmailVerifiedAt = DateTime.UtcNow };
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
        agent1.Phone = "0555 010 10 01";
        agent2.Phone = "0555 010 10 02";
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

    /// <summary>Panel demo verisi: duyurular ve bu haftanın / gelecek haftanın nöbetçileri. Mevcut veritabanına da (bir kez) eklenir.</summary>
    private async Task SeedWorkspaceDemoAsync(CancellationToken ct)
    {
        var agents = await db.Users.Where(u => u.Email == "elif.destek@stajdesk.local" || u.Email == "burak.destek@stajdesk.local")
            .OrderBy(u => u.Email).ToListAsync(ct);
        var admin = await db.Users.Where(u => u.Roles.Any(r => r.Role.Name == RoleNames.SystemAdmin)).OrderBy(u => u.Id).FirstOrDefaultAsync(ct);
        if (agents.Count < 2 || admin is null) return;
        var burak = agents[0];
        var elif = agents[1];
        burak.Phone ??= "0555 010 10 02";
        elif.Phone ??= "0555 010 10 01";

        if (!await db.Announcements.AnyAsync(ct))
        {
            var kadikoy = await db.Stores.Where(s => s.Code == "IST-01").Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
            var now = DateTime.UtcNow;
            db.Announcements.AddRange(
                new Announcement
                {
                    Title = "Cumartesi 02:00–04:00 planlı bakım", AuthorId = admin.Id, Level = AnnouncementLevel.Warning, IsPinned = true, PublishAt = now.AddDays(-1), ExpiresAt = now.AddDays(10),
                    Body = "Merkez sunucularda güncelleme yapılacaktır. Bu saatlerde stok programına ve ortak klasörlere erişilemeyecektir. Kasa sistemleri çalışmaya devam eder.",
                },
                new Announcement
                {
                    Title = "Yeni talep kategorisi: Kasa / POS", AuthorId = admin.Id, Level = AnnouncementLevel.Info, PublishAt = now.AddDays(-3),
                    Body = "POS cihazı, barkod okuyucu ve kasa yazıcısı sorunları için artık Kasa / POS kategorisini seçin. Bu talepler mağaza ekibine öncelikli yönlendirilir.",
                },
                new Announcement
                {
                    Title = "Kadıköy: Wi-Fi erişim noktası değişimi", AuthorId = admin.Id, Level = AnnouncementLevel.Info, PublishAt = now.AddHours(-6), ExpiresAt = now.AddDays(5), StoreId = kadikoy,
                    Body = "Perşembe sabahı mağaza açılmadan önce kablosuz erişim noktaları değiştirilecek. El terminallerinde kısa süreli bağlantı kesintisi yaşanabilir.",
                });
        }

        if (!await db.OnCallShifts.AnyAsync(ct))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            db.OnCallShifts.AddRange(
                new OnCallShift { UserId = elif.Id, StartsOn = monday, EndsOn = monday.AddDays(6), Note = "Mesai dışı acil durumlar için" },
                new OnCallShift { UserId = burak.Id, StartsOn = monday.AddDays(7), EndsOn = monday.AddDays(13), Note = "Mesai dışı acil durumlar için" },
                new OnCallShift { UserId = elif.Id, StartsOn = monday.AddDays(14), EndsOn = monday.AddDays(20) });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Varsayılan unvanlar (yalnızca hiç unvan yokken). Seviye 1 en üst. Unvan sistem rolü değildir;
    /// "Super Admin / Admin" birer yetki rolüdür ve burada değil Roller ve İzinler ekranında yönetilir.
    /// </summary>
    private async Task SeedPositionsAsync(CancellationToken ct)
    {
        if (await db.Positions.AnyAsync(ct)) return;
        (string Title, int Level)[] defaults =
        [
            ("CEO", 1), ("Genel Müdür", 2), ("Direktör", 3), ("Müdür", 4), ("Mağaza Müdürü", 4), ("Yönetici", 5),
            ("Departman Sorumlusu", 6), ("Uzman", 7), ("İK Uzmanı", 7), ("BT Destek Uzmanı", 7),
            ("Satış Danışmanı", 8), ("Kasa Sorumlusu", 8), ("Depo Sorumlusu", 8), ("Çalışan", 9), ("Stajyer", 10),
        ];
        db.Positions.AddRange(defaults.Select(d => new Position { Title = d.Title, Level = d.Level }));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Demo organizasyonu: departman ağacı, unvanlar, yönetici ilişkileri, personel kartları ve bir İK personeli.
    /// Mevcut demo veritabanına da bir kez uygulanır ("GM" departmanı yoksa).
    /// </summary>
    private async Task SeedOrganizationDemoAsync(CancellationToken ct)
    {
        if (await db.Departments.AnyAsync(d => d.Code == "GM", ct)) return;
        var users = await db.Users.Include(u => u.Profile).Where(u => u.Email.EndsWith("@stajdesk.local")).ToDictionaryAsync(u => u.Email, ct);
        User? Get(string email) => users.GetValueOrDefault(email);
        var positions = await db.Positions.ToDictionaryAsync(p => p.Title, ct);
        var password = config["Seed:DemoPassword"];

        // İK personeli (rolü yalnızca personel yönetimi verir)
        if (Get("deniz.ik@stajdesk.local") is null && !string.IsNullOrWhiteSpace(password))
        {
            var hr = new User { FullName = "Deniz Koç", Email = "deniz.ik@stajdesk.local", PasswordHash = hasher.Hash(password), EmailVerifiedAt = DateTime.UtcNow, Phone = "0555 020 20 01" };
            hr.Roles.Add(new UserRole { Role = await db.Roles.FirstAsync(r => r.Name == RoleNames.Hr, ct) });
            db.Users.Add(hr);
            await db.SaveChangesAsync(ct);
            users[hr.Email] = hr;
        }

        var gm = new Department { Code = "GM", Name = "Genel Müdürlük", Description = "Şirketin üst yönetimi" };
        var bt = new Department { Code = "BT", Name = "Bilgi Teknolojileri ve Süreç Geliştirme", Parent = gm, Description = "Destek, altyapı ve yazılım" };
        var ik = new Department { Code = "IK", Name = "İnsan Kaynakları ve İdari İşler", Parent = gm };
        var mg = new Department { Code = "MG", Name = "Mağazacılık", Parent = gm, Description = "Mağaza operasyonları" };
        var lj = new Department { Code = "LJ", Name = "Lojistik", Parent = gm };
        db.Departments.AddRange(gm, bt, ik, mg, lj);
        await db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var n = 1000;
        void Place(string email, Department dept, string title, string? managerEmail, DateOnly hire, DateOnly? birth, EmploymentType type = EmploymentType.FullTime)
        {
            if (Get(email) is not { } u) return;
            u.DepartmentId = dept.Id;
            u.PositionId = positions.GetValueOrDefault(title)?.Id;
            u.ManagerId = managerEmail is null ? null : Get(managerEmail)?.Id;
            u.Username ??= email.Split('@')[0].Replace(".destek", "").Replace(".magaza", "").Replace(".ik", "");
            u.EmailVerifiedAt ??= DateTime.UtcNow;
            u.Profile ??= new EmployeeProfile();
            u.Profile.EmployeeNumber ??= $"P{++n}";
            u.Profile.HireDate ??= hire;
            u.Profile.BirthDate ??= birth;
            u.Profile.EmploymentType = type;
        }

        Place("burak.destek@stajdesk.local", bt, "Müdür", null, new DateOnly(2019, 3, 4), new DateOnly(1986, 7, 21));
        Place("elif.destek@stajdesk.local", bt, "BT Destek Uzmanı", "burak.destek@stajdesk.local", today.AddYears(-3).AddDays(3), new DateOnly(1994, 2, 9));
        Place("deniz.ik@stajdesk.local", ik, "İK Uzmanı", null, new DateOnly(2021, 9, 1), new DateOnly(1990, 11, 30));
        Place("selin.magaza@stajdesk.local", mg, "Mağaza Müdürü", null, new DateOnly(2018, 5, 14), new DateOnly(1988, 4, 2));
        Place("can.magaza@stajdesk.local", mg, "Mağaza Müdürü", null, new DateOnly(2020, 1, 6), new DateOnly(1991, 9, 17));
        // Bu hafta doğum günü olan biri: panelde "Doğum günleri" kutusu boş kalmasın.
        Place("ayse@stajdesk.local", mg, "Satış Danışmanı", "selin.magaza@stajdesk.local", new DateOnly(2023, 2, 20), new DateOnly(1998, today.Month, Math.Min(today.AddDays(2).Day, 28)));
        Place("mehmet@stajdesk.local", mg, "Kasa Sorumlusu", "selin.magaza@stajdesk.local", new DateOnly(2022, 8, 1), new DateOnly(1995, 6, 12));
        Place("zeynep@stajdesk.local", mg, "Satış Danışmanı", "can.magaza@stajdesk.local", today.AddDays(-20), new DateOnly(2001, 12, 3), EmploymentType.PartTime);
        Place("emre@stajdesk.local", lj, "Depo Sorumlusu", null, new DateOnly(2024, 10, 15), new DateOnly(1997, 3, 28));

        bt.ManagerId = Get("burak.destek@stajdesk.local")?.Id;
        ik.ManagerId = Get("deniz.ik@stajdesk.local")?.Id;
        await db.SaveChangesAsync(ct);

        // Eski serbest metin alanından taşınıp artık boş kalan departmanlar demo ortamında temizlenir.
        var legacy = await db.Departments.Where(d => d.Description == "Eski departman alanından taşındı" && !d.Members.Any()).ToListAsync(ct);
        db.Departments.RemoveRange(legacy);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo organizasyonu eklendi: 5 departman, unvanlar, yönetici ilişkileri ve personel kartları");
    }

    /// <summary>Demo: hesap taleplerini onaylayan BT hesap yöneticisi (bir kez eklenir).</summary>
    private async Task SeedItDemoAsync(CancellationToken ct)
    {
        const string email = "kaan.bt@stajdesk.local";
        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password) || await db.Users.AnyAsync(u => u.Email == email, ct)) return;
        var bt = await db.Departments.FirstOrDefaultAsync(d => d.Code == "BT", ct);
        var title = await db.Positions.FirstOrDefaultAsync(p => p.Title == "BT Destek Uzmanı", ct);
        var manager = await db.Users.FirstOrDefaultAsync(u => u.Email == "burak.destek@stajdesk.local", ct);
        var it = new User
        {
            FullName = "Kaan Arslan", Email = email, Username = "kaan", PasswordHash = hasher.Hash(password), EmailVerifiedAt = DateTime.UtcNow,
            DepartmentId = bt?.Id, PositionId = title?.Id, ManagerId = manager?.Id, Phone = "0555 010 10 03",
            Profile = new EmployeeProfile { EmployeeNumber = "P1010", HireDate = new DateOnly(2022, 4, 4) },
        };
        it.Roles.Add(new UserRole { Role = await db.Roles.FirstAsync(r => r.Name == RoleNames.ItAdmin, ct) });
        db.Users.Add(it);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo BT hesap yöneticisi eklendi: {Email}", email);
    }

    /// <summary>Kurumsal sitenin temel metinleri (yalnızca eksik anahtarlar eklenir; yöneticinin değiştirdikleri korunur).</summary>
    private async Task SeedSiteSettingsAsync(CancellationToken ct)
    {
        var defaults = new Dictionary<string, string>
        {
            [Application.Features.Site.SiteKeys.CompanyName] = "StajDesk Perakende A.Ş.",
            [Application.Features.Site.SiteKeys.Tagline] = "Mağazalarımız ve ekiplerimiz için tek kurumsal portal.",
            [Application.Features.Site.SiteKeys.AboutTitle] = "Hakkımızda",
            [Application.Features.Site.SiteKeys.AboutBody] = "Türkiye genelindeki mağazalarımız, merkez ofisimiz ve depolarımızla müşterilerimize hizmet veriyoruz. StajDesk; destek talepleri, duyurular ve iş süreçlerini tek bir yerde toplayan kurumsal portalımızdır.\n\nBu metin demo içeriktir; Kurumsal site → Site metinleri ekranından değiştirilebilir.",
            [Application.Features.Site.SiteKeys.Mission] = "Çalışanlarımızın işini kolaylaştıran, hızlı ve güvenilir iç hizmetler sunmak.",
            [Application.Features.Site.SiteKeys.Vision] = "Tüm iş süreçlerinin tek portaldan, kâğıtsız ve izlenebilir yürütüldüğü bir şirket olmak.",
            [Application.Features.Site.SiteKeys.ContactAddress] = "Örnek Mah. Demo Cad. No:1, İstanbul",
            [Application.Features.Site.SiteKeys.ContactPhone] = "0212 000 00 00",
            [Application.Features.Site.SiteKeys.ContactEmail] = "iletisim@stajdesk.local",
            [Application.Features.Site.SiteKeys.CareerEmail] = "kariyer@stajdesk.local",
            [Application.Features.Site.SiteKeys.WeatherCity] = "İstanbul",
            [Application.Features.Site.SiteKeys.Links] = "[]",
        };
        var existing = await db.SiteSettings.Select(x => x.Key).ToListAsync(ct);
        foreach (var (key, value) in defaults.Where(d => !existing.Contains(d.Key)))
            db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Demo kurumsal site içeriği: haberler, açık pozisyonlar, herkese açık duyuru, reklamlar (bir kez).</summary>
    private async Task SeedSiteDemoAsync(CancellationToken ct)
    {
        if (await db.News.AnyAsync(ct) || await db.Advertisements.AnyAsync(ct)) return;
        var author = await db.Users.Where(u => u.Roles.Any(r => r.Role.Name == RoleNames.SystemAdmin)).OrderBy(u => u.Id).FirstOrDefaultAsync(ct);
        if (author is null) return;
        var now = DateTime.UtcNow;

        // Departman tanıtımları: üst düzey birimler sitede görünür
        foreach (var d in await db.Departments.Where(d => d.Parent != null && d.Parent.Code == "GM").ToListAsync(ct))
        {
            d.ShowOnWebsite = true;
            d.Description ??= d.Code switch
            {
                "IK" => "İşe alım, eğitim ve çalışan deneyimi.",
                "LJ" => "Depo ve mağaza sevkiyatları.",
                _ => d.Description,
            };
        }

        async Task<string> Banner((byte, byte, byte) a, (byte, byte, byte) b, string name)
        {
            var key = Guid.NewGuid().ToString("N");
            var bytes = DemoImages.Gradient(1200, 360, a, b);
            await using (var ms = new MemoryStream(bytes)) await storage.SaveAsync(key, ms, ct);
            db.MediaFiles.Add(new MediaFile { Key = key, ContentType = "image/png", Size = bytes.Length, FileName = name, UploadedById = author.Id });
            return key;
        }

        var b1 = await Banner((49, 46, 129), (124, 58, 237), "demo-banner-1.png");
        var b2 = await Banner((15, 118, 110), (14, 165, 233), "demo-banner-2.png");
        var b3 = await Banner((154, 52, 18), (225, 29, 72), "demo-banner-3.png");
        var side = await Banner((30, 41, 59), (71, 85, 105), "demo-yan.png");
        db.Advertisements.AddRange(
            new Advertisement { Title = "Kış sezonu mağazalarımızda", Description = "Yeni sezon ürünleri tüm mağazalarımızda. (Demo reklam)", ImageKey = b1, Page = AdPage.Landing, Placement = AdPlacement.Hero, SortOrder = 1, StartsAt = now.AddDays(-2), EndsAt = now.AddDays(45) },
            new Advertisement { Title = "Kızılay mağazamız yenilendi", Description = "Ankara Kızılay mağazamız yeni yüzüyle hizmetinizde. (Demo reklam)", ImageKey = b2, Page = AdPage.Landing, Placement = AdPlacement.Hero, SortOrder = 2, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(30) },
            new Advertisement { Title = "Geçmiş kampanya", Description = "Süresi dolmuş reklam örneği: sitede görünmez.", ImageKey = b3, Page = AdPage.Landing, Placement = AdPlacement.Hero, SortOrder = 3, StartsAt = now.AddDays(-40), EndsAt = now.AddDays(-10) },
            new Advertisement { Title = "Kariyer fırsatları", Description = "Açık pozisyonlarımıza göz atın.", ImageKey = side, Page = AdPage.Landing, Placement = AdPlacement.Sidebar, SortOrder = 1, StartsAt = now.AddDays(-1) },
            new Advertisement { Title = "Portal ipucu: Talebinize ekran görüntüsü ekleyin", Description = "Sorunun ekran görüntüsü, çözüm süresini kısaltır.", ImageKey = b2, Page = AdPage.Portal, Placement = AdPlacement.Inline, SortOrder = 1, StartsAt = now.AddDays(-1) });

        var newsImage = await Banner((67, 56, 202), (16, 185, 129), "demo-haber.png");
        db.News.AddRange(
            new NewsArticle { Slug = "kurumsal-portalimiz-yayinda", Title = "Kurumsal portalımız yayında", Summary = "Destek talepleri, duyurular ve hesap işlemleri artık tek portalda.", ImageKey = newsImage, IsPublished = true, PublishedAt = now.AddDays(-3), AuthorId = author.Id,
                Body = "StajDesk ile destek taleplerinizi açabilir, durumunu anlık takip edebilir ve şirket duyurularını tek yerden görebilirsiniz.\n\nPortala erişim için İnsan Kaynakları hesap talebinizi açar; Bilgi Teknolojileri onayladığında size ilk şifre bağlantısı gönderilir. (Demo haber)" },
            new NewsArticle { Slug = "kizilay-magazamiz-yenilendi", Title = "Kızılay mağazamız yenilendi", Summary = "Ankara Kızılay mağazamız yeni yerleşimi ve genişleyen ürün alanıyla açıldı.", IsPublished = true, PublishedAt = now.AddDays(-8), AuthorId = author.Id,
                Body = "Kızılay mağazamızda kasa alanı genişletildi, yeni POS cihazları kuruldu ve depo düzeni yenilendi. (Demo haber)" },
            new NewsArticle { Slug = "taslak-haber", Title = "Taslak haber", Summary = "Yayımlanmamış haber örneği: sitede görünmez.", Body = "Taslak.", IsPublished = false, PublishedAt = now, AuthorId = author.Id });

        var bt = await db.Departments.FirstOrDefaultAsync(d => d.Code == "BT", ct);
        var mg = await db.Departments.FirstOrDefaultAsync(d => d.Code == "MG", ct);
        db.JobOpenings.AddRange(
            new JobOpening { Title = "Satış Danışmanı", DepartmentId = mg?.Id, Location = "İstanbul / Kadıköy", EmploymentType = EmploymentType.PartTime, IsPublished = true, PublishedAt = now.AddDays(-5), ClosesOn = DateOnly.FromDateTime(now.AddDays(25)),
                Description = "Mağazamızda müşterilerimize ürün danışmanlığı yapacak, kasa ve stok süreçlerine destek olacak takım arkadaşı arıyoruz.", Requirements = "Lise mezunu\nHafta sonu çalışabilecek\nMüşteri iletişimi güçlü" },
            new JobOpening { Title = "BT Destek Uzmanı", DepartmentId = bt?.Id, Location = "İstanbul / Merkez", EmploymentType = EmploymentType.FullTime, IsPublished = true, PublishedAt = now.AddDays(-2),
                Description = "Mağaza ve merkez ofis kullanıcılarının donanım, yazılım ve ağ taleplerini karşılayacak uzman arıyoruz.", Requirements = "Üniversitelerin ilgili bölümlerinden mezun\nWindows, ağ ve POS cihazları deneyimi\nTalep yönetimi araçlarına hâkim" });

        var maintenance = await db.Announcements.FirstOrDefaultAsync(a => a.StoreId == null && a.IsPinned, ct);
        if (maintenance is not null) maintenance.IsPublic = true;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo kurumsal site içeriği eklendi (haberler, ilanlar, reklamlar)");
    }

    /// <summary>Demo portal içeriği: etkinlikler, takdirler; demo çalışanların doğum günü paylaşımı (bir kez).</summary>
    private async Task SeedPortalDemoAsync(CancellationToken ct)
    {
        if (await db.CompanyEvents.AnyAsync(ct) || await db.Recognitions.AnyAsync(ct)) return;
        var admin = await db.Users.Where(u => u.Roles.Any(r => r.Role.Name == RoleNames.SystemAdmin)).OrderBy(u => u.Id).FirstOrDefaultAsync(ct);
        if (admin is null) return;
        var now = DateTime.UtcNow;
        db.CompanyEvents.AddRange(
            new CompanyEvent { Title = "Yeni portal tanıtım eğitimi", Description = "Destek talepleri, hesap işlemleri ve panel kişiselleştirme. (Demo etkinlik)", Location = "Merkez ofis · Toplantı salonu", StartsAt = now.Date.AddDays(3).AddHours(7), EndsAt = now.Date.AddDays(3).AddHours(8), CreatedById = admin.Id },
            new CompanyEvent { Title = "Yılsonu buluşması", Description = "Tüm ekiplerin katılımıyla yılsonu değerlendirmesi. (Demo etkinlik)", Location = "İstanbul", StartsAt = now.Date.AddDays(20).AddHours(15), CreatedById = admin.Id });

        var elif = await db.Users.FirstOrDefaultAsync(u => u.Email == "elif.destek@stajdesk.local", ct);
        var selin = await db.Users.FirstOrDefaultAsync(u => u.Email == "selin.magaza@stajdesk.local", ct);
        var today = DateOnly.FromDateTime(now.AddHours(3));
        if (elif is not null)
            db.Recognitions.Add(new Recognition { UserId = elif.Id, Type = RecognitionType.EmployeeOfWeek, Title = "Haftanın çalışanı", Description = "Kızılay mağazasının POS geçişini mesai dışında tamamladı. (Demo)", Date = today.AddDays(-1), CreatedById = admin.Id });
        if (selin is not null)
            db.Recognitions.Add(new Recognition { UserId = selin.Id, Type = RecognitionType.Achievement, Title = "Ekim ayı satış hedefi aşıldı", Description = "Kadıköy mağazası hedefin %112'sine ulaştı. (Demo)", Date = today.AddDays(-6), CreatedById = admin.Id });

        // Doğum günü paylaşımı açık rızaya bağlı; demo çalışanlar için örnek olarak açılır (gerçek kullanıcılar kendisi açar).
        var demoIds = await db.Users.Where(u => u.Email.EndsWith("@stajdesk.local") && u.Profile != null && u.Profile.BirthDate != null).Select(u => u.Id).ToListAsync(ct);
        var existing = await db.UserSettings.Where(x => demoIds.Contains(x.UserId)).ToListAsync(ct);
        foreach (var id in demoIds)
        {
            var st = existing.FirstOrDefault(x => x.UserId == id) ?? db.UserSettings.Add(new UserSettings { UserId = id }).Entity;
            st.ShowBirthday = true;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo portal içeriği eklendi (etkinlikler, takdirler)");
    }

    /// <summary>Onay zincirleri ve izin türleri (her ortamda; yalnızca eksikse eklenir, yöneticinin değişikliklerine dokunulmaz).</summary>
    private async Task SeedWorkflowsAndLeaveAsync(CancellationToken ct)
    {
        if (!await db.WorkflowDefinitions.AnyAsync(d => d.Key == Application.Features.Workflows.WorkflowKeys.Leave, ct))
        {
            var def = new WorkflowDefinition { Key = Application.Features.Workflows.WorkflowKeys.Leave, Name = "İzin talebi", Description = "Çalışanın izin talebi: önce yöneticisi, sonra izin yöneticisi onaylar." };
            def.Steps.Add(new WorkflowStepDefinition { Order = 1, Name = "Yönetici onayı", ApproverType = ApproverType.Manager });
            def.Steps.Add(new WorkflowStepDefinition { Order = 2, Name = "İzin yöneticisi onayı", ApproverType = ApproverType.Role, RoleName = RoleNames.LeaveAdmin });
            db.WorkflowDefinitions.Add(def);
        }
        if (!await db.LeaveTypes.AnyAsync(ct))
            db.LeaveTypes.AddRange(
                new LeaveType { Code = "YILLIK", Name = "Yıllık izin", TracksBalance = true, DefaultDaysPerYear = 14, SortOrder = 1, Description = "Yıllık ücretli izin. Kıdeme göre kişiye özel hak tanımlanabilir." },
                new LeaveType { Code = "MAZERET", Name = "Mazeret izni", TracksBalance = true, DefaultDaysPerYear = 5, SortOrder = 2, Description = "Evlilik, vefat gibi mazeretler." },
                new LeaveType { Code = "UCRETSIZ", Name = "Ücretsiz izin", IsPaid = false, SortOrder = 3 },
                new LeaveType { Code = "DOGUM", Name = "Doğum izni", SortOrder = 4, Description = "Yasal süre; belge İK'ya iletilir." },
                new LeaveType { Code = "SUT", Name = "Süt izni", SortOrder = 5 },
                new LeaveType { Code = "GOREV", Name = "Görev izni", SortOrder = 6, Description = "Şirket adına dış görev (eğitim, fuar vb.)." },
                new LeaveType { Code = "UZAKTAN", Name = "Uzaktan çalışma", SortOrder = 7 },
                new LeaveType { Code = "DENK", Name = "Denkleştirme izni", TracksBalance = true, DefaultDaysPerYear = 0, SortOrder = 8, Description = "Fazla çalışma karşılığı; hak izin yöneticisince tanımlanır." });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Demo: İK'daki deniz.ik'e ayrıca "İzin Yöneticisi" rolü (İK rolü izin yönetimini kendiliğinden vermez).</summary>
    private async Task SeedLeaveDemoAsync(CancellationToken ct)
    {
        var deniz = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == "deniz.ik@stajdesk.local", ct);
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.LeaveAdmin, ct);
        if (deniz is null || role is null || deniz.Roles.Any(r => r.RoleId == role.Id)) return;
        if (await db.LeaveRequests.AnyAsync(ct)) return; // yalnızca ilk kurulumda
        deniz.Roles.Add(new UserRole { RoleId = role.Id });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo: deniz.ik kullanıcısına İzin Yöneticisi rolü verildi");
    }
}
