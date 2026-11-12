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
    public const string EmployeesReadPrivate = "employees.read.private";
    public const string EmployeesManage = "employees.manage";
    public const string OrgManage = "org.manage";
    public const string AccountsRequest = "accounts.request";
    public const string ContentManage = "content.manage";
    public const string AdsManage = "ads.manage";
    public const string WidgetsManage = "widgets.manage";
    public const string LeaveRequest = "leave.request";
    public const string LeaveManage = "leave.manage";
    public const string WorkflowsManage = "workflows.manage";
    public const string TasksManage = "tasks.manage";
    public const string SocialPost = "social.post";
    public const string SocialModerate = "social.moderate";
    public const string FaqManage = "faq.manage";

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
        new(UsersManage, "Kullanıcılar", "Hesap taleplerini onaylama, hesap açma / pasifleştirme / kilitleme, rol atama (BT)"),
        new(RolesManage, "Kullanıcılar", "Rollerin izinlerini düzenleme"),
        new(ReportsReadStore, "Raporlar", "Mağaza özet paneli"),
        new(ReportsReadAll, "Raporlar", "Genel özet paneli ve ekip iş yükü"),
        new(AuditRead, "Sistem", "Denetim kayıtlarını görme"),
        new(MailRead, "Sistem", "Gönderilen e-postaları görme (demo posta kutusu)"),
        new(AnnouncementsManage, "Panel", "Duyuru yayınlama / düzenleme"),
        new(OnCallManage, "Panel", "Bilgi işlem nöbet çizelgesini düzenleme"),
        new(EmployeesReadPrivate, "Personel", "Çalışanların kişisel bilgilerini görme (adres, doğum tarihi, acil durum kişisi)"),
        new(EmployeesManage, "Personel", "Personel kaydı oluşturma ve personel bilgilerini düzenleme (İK)"),
        new(OrgManage, "Personel", "Departman ve unvan yönetimi, organizasyon şeması"),
        new(AccountsRequest, "Personel", "Yeni çalışan hesabı / hesap kapatma talebi açma (İK)"),
        new(ContentManage, "Kurumsal site", "Haber, açık pozisyon ve site metinlerini yönetme"),
        new(AdsManage, "Kurumsal site", "Reklam ve banner yönetimi, gösterim/tıklama istatistikleri"),
        new(LeaveRequest, "İzin", "İzin talebi açma, kendi bakiyesini ve taleplerini görme"),
        new(LeaveManage, "İzin", "İzin türleri, hak/bakiye tanımlama, tüm izin taleplerini görme (İK rolünden ayrı)"),
        new(SocialPost, "Sosyal akış", "Paylaşım, yorum ve beğeni"),
        new(SocialModerate, "Sosyal akış", "Uygunsuz içeriği kaldırma, şikayetleri sonuçlandırma"),
        new(FaqManage, "Yardım", "Sık sorulan soruları yönetme"),
        new(TasksManage, "Görevler", "Tüm görevleri görme ve herkese görev atama (yöneticiler kendi ekibine bu izin olmadan atayabilir)"),
        new(WorkflowsManage, "İş akışları", "Onay zincirlerini tanımlama: adımlar, onaylayıcılar, departman yönlendirmesi"),
        new(WidgetsManage, "Portal", "Panel kutucuklarını yönetme: açık/kapalı, sıra, boyut, hangi rolün göreceği"),
    ];

    public static bool IsKnown(string key) => All.Any(p => p.Key == key);
}

public static class RoleNames
{
    public const string SystemAdmin = "system_admin";
    public const string Agent = "agent";
    public const string StoreOwner = "store_owner";
    public const string User = "user";
    public const string Hr = "hr";
    public const string ItAdmin = "it_admin";
    public const string LeaveAdmin = "leave_admin";

    public sealed record Definition(string Name, string DisplayName, string Description, string[] Permissions);

    public static readonly IReadOnlyList<Definition> Defaults =
    [
        new(SystemAdmin, "Sistem Yöneticisi", "Sistemin tamamını yönetir.", Security.Permissions.All.Select(p => p.Key).ToArray()),
        new(Agent, "Destek Personeli", "Talepleri karşılar, çözer ve envanteri yönetir.",
        [
            Security.Permissions.LeaveRequest, Security.Permissions.SocialPost, Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsReadAll, Security.Permissions.TicketsUpdateOwn,
            Security.Permissions.TicketsManage, Security.Permissions.CommentsCreate, Security.Permissions.CommentsInternal,
            Security.Permissions.CategoriesRead, Security.Permissions.StoresRead, Security.Permissions.AssetsReadOwn,
            Security.Permissions.AssetsReadAll, Security.Permissions.AssetsManage, Security.Permissions.UsersRead,
            Security.Permissions.ReportsReadAll, Security.Permissions.MailRead, Security.Permissions.OnCallManage, Security.Permissions.FaqManage,
        ]),
        new(StoreOwner, "Mağaza Sahibi", "Kendi mağazasının taleplerini, cihazlarını ve raporunu görür.",
        [
            Security.Permissions.LeaveRequest, Security.Permissions.SocialPost, Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsReadStore,
            Security.Permissions.TicketsUpdateOwn, Security.Permissions.CommentsCreate, Security.Permissions.CategoriesRead,
            Security.Permissions.StoresRead, Security.Permissions.AssetsReadOwn, Security.Permissions.AssetsReadStore,
            Security.Permissions.UsersRead, Security.Permissions.ReportsReadStore,
        ]),
        new(Hr, "İK Personeli", "Personel kayıtlarını ve çalışan bilgilerini yönetir. Diğer modüllere otomatik erişimi yoktur.",
        [
            Security.Permissions.LeaveRequest, Security.Permissions.SocialPost, Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsUpdateOwn,
            Security.Permissions.CommentsCreate, Security.Permissions.CategoriesRead, Security.Permissions.AssetsReadOwn,
            Security.Permissions.StoresRead,
            Security.Permissions.EmployeesReadPrivate, Security.Permissions.EmployeesManage, Security.Permissions.AccountsRequest,
        ]),
        new(ItAdmin, "BT Hesap Yöneticisi", "Hesap taleplerini inceler, hesap açar/kapatır/kilitler ve sahip olduğu yetkiler kadar rol atar.",
        [
            Security.Permissions.LeaveRequest, Security.Permissions.SocialPost, Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsUpdateOwn,
            Security.Permissions.CommentsCreate, Security.Permissions.CategoriesRead, Security.Permissions.AssetsReadOwn,
            Security.Permissions.StoresRead, Security.Permissions.UsersRead, Security.Permissions.UsersManage, Security.Permissions.AuditRead,
        ]),
        new(LeaveAdmin, "İzin Yöneticisi", "İzin türlerini ve hakları yönetir, izin taleplerinin İK onay adımını verir. İK rolünden ayrıdır; ayrıca atanır.",
        [
            Security.Permissions.LeaveRequest, Security.Permissions.SocialPost, Security.Permissions.LeaveManage,
        ]),
        new(User, "Kullanıcı", "Talep açar ve kendi taleplerini takip eder.",
        [
            Security.Permissions.LeaveRequest, Security.Permissions.SocialPost, Security.Permissions.TicketsCreate, Security.Permissions.TicketsReadOwn, Security.Permissions.TicketsUpdateOwn,
            Security.Permissions.CommentsCreate, Security.Permissions.CategoriesRead, Security.Permissions.AssetsReadOwn,
        ]),
    ];
}
