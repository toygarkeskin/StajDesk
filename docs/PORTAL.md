# Çalışan portalı (Aşama 5)

Giriş sonrası portalın çekirdeği: kutucuk (widget) sistemi, menü, genel arama ve kurumsal akışlar.

## 1. Kutucuk sistemi

Ana sayfa (`/portal`) bağımsız kutucuklardan oluşur. Bir kutucuğun görünmesi için **üç kural birlikte** sağlanmalıdır:

1. **İzin (kodda, değiştirilemez):** Kutucuğun gerektirdiği izin varsa kullanıcıda olmalıdır. Örneğin "Hızlı kişi arama" için `tickets.manage`, `users.manage` veya `assets.manage`; "Bekleyen hesap talepleri" için `accounts.request` veya `users.manage`. Yönetici bu kuralı gevşetemez.
2. **Şirket ayarı (yönetici, `widgets.manage`):** Portal → Yönetim → **Kutucuk yönetimi**. Kutucuk başına açık/kapalı, varsayılan sıra, boyut (1/3, 1/2, 2/3, tam satır) ve hangi rollerin göreceği ayarlanır. Rol seçilmezse herkes görür.
3. **Kişisel tercih (her kullanıcı):** Ana sayfa → **Panelimi düzenle**. Kullanıcı sırayı değiştirir, kutucuk gizler/gösterir, **Varsayılana dön** ile tercihlerini siler. Yalnızca görebildiği kutucuklar kaydedilir; bilinmeyen veya yetkisiz anahtarlar yok sayılır.

Görünürlük sunucuda hesaplanır (`GET /api/dashboard/layout`); arayüz yalnızca gelen listeyi çizer. Böylece CEO, müdür, çalışan ve stajyer farklı paneller görür.

| Kutucuk | Kategori | İzin şartı |
|---|---|---|
| Karşılama, Profil özeti | Kişisel | — |
| Duyurular, BT nöbetçi personel, Doğum günleri, Aramıza katılanlar, Takdirler, Şirket etkinlikleri, Açık pozisyonlar, Portal bannerları | Kurumsal | — |
| Hava durumu, Döviz kurları, Gram altın | Bilgi | — |
| Talep sayaçları, Onayımı bekleyenler, Görevlerim / taleplerim, Son hareketler, Talep eğilimi, Talep dağılımı | Destek | — (içerik yetki kapsamına göre süzülür) |
| Hızlı kişi arama | Yönetim | `tickets.manage` / `users.manage` / `assets.manage` |
| Bekleyen hesap talepleri | Yönetim | `accounts.request` / `users.manage` |

## 2. Kurumsal akışlar (yalnızca giriş sonrası)

| Kutucuk | Kaynak | Kural |
|---|---|---|
| Doğum günleri | Profil doğum tarihi | **Açık rıza:** yalnızca Profil → Bildirim ve gizlilik → "Doğum günüm portalda görünsün" seçeneğini açanlar. Varsayılan kapalı. Yalnızca gün ve ay döner, yaş hiçbir yanıtta yoktur. Önümüzdeki 7 gün gösterilir |
| Aramıza katılanlar | Profil işe başlama tarihi | Son 30 gün, aktif hesaplar |
| Takdirler | Kurumsal site ve içerik → Takdirler | Haftanın çalışanı öne çıkar; son 60 gün. Yönetim: `content.manage` veya İK (`employees.manage`) |
| Şirket etkinlikleri | Kurumsal site ve içerik → Etkinlikler | Yayımlanmış ve bitmemiş etkinlikler. Yönetim: `content.manage` |
| Açık pozisyonlar | Kurumsal site ilanları | Kariyer sayfasıyla aynı kaynak |

Bu kişisel içerikler kurumsal sitede (giriş öncesi) gösterilmez (KVKK kararı).

## 3. Menü

- Menü tek bir kayıttan (`src/frontend/src/lib/nav.tsx`) üretilir: kenar çubuğu ve genel aramanın "Sayfalar" grubu aynı listeyi kullanır.
- Gruplar (Genel, Destek Talep Sistemi, Çalışanlar, Mağaza ve operasyon, Yönetim) açılıp kapanır. Etkin sayfanın grubu her zaman açıktır. Tercih tarayıcıda saklanır.
- Masaüstünde **Menüyü daralt** ile yalnızca ikonlar kalır; dar ekranda menü çekmecede açılır.
- Menü öğeleri **izne göre** görünür. Henüz yapılmamış uygulamalar (izin, masraf, iş akışı vb.) menüde **yoktur**; boş sayfa gösterilmez. Bu uygulamalar Aşama 6 ve 7'de eklendikçe menüye girer.

## 4. Genel arama

`Ctrl+K` veya üst çubuktaki arama alanı. Klavyeyle gezilir (↑ ↓, Enter, Esc).

| Grup | Kaynak | Yetki |
|---|---|---|
| Sayfalar | Menü kaydı | Yalnızca kullanıcının menüsünde olan sayfalar |
| Kişiler | `GET /api/search` | Rehber kuralı: aktif hesaplar (İK dışındakiler için davet bekleyen veya doğrulanmış) |
| Departmanlar | `GET /api/search` | Aktif departmanlar |
| Duyurular | `GET /api/search` | Yayında olan; mağaza duyuruları yalnızca o mağazaya (yönetici hepsini görür) |
| Talepler | `GET /api/search` | Talep görünürlük kapsamı (kendi / mağaza / tümü); `#numara` ile doğrudan |

En az 2 karakter; her grupta en fazla 5–6 sonuç. Sonda "Taleplerde tam metin ara" seçeneği PostgreSQL tam metin aramasına götürür.

## 5. API

| Uç | Yetki |
|---|---|
| `GET /api/dashboard/layout` | Oturum |
| `PUT /api/dashboard/preferences` (`{ order, hidden }`) · `DELETE /api/dashboard/preferences` | Oturum (yalnızca kendi düzeni) |
| `GET /api/admin/widgets` · `PUT /api/admin/widgets/{key}` | `widgets.manage` |
| `GET /api/portal/birthdays` · `new-hires` · `events` · `recognitions` | Oturum |
| `GET /api/search?q=` | Oturum |
| `GET/POST/PUT/DELETE /api/content/portal/events[/{id}]` | `content.manage` |
| `GET/POST/PUT/DELETE /api/content/portal/recognitions[/{id}]` | `content.manage` veya `employees.manage` |

## 6. Veri modeli

`dashboard_widgets` (anahtar, açık, sıra, boyut, roller `text[]`), `user_dashboard_preferences` (kullanıcı + anahtar, sıra, gizli), `company_events` (bitiş ≥ başlangıç kısıtı), `recognitions`, `user_settings.show_birthday`. Migration: `PortalCekirdegi`.

## 7. Testler

`PortalTests` (11 test): izin gerektiren kutucuğun yönetici ayarıyla bile açılamaması; kapatma, boyut, rol hedefleme; yalnızca `widgets.manage`; kişisel sıra/gizleme ve başka kullanıcıyı etkilememe; doğum günü rızası ve 7 gün; yeni başlayanlar; aramanın talep ve duyuru kapsamına uyması; pasif hesabın aramada çıkmaması; etkinlik ve takdir yetkileri. Smoke testinde "Çalışan portalı" bölümü.

## 8. Bu aşamada bilerek yapılmayanlar

| Konu | Neden / ne zaman |
|---|---|
| Dil seçimi (TR/EN) | Bütün ekranların çevirisi gerekiyor. Yarım çeviri gösterilmedi; Aşama 9'da tüm ekranlarla birlikte yapılacak |
| Mesajlar ve Yardım ikonları | Mesajlaşma ve SSS modülleri Aşama 6'da; çalışmayan ikon eklenmedi |
| İzin bakiyesi kutucuğu | İzin sistemi Aşama 6'da; o zaman kutucuk kataloğa eklenecek |
| Sosyal akış kutucuğu | Sosyal akış Aşama 6'da |
| Ciro, mağaza performansı, departman performansı kutucukları | Ciro ve iş akışı verisi Aşama 7'de oluşacak |
| Yapay zekâ kullanım kartı | Gerçek bir yapay zekâ entegrasyonu yok; prompt gereği gösterilmiyor |
