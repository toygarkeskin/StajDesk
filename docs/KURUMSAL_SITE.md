# Kurumsal site (Aşama 4)

Giriş öncesi herkese açık tanıtım sitesi ile portal içindeki içerik yönetimi. İki alan aynı backend'i, veritabanını ve kullanıcı sistemini kullanır.

## 1. Adresler

| Adres | Sayfa |
|---|---|
| `/` | Ana sayfa: dönen banner, istatistikler, duyurular, haberler, departmanlar, hava durumu, döviz, gram altın, açık pozisyonlar, yan reklam, kurumsal bağlantılar |
| `/hakkimizda` | Hakkımızda, misyon, vizyon |
| `/departmanlarimiz` | Sitede tanıtılan departmanlar ve alt birimleri |
| `/uygulamalar` | Portalda **gerçekten çalışan** uygulamalar (henüz yapılmamış modüller listelenmez) |
| `/duyurular` | Herkese açık duyurular |
| `/haberler`, `/haberler/:slug` | Haber listesi (sayfalı) ve detay |
| `/kariyer`, `/kariyer/:id` | Açık pozisyonlar (aranabilir) ve ilan detayı, e-posta ile başvuru |
| `/iletisim` | İletişim bilgileri |
| `/portal` | Çalışan portalı. Oturum yoksa giriş ekranına yönlendirir |
| `/admin/content` | İçerik yönetimi (portal içinde, yetkiye bağlı) |

Site her zaman açık temadadır, üst menü lacivert. Mobilde menü hamburger çekmeceye dönüşür. Portalın koyu tema tercihi siteye taşınmaz; portala dönülünce kullanıcının tercihi geri gelir.

## 2. Kişisel veri kararı (KVKK)

Herkese açık sitede çalışan adı, doğum günü, fotoğraf, "haftanın çalışanı" gibi **kişisel veri gösterilmez** (analizde alınan karar, [PORTAL_ANALIZ.md](PORTAL_ANALIZ.md) §8). Bu alanlar giriş sonrası portalda (Aşama 5) yer alır. `/api/public/*` uçları yalnızca kurumsal içerik ve sayısal özet (çalışan / mağaza / departman / şehir sayısı) döner; smoke testi yanıtta e-posta alanı olmadığını denetler.

## 3. Reklam ve banner yönetimi

**Ekran:** Portal → Yönetim → **Kurumsal site** → Reklam ve bannerlar. Gereken izin: `ads.manage`.

| Alan | Açıklama |
|---|---|
| Görsel | PNG / JPG / WEBP, en fazla 3 MB. Dosyanın içeriği (imza baytları) kontrol edilir. Önerilen oran 3:1 |
| Başlık, açıklama | Düz metin. HTML reddedilir |
| Tıklama bağlantısı | İsteğe bağlı. Yalnızca `http://` veya `https://`. `javascript:`, `data:` ve göreli adresler reddedilir |
| Sayfa | Kurumsal site veya Portal (portal panelinde gösterilir) |
| Konum | Üst banner (dönen), Yan sütun, İçerik arası |
| Başlangıç / bitiş | Bitiş boşsa süresiz. **Süresi dolan reklam sorguda elenir**, ayrıca bir zamanlanmış iş gerekmez |
| Aktif | Pasif reklam tarih aralığından bağımsız olarak gösterilmez |
| Sıra | Küçük sayı önce gösterilir |

**Durumlar:** Yayında, Zamanlandı (başlangıç ileride), Süresi doldu, Pasif.

**İstatistikler**
- *Gösterim:* reklam ekranda en az yarısı görünür olduğunda, sayfa oturumu başına bir kez sayılır. Gösterimler toplu gönderilir (istek başına en fazla 10 reklam). Yalnızca o an yayında olan reklamın gösterimi sayılır, böylece sahte sayım engellenir.
- *Tıklama:* bağlantı `/api/public/ads/{id}/click` adresine gider. Sunucu tıklamayı sayar ve kayıtlı adrese 302 ile yönlendirir. Hedef adres istemciye hiç verilmez; açık yönlendirme (open redirect) olmaz.
- *Tıklama oranı:* tıklama / gösterim × 100.
- Günlük kırılım `ad_stats_daily` tablosunda tutulur; ekranda son 30 gün gösterilir.

## 4. Haber, açık pozisyon, site metinleri

Gereken izin: `content.manage`.

- **Haber:** başlıktan Türkçe karakterleri sadeleştirilmiş tekil adres üretilir ("Yeni Mağazamız Açıldı" → `yeni-magazamiz-acildi`, çakışırsa `-2`). Taslak ve ileri tarihli haber herkese açık değildir (404).
- **Açık pozisyon:** departman, yer, çalışma türü, açıklama, nitelikler (her satır bir madde), son başvuru tarihi. Son başvuru geçince ilan otomatik gizlenir.
- **Site metinleri:** şirket adı, tanıtım cümlesi, hakkımızda, misyon, vizyon, iletişim, kariyer e-postası, hava durumu varsayılan şehri ve kurumsal bağlantılar.
- Değişiklikler önbellek (`site:*`) temizlenerek en geç 2 dakika içinde sitede görünür. Tüm yazma işlemleri denetim kaydı bırakır.

## 5. Canlı veri: hava durumu, döviz, gram altın

Tüm çağrılar **sunucudan** yapılır. Tarayıcıya anahtar veya dış adres verilmez; CSP `connect-src 'self'` olarak kalır.

| Veri | Kaynak | Önbellek | Not |
|---|---|---|---|
| Hava durumu (anlık, nem, rüzgâr, 4 günlük tahmin) | Open-Meteo (anahtar gerektirmez) | 15 dk | 14 il seçilebilir; varsayılan şehir site ayarından gelir. Ziyaretçinin şehir seçimi kendi tarayıcısında saklanır |
| USD, EUR, GBP alış/satış | TCMB `today.xml` | 30 dk | İş günleri 15:30'da güncellenir; bülten tarihi gösterilir |
| Gram altın | **Yapılandırılmadı** | — | Ücretsiz ve resmî bir kaynak yok. Sahte sayı gösterilmez; kartta "Veri kaynağı yapılandırılmadı" yazar |

**Hata davranışı:** Her başarılı yanıt `external_snapshots` tablosuna yazılır. Kaynak çökerse son başarılı veri **"eski"** etiketiyle, kaynak adı ve son güncelleme zamanıyla gösterilir. Hiç veri yoksa "Kaynağa şu anda ulaşılamıyor" yazar. Hata durumu 2 dakika önbelleklenir, kaynak sürekli zorlanmaz.

> **Lisans uyarısı:** Open-Meteo'nun ücretsiz kullanımı ticari olmayan amaçlar içindir. Şirket kullanımında Open-Meteo'nun ücretli planı veya başka bir sağlayıcı yapılandırılmalıdır (`IWeatherProvider` arayüzü). Gram altın için `IGoldPriceProvider` uygulanıp DI'da kaydedilmesi yeterlidir.

## 6. API

| Uç | Yetki | Açıklama |
|---|---|---|
| `GET /api/public/landing` | Herkese açık | Ana sayfanın tüm verisi (2 dk önbellek) |
| `GET /api/public/site` | Herkese açık | Site metinleri ve bağlantılar |
| `GET /api/public/news?page=` · `GET /api/public/news/{slug}` | Herkese açık | Yayımlanmış haberler |
| `GET /api/public/jobs` · `GET /api/public/jobs/{id}` | Herkese açık | Açık pozisyonlar |
| `GET /api/public/departments` · `GET /api/public/announcements` | Herkese açık | Tanıtılan departmanlar, herkese açık duyurular |
| `GET /api/public/ads?page=Landing\|Portal` | Herkese açık | Yayındaki reklamlar |
| `POST /api/public/ads/impressions` | Herkese açık | `{ ids: [..] }` gösterim sayımı |
| `GET /api/public/ads/{id}/click` | Herkese açık | Tıklama sayımı + 302 yönlendirme |
| `GET /api/public/media/{key}` | Herkese açık | Yalnızca içerik yönetimiyle yüklenmiş görseller (talep ekleri buradan açılamaz) |
| `GET /api/public/cities` · `weather?city=` · `rates` · `gold` | Herkese açık | Canlı veri (`status`: live / stale / not_configured / unavailable, `source`, `fetchedAt`) |
| `GET/POST/PUT/DELETE /api/content/ads[/{id}]`, `GET /api/content/ads/{id}/stats` | `ads.manage` | Reklam yönetimi |
| `GET/POST/PUT/DELETE /api/content/news[/{id}]`, `.../jobs[/{id}]` | `content.manage` | Haber, ilan |
| `PUT /api/content/settings` | `content.manage` | Site metinleri |
| `POST /api/content/media` | `content.manage` veya `ads.manage` | Görsel yükleme |

Herkese açık uçlar IP başına dakikada 300 istekle sınırlıdır (`public` hız sınırı politikası).

## 7. Veri modeli

`news_articles`, `advertisements` (bitiş > başlangıç kontrol kısıtı), `ad_stats_daily` (reklam + gün birincil anahtar), `job_openings`, `site_settings` (anahtar/değer), `media_files`, `external_snapshots`. Ayrıca `announcements.is_public` ve `departments.show_on_website`. Migration: `KurumsalSite`.

## 8. Testler

- **Birim testleri** (`SiteTests`): yalnızca yayındaki reklam gösterilir; gösterim ve tıklama yalnızca yayındaki reklam için sayılır; güvenli bağlantı kuralı; doğrulayıcı (`javascript:`, ters tarih, HTML); slug üretimi ve tekilliği; taslak ve ileri tarihli haber 404; reklam ve içerik izinlerinin ayrılığı; yüklenmemiş görselle reklam reddi; medya ucunun yalnızca kayıtlı görselleri sunması; canlı verinin son başarılı veriye düşmesi; altının "yapılandırılmadı" dönmesi; TCMB ve Open-Meteo ayrıştırıcıları.
- **Smoke** (`scripts/smoke-test.sh`, "Kurumsal site" bölümü): oturumsuz ana sayfa, kişisel veri olmaması, taslak 404, hava/kur/altın durumları, tıklama yönlendirmesi, içerik yönetiminin 401/403/200 davranışı, `javascript:` bağlantısının reddi.

## 9. Bilinen eksikler

- Gram altın veri sağlayıcısı (lisanslı kaynak ve anahtar gerekiyor).
- Open-Meteo'nun ticari kullanım için ücretli planı veya başka bir sağlayıcı.
- Açık temalı karşılama sitesi ekran görüntüsü ulaşmadı. Tasarım promptun tarifine (açık zemin, lacivert üst menü, kart düzeni) göre yapıldı; görüntü gelince renk ve yerleşim ona göre ayarlanabilir.
- Portal içi kişisel alanlar (doğum günleri, çalışan başarıları, haftanın çalışanı) Aşama 5'te portal paneline gelecek.
