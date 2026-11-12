namespace StajDesk.Domain.Security;

/// <summary>
/// Rol ve izin (RBAC) modeli.
/// - İzinler küçük, tek işi anlatan anahtarlardır: "tickets.read.all"
/// - Roller izin kümeleridir; bir kullanıcıya birden fazla rol verilebilir.
/// - Kod rol adına değil izne bakar → yeni rol eklemek kod değişikliği gerektirmez.
/// - Varsayılan matris ilk kurulumda veritabanına yazılır, sonra "Roller ve İzinler" ekranından değiştirilebilir.
/// </summary>
public static class Permissions
{
    public const string TicketsCreate = "tickets.create";
    public const string TicketsReadOwn = "tickets.read.own";
    public const string TicketsReadStore = "tickets.read.store";
    public const string TicketsReadAll = "tickets.read.all";
    public const string TicketsUpdateOwn = "tickets.update.own";
    public const string TicketsManage = "tickets.manage";
    public const string TicketsDelete = "tickets.delete";
    public const string CommentsCreate = "comments.create";
    public const string CommentsInternal = "comments.internal";
    public const string CategoriesRead = "categories.read";
    public const string CategoriesManage = "categories.manage";
    public const string StoresRead = "stores.read";
    public const string StoresManage = "stores.manage";
    public const string AssetsReadOwn = "assets.read.own";
    public const string AssetsReadStore = "assets.read.store";
    public const string AssetsReadAll = "assets.read.all";
    public const string AssetsManage = "assets.manage";
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string ReportsReadStore = "reports.read.store";
    public const string ReportsReadAll = "reports.read.all";
    public const string AuditRead = "audit.read";
    public const string MailRead = "mail.read";
    public const string AnnouncementsManage = "announcements.manage";
    public const string OnCallManage = "oncall.manage";

    public sealed record Definition(string Key, string Group, string Description);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(TicketsCreate, "Talepler", "Talep oluşturma"),
        new(TicketsReadOwn, "Talepler", "Kendi taleplerini görme"),
        new(TicketsReadStore, "Talepler", "Mağazasındaki tüm talepleri görme"),
        new(TicketsReadAll, "Talepler", "Tüm talepleri görme"),
        new(TicketsUpdateOwn, "Talepler", "Açık durumdaki kendi talebini düzenleme"),
        new(TicketsManage, "Talepler", "Durum, öncelik ve atama değiştirme"),
        new(TicketsDelete, "Talepler", "Talep silme"),
        new(CommentsCreate, "Talepler", "Talebe yanıt yazma"),
        new(CommentsInternal, "Talepler", "Dahili not yazma ve görme"),
        new(CategoriesRead, "Kategoriler", "Kategorileri görme"),
        new(CategoriesManage, "Kategoriler", "Kategori ekleme / düzenleme / silme"),
        new(StoresRead, "Mağazalar", "Mağazaları görme"),
        new(StoresManage, "Mağazalar", "Mağaza ekleme / düzenleme"),
        new(AssetsReadOwn, "Envanter", "Kendisine zimmetli cihazları görme"),
        new(AssetsReadStore, "Envanter", "Mağazasındaki cihazları görme"),
        new(AssetsReadAll, "Envanter", "Tüm envanteri görme"),
        new(AssetsManage, "Envanter", "Cihaz ekleme / düzenleme / zimmetleme"),
        new(UsersRead, "Kullanıcılar", "Kullanıcıları görme"),
        new(UsersManage, "Kullanıcılar", "Kullanıcı oluşturma, rol atama, pasifleştirme"),
        new(RolesManage, "Kullanıcılar", "Rollerin izinlerini düzenleme"),
        new(ReportsReadStore, "Raporlar", "Mağaza özet paneli"),
        new(ReportsReadAll, "Raporlar", "Genel özet paneli ve ekip iş yükü"),
        new(AuditRead, "Sistem", "Denetim kayıtlarını görme"),
        new(MailRead, "Sistem", "Gönderilen e-postaları görme (demo posta kutusu)"),
        new(AnnouncementsManage, "Panel", "Duyuru yayınlama / düzenleme"),
        new(OnCallManage, "Panel", "Bilgi işlem nöbet çizelgesini düzenleme"),
    ];

    public static bool IsKnown(string key) => All.Any(p => p.Key == key);
}

public static class RoleNames
{
    public const string SystemAdmin = "system_admin";
    public const string Agent = "agent";
    public const string StoreOwner = "store_owner";
    public const string User = "user";

    public sealed record Definition(string Name, string DisplayName, string Description, string[] Permissions);

    public static readonly IReadOnlyList<Definition> Defaults =
    [
        new(SystemAdmin, "Sistem Yöneticisi", "Sistemin tamamını yönetir.", Security.Permissions.All.Select(p => p.Key).ToArray()),
        new(Agent, "Destek Personeli", "Talepleri karşılar, çözer ve envanteri yönetir.",
        [
            Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsReadAll, Security.Permissions.TicketsUpdateOwn,
            Security.Permissions.TicketsManage, Security.Permissions.CommentsCreate, Security.Permissions.CommentsInternal,
            Security.Permissions.CategoriesRead, Security.Permissions.StoresRead, Security.Permissions.AssetsReadOwn,
            Security.Permissions.AssetsReadAll, Security.Permissions.AssetsManage, Security.Permissions.UsersRead,
            Security.Permissions.ReportsReadAll, Security.Permissions.MailRead, Security.Permissions.OnCallManage,
        ]),
        new(StoreOwner, "Mağaza Sahibi", "Kendi mağazasının taleplerini, cihazlarını ve raporunu görür.",
        [
            Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsReadStore,
            Security.Permissions.TicketsUpdateOwn, Security.Permissions.CommentsCreate, Security.Permissions.CategoriesRead,
            Security.Permissions.StoresRead, Security.Permissions.AssetsReadOwn, Security.Permissions.AssetsReadStore,
            Security.Permissions.UsersRead, Security.Permissions.ReportsReadStore,
        ]),
        new(User, "Kullanıcı", "Talep açar ve kendi taleplerini takip eder.",
        [
            Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsUpdateOwn,
            Security.Permissions.CommentsCreate, Security.Permissions.CategoriesRead, Security.Permissions.AssetsReadOwn,
        ]),
    ];
}
