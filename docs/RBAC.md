# Roller ve İzinler (RBAC)

## Model

```
users ──< user_roles >── roles ──< role_permissions >── permissions
```

- **İzin**, tek bir işi anlatan küçük bir anahtardır: `tickets.read.store`.
- **Rol**, izin kümesidir. Bir kullanıcının birden fazla rolü olabilir; izinleri birleşir.
- Kod **rol adına değil izne** bakar. Yeni bir rol ("Bölge Müdürü" gibi) eklemek veya bir rolün yetkisini değiştirmek kod değişikliği istemez.
- İzin listesi kodda tanımlıdır (`StajDesk.Domain/Security/Permissions.cs`) ve her açılışta veritabanına senkronlanır.
- Varsayılan rol matrisi ilk kurulumda yazılır. Sonrasında **Yönetim → Roller ve İzinler** ekranından değiştirilir.
- `system_admin` rolü her zaman tüm izinlere sahiptir ve ekrandan kısıtlanamaz (kendini kilitleme riskine karşı).

## Varsayılan matris

| İzin | Sistem Yöneticisi | Destek Personeli | Mağaza Sahibi | Kullanıcı |
|---|:-:|:-:|:-:|:-:|
| `tickets.create` — talep oluşturma | ✔ | ✔ | ✔ | ✔ |
| `tickets.read.own` — kendi talepleri | ✔ | ✔ | ✔ | ✔ |
| `tickets.read.store` — mağazanın talepleri | ✔ | | ✔ | |
| `tickets.read.all` — tüm talepler | ✔ | ✔ | | |
| `tickets.update.own` — açık talebini düzenleme | ✔ | ✔ | ✔ | ✔ |
| `tickets.manage` — durum, öncelik, atama | ✔ | ✔ | | |
| `tickets.delete` — talep silme | ✔ | | | |
| `comments.create` — yanıt yazma | ✔ | ✔ | ✔ | ✔ |
| `comments.internal` — dahili not | ✔ | ✔ | | |
| `categories.read` | ✔ | ✔ | ✔ | ✔ |
| `categories.manage` | ✔ | | | |
| `stores.read` | ✔ | ✔ | ✔ | |
| `stores.manage` | ✔ | | | |
| `assets.read.own` — zimmetli cihazlar | ✔ | ✔ | ✔ | ✔ |
| `assets.read.store` — mağaza cihazları | ✔ | | ✔ | |
| `assets.read.all` — tüm envanter | ✔ | ✔ | | |
| `assets.manage` | ✔ | ✔ | | |
| `users.read` | ✔ | ✔ | ✔ | |
| `users.manage` | ✔ | | | |
| `roles.manage` | ✔ | | | |
| `reports.read.store` — mağaza özet paneli | ✔ | | ✔ | |
| `reports.read.all` — genel özet paneli | ✔ | ✔ | | |
| `audit.read` — denetim kayıtları | ✔ | | | |
| `mail.read` — gönderilen e-postalar | ✔ | ✔ | | |
| `announcements.manage` — duyuru yayınlama | ✔ | | | |
| `oncall.manage` — nöbet çizelgesi | ✔ | ✔ | | |
| `employees.read.private` — kişisel bilgileri görme | ✔ | | | |
| `employees.manage` — personel kaydı ve iş bilgileri (İK) | ✔ | | | |
| `org.manage` — departman ve unvan yönetimi | ✔ | | | |

**İK Personeli (`hr`)** rolü: `employees.read.private`, `employees.manage` + kendi taleplerini açma/izleme.
Sistem rolü veremez, departman ağacını değiştiremez, talepleri yönetemez. Ayrıntı: [ORGANIZASYON.md](ORGANIZASYON.md).

Sürüm güncellemesiyle gelen yeni bir izin, varsayılanında o izni içeren rollere açılışta bir kez eklenir.
Yöneticinin ekrandan kaldırdığı eski izinlere dokunulmaz.

## Veri kapsamı (kim hangi satırı görür?)

İzin yalnızca "bu işi yapabilir mi" sorusunu cevaplamaz; hangi kayıtları göreceğini de belirler.

| Kaynak | `*.read.all` | `*.read.store` | `*.read.own` |
|---|---|---|---|
| Talepler | Hepsi | Kullanıcının mağazasına ait talepler + kendi talepleri | Yalnızca açtığı talepler |
| Cihazlar | Hepsi | Mağazasındaki cihazlar + kendisine zimmetliler | Yalnızca kendisine zimmetliler |
| Özet paneli | `scope: all` | `scope: store` | `scope: user` |
| Kullanıcılar (`users.read`) | Yönetici/destek: hepsi | Mağaza sahibi: yalnızca kendi mağazası | — |

Kapsam dışındaki bir kayda erişim **403** döner; listelerde ise bu kayıtlar hiç gösterilmez.

## Uygulama noktaları

1. **`PermissionLoadingMiddleware`** — her istekte izinleri Redis'ten (`perms:user:{id}`, 5 dk) ya da DB'den yükler. Pasif kullanıcıya 401 döner.
2. **`[RequirePermission(...)]`** — controller seviyesinde kaba kontrol. Birden fazla izin verilirse "herhangi biri yeterli".
3. **`ICurrentUser.Require(...)` ve `TicketAccess`** — handler/servis içinde ince kontrol: kayıt sahipliği, mağaza eşleşmesi, durum kuralları.
4. **Arayüz** — `useCan()` ile menü ve butonlar gizlenir. Bu yalnızca deneyim içindir; güvenlik API'dedir.

Rol veya kullanıcı değiştiğinde ilgili `perms:*` önbelleği silinir; değişiklik bir sonraki istekte etkili olur.

## Yeni rol ekleme

Ekrandan: **Roller ve İzinler** sayfasında matrisi düzenleyin. Kalıcı varsayılan olarak eklemek için
[playbooks/add-permission.md](playbooks/add-permission.md) rehberine bakın.
