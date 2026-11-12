# StajDesk Kurumsal Portal — Aşama 1: Analiz Raporu

Tarih: 12.11.2026 · Kapsam: "StajDesk — Kapsamlı Kurumsal Portal ve İş Yönetim Sistemi" geliştirme promptu

## 1. İncelenen kaynaklar

| Kaynak | Durum |
|---|---|
| Devoplus Stajyer Programı PDF (22 sayfa) | İncelendi — destek talep sistemi + 16 haftalık plan + bonus görevler |
| Kurumsal portal geliştirme promptu (24 bölüm) | İncelendi |
| Koyu temalı portal ekran görüntüleri (14 adet: panel, profil, kenar menü ve alt menüler) | İncelendi |
| **Açık temalı karşılama sitesi ekran görüntüsü** | **Gelmedi** — Aşama 4 tasarımı için gerekli |
| **Ek gereksinim TXT dosyası** | **Gelmedi** — içeriği bilinmiyor |
| Mevcut kaynak kod (commit `b465df2`) | İncelendi |

## 2. Mevcut durum → promptun karşılığı

Proje sıfırdan başlamıyor. Promptun 2., 3. ve 6. aşamalarının önemli bir kısmı hazır, test edilmiş ve çalışıyor.

| Prompt maddesi | Mevcut durum |
|---|---|
| Docker Compose ile tek komut | ✔ postgres, redis, rabbitmq, seq, mailpit, api, worker, web |
| .NET 10, EF Core, PostgreSQL, migration | ✔ Clean Architecture, 2 migration, 23 tablo |
| Redis, RabbitMQ, Seq | ✔ önbellek, hız sınırı, outbox, Worker, yapısal log |
| JWT + refresh token + oturum güvenliği | ✔ OAuth 2.0 + PKCE, RS256, refresh rotasyonu, iptal |
| Rate limiting, aktif/pasif hesap | ✔ e-posta + IP bazlı, pasif hesap anında engellenir |
| Şifremi unuttum | ✔ e-posta ile (Mailpit) |
| RBAC (API seviyesinde) | ✔ 26 izin, 4 rol, ekrandan düzenlenebilir matris; veri kapsamı own/store/all |
| Destek talep sistemi (PDF'in ana işlevi) | ✔ CRUD, atama, yorum, dahili not, geçmiş, dosya eki, tam metin arama, bildirim |
| Duyurular | ✔ temel (hedef mağaza, önem, yayın/bitiş) — görsel, ek, departman hedefi, okundu bilgisi **yok** |
| Nöbetçi personel | ✔ |
| Mağazalar, envanter | ✔ |
| Bildirim merkezi + anlık bildirim | ✔ SignalR |
| Widget tabanlı koyu panel | ✔ sabit düzen — yönetilebilir widget sistemi **yok** |
| Çalışan arama | ✔ basit (yetkili kullanıcılar) |
| Audit log | ✔ |
| Testler | ✔ 51 birim testi + 33 uçtan uca kontrol |

## 3. Gereksinim envanteri (eksik olanlar)

**K** = karşılama sitesi · **P** = portal · **Y** = yönetim paneli · **D** = departman uygulaması

| # | Alan | Gereksinim | Büyüklük |
|---|---|---|---|
| 1 | K | Herkese açık kurumsal site: üst menü, banner, haberler, departmanlar, kariyer, iletişim, footer | Orta |
| 2 | K/Y | Reklam ve banner yönetimi: görsel, konum, sıralama, tarih aralığı, gösterim/tıklama/oran | Orta |
| 3 | K/P | Canlı hava durumu, USD/TRY, EUR/TRY, gram altın (backend üzerinden, önbellekli, kaynak ve zaman bilgisiyle) | Orta |
| 4 | P | Kullanıcı adı ile giriş, ilk parola oluşturma, SMS OTP, e-posta doğrulama | Orta |
| 5 | P | Organizasyon: departman, pozisyon/unvan, yönetici ilişkisi, organizasyon şeması (unvan ≠ sistem rolü) | Büyük |
| 6 | P | Çalışan profili: fotoğraf, kapak, kişisel bilgiler, iş bilgileri, acil durum kişisi, bildirim tercihleri | Orta |
| 7 | P | İzin türleri, izin bakiyesi, izin talebi + onay | Orta |
| 8 | P | İş akışı motoru: tanımlanabilir adımlar, onaylayıcı, departman yönlendirme, onay/ret, geçmiş | Büyük |
| 9 | P | Widget yönetimi: rol bazlı görünürlük, sıralama, boyut, kullanıcı kişiselleştirmesi | Orta |
| 10 | P | Global arama (ekran, kişi, departman, duyuru, uygulama — yetkiye göre) | Orta |
| 11 | P | Açılır/kapanır alt menülü kenar çubuğu, dil seçimi (TR/EN), yardım | Küçük |
| 12 | P | Sosyal akış (paylaşım, yorum, beğeni, moderasyon) | Orta |
| 13 | P | Mesajlaşma | Büyük |
| 14 | P | Görev yönetimi, belgeler, anketler, etkinlikler, başarılar, haftanın çalışanı, doğum günü, iş yıldönümü, açık pozisyonlar, SSS | Orta (her biri küçük) |
| 15 | P | Mağaza ciro yönetimi (günlük/haftalık/aylık, şampiyonlar) | Orta |
| 16 | P | Fiş tarama | Büyük (OCR) |
| 17 | P | İş gücü yönetimi (vardiya, vardiya raporu, düzeltme talepleri, resmî tatil, dönem kapanışı) | Büyük |
| 18 | P | İş avansı, masraf beyanı, masraf limitleri | Orta |
| 19 | D | 14 departman grubu, ~50 alt uygulama (BT, İK, Kalite, Lojistik, Muhasebe, Pazarlama, Satın Alma, …) | Çok büyük |
| 20 | Y | Ayrı yönetim paneli: departman, organizasyon şeması, banner, widget, uygulama, sistem ayarları, API entegrasyonları, bildirim ayarları | Büyük |
| 21 | — | Talep durumlarına "Yeni" ve "Reddedildi" eklenmesi, ilgili departman alanı | Küçük |

## 4. Çelişkiler ve riskler (karar gerektirenler kalın)

1. **Frontend teknolojisi.** Promptun 16. bölümü Vue 3 + Pinia diyor; aynı bölüm "çalışan altyapıyı sebepsiz değiştirme" diyor.
   Sen de daha önce React + Vite + Tailwind'i kesin istek olarak belirttin. Mevcut arayüz React ile çalışıyor ve test edildi.
   **Öneri: React'te kalmak.** Vue'ya geçiş ~40 ekranı yeniden yazmak demek ve hiçbir yeni işlev kazandırmaz.

2. **Herkese açık sitede kişisel veri.** Karşılama sitesinde "doğum günleri", "çalışanlarımız", "haftanın çalışanı" isteniyor.
   Giriş yapmamış herkese çalışan adı, fotoğrafı ve doğum tarihi göstermek KVKK açısından risklidir (açık rıza gerekir).
   **Öneri:** Giriş öncesi sitede yalnızca kurumsal içerik (banner, haber, duyuru, departman tanıtımı, açık pozisyon, istatistik, hava/döviz);
   kişisel içerikler giriş sonrası portalda.

3. **~50 departman uygulaması.** "Her modülün listeleme, detay, oluşturma, düzenleme, onay ekranı olmalı; boş menü bırakma" deniyor.
   Her birini ayrı elle yazmak aylar sürer ve çoğu aynı kalıptır (form → onay zinciri → liste → rapor).
   **Öneri:** Yapılandırılabilir **Form + İş Akışı motoru**. Yönetici form alanlarını ve onay adımlarını tanımlar; her başvuru
   gerçek kayıt olarak saklanır, onaylanır, raporlanır, dışa aktarılır. Menüdeki her uygulama (Donanım Talebi, Eğitim Talebi,
   Araç Talebi…) bu motorda tanımlı bir form türü olur. Kendine özgü hesaplaması olanlar (izin bakiyesi, ciro, vardiya, masraf limiti)
   ayrı modül olarak yazılır.

4. **Canlı veri kaynakları ve lisans.**
   - Hava durumu: Open-Meteo ücretsiz ve anahtarsız ama **yalnızca ticari olmayan kullanım** içindir; şirket içi portal ticari sayılabilir.
     Ticari kullanım için Open-Meteo'nun ücretli planı veya OpenWeatherMap anahtarı gerekir.
   - USD/EUR: TCMB'nin günlük kur dosyası (today.xml) resmî ve ücretsizdir, iş günleri 15:30'da güncellenir.
   - **Gram altın: ücretsiz ve resmî bir kaynak yok** (TCMB altın yayınlamıyor). Anahtarlı bir sağlayıcı gerekir;
     anahtar verilmezse kart "kaynak yapılandırılmadı" der, sahte rakam göstermez.

5. **SMS OTP.** Gerçek SMS için sağlayıcı (Netgsm, İleti Merkezi vb.) hesabı ve API bilgisi gerekir. Prompt "sahte SMS gönderilmiş
   gibi davranma" diyor. Plan: sağlayıcı arayüzü + yapılandırma; bilgiler girilene kadar SMS adımı kapalı ve ekranda bu söylenir.
   Demo ortamında yalnızca açıkça "Demo SMS kutusu" etiketli bir ekran (Mailpit benzeri) seçenek olarak sunulabilir.

6. **Yapay zekâ kullanım kartı.** Promptun kendi kuralına göre gerçek bir yapay zekâ entegrasyonu olmadığı için gösterilmeyecek.

7. **Fiş tarama (OCR).** Gerçek okuma için Tesseract konteyneri gerekir (Türkçe fişlerde doğruluk orta). İlk sürüm: fiş fotoğrafı
   yükleme + elle tutar/tarih/firma girişi + masraf beyanına bağlama; OCR sonraki adım.

8. **Talep durumları.** PDF: Açık → İşlemde → Çözüldü → Kapandı (+ projede Beklemede). Prompt "Yeni" ve "Reddedildi" ekliyor.
   Öneri: "Reddedildi" yeni durum olarak eklenir. "Yeni", "Açık + atanmamış" ile aynı anlamda olduğu için ayrı durum yerine
   etiket olarak gösterilir (iş kuralları ve mevcut veriler bozulmaz).

9. **LOG.md.** PDF'te LOG.md stajyerin kendi öğrenme günlüğü (mentor değerlendirmesinde %15 ağırlık). Prompt buraya geliştirme
   kaydı yazılmasını istiyor. Günlüğü kirletmemek için geliştirme kaydı `docs/GELISTIRME_LOG.md` dosyasına yazılır.

10. **Marka.** Referans görüntüler Süvari portalına ait. Düzen ve işlevler örnek alınır; ad, logo ve renkler StajDesk'e aittir.

11. **Kullanıcı yönetimi + İK.** Prompt "İK rolü tüm modüllere erişim demek değildir" diyor. Mevcut izin modeli buna uygun:
    personel bilgisi yönetimi (`employees.manage`) ile izin/masraf onayı ayrı izinler olacak.

12. **Ölçek.** Promptun tamamı tek seferde bitirilemez. Aşağıdaki plan aşama aşama, her aşama sonunda çalışan ve test edilmiş
    kodla ilerler; her aşamanın sonunda tamamlananlar, değişen dosyalar, test sonuçları ve eksikler raporlanır.

## 5. Hedef mimari

Mevcut modüler monolit korunur; yeni alanlar ayrı **modüller** olarak eklenir (Domain + Application özellik klasörü + API controller + React sayfaları).

```
Kimlik & Organizasyon   kullanıcı, profil, departman, pozisyon, yönetici, org şeması, roller, izinler
Portal Çekirdeği        widget kataloğu, kullanıcı panel tercihleri, global arama, bildirim, menü/uygulama kataloğu
İçerik                  duyuru (+okundu), haber, banner/reklam (+istatistik), etkinlik, başarı, açık pozisyon, SSS, sosyal akış
Form & İş Akışı Motoru  form tanımı (JSON şema), başvuru, iş akışı şablonu, adım, onay kaydı, dışa aktarma
Destek                  mevcut talep sistemi (+departman, +Reddedildi)
İK                      izin türü, bakiye, izin talebi (motor üzerinden onay), personel kartı
Finans                  iş avansı, masraf beyanı, masraf limiti, fiş
Mağaza                  mağaza (mevcut), ciro bildirimi, ciro raporları
İş Gücü                 vardiya planı, vardiya kaydı, düzeltme talebi, resmî tatil, dönem kapanışı
Entegrasyonlar          hava, kur, altın, SMS — sağlayıcı arayüzü + Redis önbelleği + "son başarılı veri"
```

Karşılama sitesi ve portal **aynı React uygulamasında ayrı düzenlerle** çalışır:
`/` (herkese açık site, açık tema) ve `/portal/*` (giriş sonrası koyu portal). Aynı API, aynı kullanıcı sistemi.

## 6. Veri modeli taslağı (yeni tablolar)

| Modül | Tablolar |
|---|---|
| Organizasyon | `departments` (üst departman, yönetici), `positions` (unvan, seviye), `employee_profiles` (personel no, unvan, departman, yönetici, işe giriş/çıkış, çalışma türü, telefon, adres, acil kişi, doğum tarihi, fotoğraf, kapak), `user_settings` (dil, tema, bildirim tercihleri) |
| Portal | `widgets` (katalog), `role_widgets` (rol × widget, sıra, boyut), `user_dashboard_preferences`, `portal_apps` (menü kataloğu, izin anahtarı, üst menü) |
| İçerik | `announcement_reads`, `announcement_targets` (departman/rol), `news`, `banners`, `ad_impressions` (günlük toplam), `events`, `achievements`, `job_openings`, `faqs`, `social_posts`, `social_comments`, `social_likes` |
| Form & akış | `form_definitions` (sürümlü JSON alan şeması), `workflow_templates`, `workflow_steps` (onaylayıcı türü: kişi / rol / departman yöneticisi / talep sahibinin yöneticisi), `submissions` (JSON veri + durum), `submission_approvals`, `submission_attachments` |
| İK | `leave_types`, `leave_balances` (yıl × tür), `leave_requests` |
| Finans | `advance_requests`, `expense_claims`, `expense_lines`, `expense_limits`, `receipts` |
| Mağaza | `store_revenues` (gün × mağaza: ciro, adet, sepet) |
| İş gücü | `shift_templates`, `shifts`, `shift_corrections`, `public_holidays`, `period_closings` |
| Entegrasyon | `external_snapshots` (kaynak, anahtar, veri, alınma zamanı — son başarılı değer), `sms_messages` |
| Kimlik | `users.username`, `otp_codes` (hash'li, süreli), `email_verifications` |

Kurallar: tüm FK'ler açık silme davranışıyla, sık filtrelenen sütunlara index, iş anahtarlarına unique (personel no, departman kodu),
form verisi JSONB + raporlanan alanlar için GIN index.

## 7. Aşama planı

| Aşama | İçerik | Kabul ölçütü |
|---|---|---|
| 1 Analiz | Bu doküman | Kararlar alındı |
| 2 Altyapı | ✔ Hazır. Eklenecek: dış servis istemcileri için Polly yeniden deneme/zaman aşımı, MinIO gerekirse | — |
| 3 Kimlik & organizasyon | Departman, pozisyon, yönetici, profil, kullanıcı adı ile giriş, ilk parola, e-posta doğrulama, SMS sağlayıcı arayüzü, İK izinleri, org şeması | Unvan/rol ayrımı testli; İK yalnızca personel bilgisini yönetir |
| 4 Karşılama sitesi | Açık tema site, banner/reklam yönetimi + istatistik, haber, kariyer, iletişim, hava/kur/altın servisleri | Süresi dolan reklam yayından kalkar; veri kaynağı ve zamanı görünür |
| 5 Portal çekirdeği | Alt menülü kenar çubuğu, widget sistemi (rol + kişisel), global arama, profil sayfası, dil seçimi | Kullanıcı yetkisi olmayan widget/uygulamayı göremez (API testi) |
| 6 Temel iş uygulamaları | Form & iş akışı motoru, izin sistemi, görevler, duyuru genişletme, sosyal akış, talep sistemi uyarlaması | Onay zinciri uçtan uca (talep → yönetici → müdür → departman) |
| 7 Departman uygulamaları | Motor üzerinde ~50 form türü + özel modüller (ciro, masraf/avans, vardiya, fiş) | Her menü öğesi gerçek kayıt oluşturur ve onaylanır |
| 8 Yönetim paneli | Ayrı yönetim alanı: departman, org şeması, banner, widget, uygulama kataloğu, ayarlar, entegrasyonlar | Yönetici olmayan admin API'lerine erişemez |
| 9 Test & iyileştirme | Güvenlik, responsive, performans, dokümantasyon | Tüm testler yeşil, docs güncel |

Her aşama ayrı commit/push, `docs/GELISTIRME_LOG.md` kaydı ve aşama raporu ile kapanır.
