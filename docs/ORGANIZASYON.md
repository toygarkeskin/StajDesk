# Kimlik ve Organizasyon (Aşama 3)

## 1. Kavramlar: unvan ≠ rol

| Kavram | Nerede tutulur | Ne belirler | Kim değiştirir |
|---|---|---|---|
| **Departman** | `departments` (ağaç: üst departman, yönetici) | Organizasyon yapısı, onay zincirinde "departman yöneticisi" | `org.manage` |
| **Unvan** | `positions` (seviye 1 = en üst) | Kişinin kurumdaki adı: Müdür, Uzman, Stajyer… | `org.manage` (liste), İK (atama) |
| **Yönetici** | `users.manager_id` | Organizasyon şeması, ileride onay zincirleri | İK |
| **Sistem rolü** | `user_roles` → `role_permissions` | Sistemde **ne yapabileceği** | `users.manage` (sistem yöneticisi) |

"Müdür" unvanlı biri sistemde yalnızca "Kullanıcı" rolünde olabilir; yetkiyi unvan değil rol verir.
Promptun örnek hiyerarşisindeki "Super Admin / Admin" birer **rol**dür, unvan listesinde yer almaz.

## 2. Kurumsal hesap süreci (kapalı portal)

StajDesk yalnızca şirket çalışanlarına açıktır: **kimse kendi hesabını açamaz** (kayıt ekranı ve `/api/auth/register` yoktur).

```mermaid
sequenceDiagram
    participant IK as İK (accounts.request)
    participant BT as BT / Yönetici (users.manage)
    participant S as StajDesk
    participant C as Yeni çalışan
    IK->>S: Yeni çalışan hesabı talebi (ad, personel kodu, departman, unvan, personel türü, kurumsal e-posta, işe başlama)
    S-->>BT: Uygulama içi bildirim
    BT->>S: İnceler → kullanıcı adı + sistem rolleri → Onayla (veya gerekçeyle Reddet)
    S->>S: Hesap açılır (şifresiz), personel kartı talepten doldurulur
    S-->>C: "İlk şifreni belirle" e-postası (72 saat, tek kullanımlık)
    C->>S: Şifresini belirler → kullanıcı adı veya e-postayla giriş
    S-->>IK: "Talep onaylandı" bildirimi
```

| Rol | Yapabilir | Yapamaz |
|---|---|---|
| **İK Personeli** (`hr`) | Personel bilgilerini düzenlemek; yeni hesap ve hesap kapatma **talebi** açmak | Hesap açmak/kapatmak/kilitlemek, rol vermek, talep onaylamak |
| **BT Hesap Yöneticisi** (`it_admin`) | Talepleri onaylamak/reddetmek; hesap açmak, pasife almak, kilitlemek, davet yenilemek; **kendi sahip olduğu yetkiler kadar** rol vermek | Sistem Yöneticisi / İK / Destek gibi kendisinde olmayan yetkileri içeren rolleri vermek; kendinden yetkili hesabı değiştirmek |
| **Sistem Yöneticisi** | Hepsi + rol/izin matrisi | — |
| **Çalışan** | Kendisine açılan hesapla giriş, kendi şifresi ve kişisel bilgileri | Hesap açmak, başkasına yetki vermek |

Güvenlik kuralları (hepsi API'de uygulanır, arayüz yalnızca yansıtır):
- **Yetki yükseltme koruması:** bir rolü atayabilmek için o rolün tüm izinlerine sahip olmak gerekir; işlemi yapandan daha yetkili hesap değiştirilemez.
- **Görevler ayrılığı:** BT kendi açtığı talebi onaylayamaz (yalnızca rol/izin yöneticisi istisna).
- **Şifreyi kimse bilmez:** hesap şifresiz açılır, çalışan davet bağlantısıyla belirler.
- **Ayrılan personel:** İK ayrılış tarihi girince BT'ye otomatik **hesap kapatma talebi** açılır; BT onaylayınca hesap pasife alınır ve açık oturumlar kapanır.
- **Kilitleme:** BT bir hesabı gerekçeyle kilitleyebilir; şifre doğru olsa bile giriş yapılamaz (`account_locked`), mevcut oturumlar o an kapanır.
- **Kayıt:** talep açma, geri çekme, onay, ret, hesap açma, pasife alma, kilitleme/kilit açma, davet yenileme denetim kaydına yazılır.

## 3. Kim neyi görür?

| Alan | Herkes | Kişinin kendisi | İK (`employees.read.private`) |
|---|---|---|---|
| Ad, unvan, departman, yönetici, iş e-postası, telefon, işe başlama, kıdem | ✔ | ✔ | ✔ |
| Doğum günü (gün-ay) | ✔ | ✔ | ✔ |
| Doğum tarihi (yıl dahil), adres, kişisel e-posta, acil durum kişisi, personel no | | ✔ | ✔ |
| Kullanıcı adı, son giriş, sistem rolleri | | ✔ | ✔ |

Yetkisiz kişiye özel alanlar API'den **hiç dönmez** (`private: null`); arayüzde gizlemek yeterli sayılmaz.
Kişisel bilgi değişiklikleri denetim kaydına yazılır ama **değerleri** yazılmaz.

## 4. Hesap akışları

| Akış | Nasıl çalışır |
|---|---|
| Giriş | Kullanıcı adı **veya** e-posta + şifre (`@` içeriyorsa e-posta) |
| Hesap açılışı | Yalnızca BT / yönetici (İK talebi üzerine) → "ilk şifreni belirle" daveti (72 saat, tek kullanımlık) → şifre belirlenince e-posta doğrulanmış sayılır |
| Kilitli hesap | Şifre doğruysa `account_locked` döner (yanlış şifrede kilit bilgisi sızdırılmaz) |
| Şifre değiştirme | Profil → Hesap ve güvenlik (mevcut şifre gerekir) |
| Şifre sıfırlama | Giriş ekranı → Şifremi unuttum (30 dk) |
| SMS ile iki adımlı doğrulama | Aşağıya bakın |

Tüm bağlantı türleri tek tabloda (`password_reset_tokens.purpose`) yalnızca SHA-256 özetiyle saklanır;
bir türün bağlantısı başka iş için kullanılamaz.

Bu sürümden önce açılmış hesaplar migration'da doğrulanmış sayıldı; mevcut kullanıcıların girişi etkilenmedi.

## 5. SMS ile iki adımlı doğrulama

- Kod: 6 hane, 5 dakika, en fazla 5 deneme; veritabanında yalnızca özet.
- Kullanıcı açmak için telefonuna gelen kodu doğrular (telefon sahipliği kanıtlanır); kapatmak için şifresini girer.
- Giriş: şifre doğruysa API yetki kodu yerine `{ otpRequired: true, challengeId, maskedPhone }` döner;
  `POST /connect/authorize/otp` ile kod doğrulanınca yetki kodu verilir.

**SMS sağlayıcısı:** şu an **bağlı değil** (`NotConfiguredSmsSender`). Bu durumda:
- Profilde "SMS servisi yapılandırılmadı" yazar, iki adımlı doğrulama açılamaz.
- Hiçbir ekranda "SMS gönderildi" denmez.

Sağlayıcı bağlamak için `ISmsSender`'ı uygulayan bir sınıf (ör. Netgsm, İleti Merkezi HTTP API'si) yazılıp
`StajDesk.Infrastructure/DependencyInjection.cs` içinde `NotConfiguredSmsSender` yerine kaydedilir; API bilgileri `Sms__*` ortam değişkenlerinden okunur.

## 6. Veri modeli

```mermaid
erDiagram
    DEPARTMENTS ||--o{ DEPARTMENTS : "üst departman"
    USERS |o--o{ DEPARTMENTS : "yönetir"
    DEPARTMENTS ||--o{ USERS : "çalışır"
    POSITIONS ||--o{ USERS : "unvan"
    USERS ||--o{ USERS : "yönetici"
    USERS ||--|| EMPLOYEE_PROFILES : "personel kartı"
    USERS ||--o| USER_SETTINGS : "tercihler"
    USERS ||--o{ OTP_CHALLENGES : "SMS kodu"
```

Kısıtlar: departman kodu ve unvan adı tekil; personel no tekil (boş olabilir); kullanıcı adı tekil (boş olabilir);
departman kendisinin üstü olamaz, kullanıcı kendisinin yöneticisi olamaz (CHECK) ve zincirde döngü oluşturamaz (uygulama kuralı);
çıkış tarihi işe girişten önce olamaz (CHECK); alt departmanı veya çalışanı olan departman, kullanılan unvan silinemez.

Migration, eski serbest metin `users.department` değerlerini `departments` tablosuna taşıyıp kullanıcıları bağladı; veri kaybolmadı.

## 7. API

| Metot | Yol | İzin |
|---|---|---|
| GET | `/api/employees?search=&departmentId=&includeInactive=` | Oturum (pasifleri yalnızca İK) |
| GET | `/api/employees/{id}` | Oturum (alan bazlı gizlilik) |
| PUT | `/api/employees/{id}/personal` | Kendisi veya `employees.manage` |
| PUT | `/api/employees/{id}/work` | `employees.manage` |
| GET / PUT / DELETE | `/api/employees/{id}/photo` · `/cover` | Okuma: oturum · Değişiklik: kendisi veya İK (PNG/JPG/WEBP, 2 MB, imza kontrolü) |
| GET | `/api/org/chart`, `/api/org/departments`, `/api/org/positions` | Oturum |
| POST / PUT / DELETE | `/api/org/departments`, `/api/org/positions` | `org.manage` |
| GET / PUT | `/api/me/settings` | Oturum |
| PUT | `/api/me/password` | Oturum |
| GET | `/api/me/security` | Oturum |
| POST | `/api/me/2fa/start` · `/confirm` · `/disable` | Oturum |
| GET | `/api/account-requests?status=` | `accounts.request` / `users.manage` |
| POST | `/api/account-requests/new-account` · `/deactivation` | `accounts.request` / `users.manage` |
| POST | `/api/account-requests/{id}/approve` · `/reject` | `users.manage` |
| POST | `/api/account-requests/{id}/cancel` | Talebi açan |
| POST | `/api/users/{id}/lock` · `/unlock` · `/invite` | `users.manage` |
| POST | `/connect/authorize/otp` | Anonim (hız sınırlı) |

## 8. Ekranlar

| Ekran | Yol |
|---|---|
| Çalışanlar (rehber; İK için "Yeni çalışan hesabı talebi") | `/employees` |
| Hesap talepleri (İK açar, BT onaylar/reddeder) | `/account-requests` |
| Kullanıcı hesapları (BT: rol, pasif, kilit, davet) | `/admin/users` |
| Profil: Genel bakış · Kişisel bilgiler · İş bilgileri · Hesap ve güvenlik · Bildirim tercihleri | `/employees/{id}`, `/profile` |
| Organizasyon şeması (departman ağacı + yönetici ağacı) | `/org` |
| Departmanlar ve unvanlar | `/admin/org` |
| İlk şifre belirleme | `/set-password` |

## 9. Bu aşamada bilerek yapılmayanlar

| Konu | Neden / ne zaman |
|---|---|
| İzin bakiyesi sekmesi | İzin modülü Aşama 6'da; boş sekme gösterilmedi |
| Dil seçimi (TR/EN) | Ayar kaydı backend'de hazır; arayüz çevirisi Aşama 5'te. Çalışmayan bir seçenek gösterilmedi |
| Duyuru e-postası tercihi | Duyurular henüz e-posta göndermiyor; işlevsiz anahtar gösterilmedi |
| Gerçek SMS sağlayıcısı | Hesap bilgisi gerekiyor (bkz. §5) |

## 10. Bekleyen

- **Departman ve unvan listesi:** gönderileceği belirtilen CSV dosyası henüz ulaşmadı. Gelince bu listeler CSV'den yüklenecek (seed + mevcut kayıtlarla eşleştirme). O zamana kadar demo departmanları ve 15 varsayılan unvan kullanılıyor.
