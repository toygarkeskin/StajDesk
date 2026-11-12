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
