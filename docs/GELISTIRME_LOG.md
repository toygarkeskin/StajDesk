# Geliştirme Günlüğü

Yapılan geliştirmeler, karşılaşılan hatalar ve çözümleri, tamamlanan aşamalar. (Stajyerin kişisel öğrenme günlüğü kök dizindeki `LOG.md`'dir.)

## 12.11.2026 — Panel, bonus görevler ve portal analizi

**Tamamlanan** (commit `b465df2`)
- Portal tarzı ana sayfa, duyurular, nöbet çizelgesi, dosya ekleri, SignalR anlık bildirim, tam metin arama.
- Postman koleksiyonu, PANEL.md, SUNUM.md.

**Hatalar ve çözümler**
| Hata | Neden | Çözüm |
|---|---|---|
| Dosya yükleme 500 | Depolama alt klasörü oluşturulmuyordu | `Directory.CreateDirectory` |
| Seq'te 400/401 hatalar "500" görünüyordu | İstek logu hata yakalayıcının içindeydi | `UseSerilogRequestLogging` öne alındı |
| Yeni sürüm yayınlanınca eski ekran | `index.html` tarayıcı önbelleğindeydi | nginx `expires -1` |
| Telefonda sayfa uzaklaştırılmış görünüyordu | Tablo içindeki `sr-only` etiketi kaydırma kutusundan taşıyordu | `.overflow-x-auto { position: relative }` |
| İki sekmede oturum düşüyordu | Eşzamanlı yenileme "token çalınması" sanılıyordu | 20 sn tolerans (ADR-013) |

**Testler:** 51 birim, 33 uçtan uca, 12 sayfa × 375 px taşma kontrolü.

**Aşama 1 (analiz) tamamlandı:** `docs/PORTAL_ANALIZ.md`. Kararlar: React, Form + İş Akışı motoru, açık sitede yalnızca kurumsal içerik, TCMB + Open-Meteo (altın anahtar bekliyor). Sıradaki: Aşama 3 — kimlik ve organizasyon.

## 12.11.2026 — Aşama 3: Kimlik ve organizasyon

**Tamamlanan**
- Departman ağacı, unvanlar (seviye), yönetici ilişkisi, personel kartı (1:1), kullanıcı tercihleri.
- İK Personeli rolü: personel kaydı ve iş bilgileri; sistem rolü veremez (unvan ≠ rol).
- Alan bazlı gizlilik: kişisel bilgiler API'den yalnızca kişiye ve İK'ya döner.
- Kullanıcı adıyla giriş, e-posta doğrulama, davetle ilk şifre, şifre değiştirme.
- SMS ile iki adımlı doğrulama akışı (sağlayıcı arayüzü; sağlayıcı bağlı olmadığı için kapalı ve bu açıkça gösteriliyor).
- Profil/kapak fotoğrafı (yetkili istekle gösterilir), çalışan rehberi, organizasyon şeması, departman/unvan yönetimi.
- Migration eski serbest metin departmanları yeni tabloya taşıdı; mevcut hesaplar doğrulanmış sayıldı.

**Hatalar ve çözümler**
| Hata | Neden | Çözüm |
|---|---|---|
| Uçtan uca testte toplu 401/429 | IP başına dakikada 30 kimlik isteği; her sayfa açılışı token yeniliyor, mağazada herkes aynı IP | Sınır 120 ve yapılandırılabilir; kaba kuvvet koruması hatalı deneme sayacında |
| Bellek içi test DB'si `ExecuteUpdate` desteklemiyor | Oturum iptali toplu güncellemeydi | İdempotent iptal normal yükle-kaydet; tek kullanımlık kod tüketimi atomik kaldı |
| Doğrulanmamış kayıtlar rehberde | Rehber tüm aktif hesapları listeliyordu | İK dışındakilere yalnızca doğrulanmış veya davetli hesaplar |

**Testler:** 66 birim (15 yeni: kullanıcı adı, doğrulama, davet, OTP, 5 deneme kilidi, sağlayıcısız durum, gizlilik, İK sınırları, döngüler), 45 uçtan uca, 9 yeni sayfa × 375 px taşma kontrolü.

**Eksik / sonraki:** Aşama 4 — kurumsal karşılama sitesi (açık tema referans görüntüsü bekleniyor), reklam/banner yönetimi, hava/kur servisleri.

## 12.11.2026 — Aşama 3 revizyonu: kapalı portal ve kurumsal hesap süreci

İstek: çalışanlar kendi hesabını açamasın; İK talep açsın, BT/yönetici onaylayıp hesabı açsın, rol versin, kilitlesin.

**Yapılan**
- Kendi kendine kayıt (ekran + API + e-posta doğrulama akışı) kaldırıldı.
- Hesap talepleri: yeni hesap ve hesap kapatma; İK açar, BT onaylar / gerekçeyle reddeder; her iki tarafa bildirim.
- BT Hesap Yöneticisi rolü; hesap kilitleme/kilit açma, davet yenileme.
- Yetki yükseltme koruması (yalnızca sahip olunan yetkiler kadar rol), görevler ayrılığı (kendi talebini onaylayamama).
- İK'dan doğrudan hesap açma ve aktif/pasif yetkisi alındı; ayrılış tarihi girilince BT'ye otomatik kapatma talebi.

**Doğrulama:** 74 birim testi (9 yeni), 50 uçtan uca kontrol; arayüzde İK talebi → BT onayı → davet e-postası → çalışanın ilk şifresi ve girişi denendi.

**Bekleyen:** departman / unvan CSV dosyası ulaşmadı.
**Ara verilen:** Aşama 4 (karşılama sitesi) yarım işi `phase4-wip-landing-20261112` adlı stash'te; bu revizyondan sonra devam edilecek.

## 12.11.2026 — CSV ile toplu çalışan ekleme

**Yapılan:** Departmanlar ve unvanlar ekranına "CSV ile çalışan ekle". Kapalı portal kuralı korundu: CSV hesap açmaz; her geçerli satır BT onayına düşen bir hesap talebi olur. Önce önizleme (satır satır hata), sonra gönderim. Eksik departman ve unvan yalnızca `org.manage` ile oluşturulur. `;` ve `,` ayırıcı, UTF-8 ve Windows-1254 desteklenir.

**Hata → çözüm:** Kullanıcı gerçek dosyasında "en fazla 500 satır" hatası aldı. Excel dosya sonuna tamamen boş satırlar (`;;;;`) ekliyor ve bunlar sayılıyordu. Boş satırlar ayrıştırmada atlanıyor; sınır 5.000 satır / 5 MB'a çıktı. Önizleme tüm hatalı satırları ve geçerlilerin ilk 200'ünü gösteriyor.

**Doğrulama:** 10 birim testi (600 satır + 2.000 boş satır, sınır aşımı, kodlama, tekrarlar, yetki); smoke testine 3 kontrol.

## 12.11.2026 — Aşama 4: Kurumsal karşılama sitesi

**Yapılan**
- Herkese açık site (`/`): lacivert üst menü, mobil çekmece, dönen banner, istatistikler, duyurular, haberler, departmanlar, açık pozisyonlar, kurumsal bağlantılar, footer. Alt sayfalar: Hakkımızda, Departmanlarımız, Uygulamalar, Duyurular, Haberler (+detay), Kariyer (+detay, arama), İletişim, 404.
- Portal paneli `/` → `/portal` adresine taşındı; "Portala Git" oturum yoksa girişe yönlendirir.
- İçerik yönetimi (`/admin/content`): reklam/banner (görsel yükleme, sayfa, konum, sıra, tarih aralığı, aktif/pasif, gösterim, tıklama, oran, son 30 gün), haber, açık pozisyon, site metinleri ve kurumsal bağlantılar. İzinler: `ads.manage`, `content.manage`.
- Canlı veri: Open-Meteo hava durumu (şehir seçilebilir), TCMB USD/EUR/GBP. Sunucu üzerinden alınır, önbelleklenir, son başarılı veri saklanır, kaynak ve zaman gösterilir. Gram altın "yapılandırılmadı" (sahte veri yok). Kartlar portal panelinde de var; portal reklamları panelde gösteriliyor.

**Hata → çözüm**
- Hesap kapatma onayı 500 veriyordu: işe başlama tarihi ileride (1 Aralık) olan çalışan için bugünkü ayrılış tarihi `ck_employee_profiles_dates` kuralını ihlal ediyordu. "İşe başlamadan iptal" olarak ele alındı: hesap kapanır, ayrılış tarihi işe başlama tarihine eşitlenir, istenen tarih denetim kaydında kalır. Birim testi eklendi.
- Banner görseli ilk ekranda geç görünüyordu (tembel yükleme): üst banner hemen yükleniyor.
- Mobilde banner düğmeleri açıklama metninin üstüne biniyordu: metin yukarı hizalandı.
- 5432 portu başka bir projenin PostgreSQL'i tarafından kullanılıyordu: bu ortamda `POSTGRES_PORT=5433` ile çalıştırıldı (`.env.example`'da belgelenmiş ayar).

**Doğrulama:** 111 birim testi; tip kontrolü + derleme; tarayıcıda masaüstü ve mobil (375 px, yatay taşma yok), hamburger menü, haber/ilan detayı, taslak haber 404, içerik yönetimi formu ve doğrulaması, portal paneli. Smoke: kurumsal site kontrollerinin demo hesap gerektirmeyenleri geçti. Demo hesapları (ayse, deniz.ik, kaan…) ortamda pasife alındığı için giriş gerektiren kontroller bu çalıştırmada doğrulanamadı.

**Bekleyen:** gram altın sağlayıcısı, Open-Meteo ticari planı, açık tema ekran görüntüsü, departman/unvan CSV'si.
