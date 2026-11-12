# Sosyal akış, mesajlaşma ve SSS (Aşama 6.3)

## 1. Sosyal akış

**Ekran:** Genel → **Sosyal akış** (`/social`). Panelde "Sosyal akış" kutucuğu.

| Kural | Açıklama |
|---|---|
| Kim görür | Yalnızca giriş yapmış çalışanlar. Kurumsal sitede hiç görünmez |
| Paylaşım | `social.post` (tüm varsayılan rollerde). Metin en fazla 2000 karakter ve/veya tek görsel (PNG, JPG, WEBP; en fazla 3 MB; içerik imzası doğrulanır). HTML etiketleri temizlenir. Kişi başı saatte en fazla 10 paylaşım |
| Beğeni / yorum | Beğeni aç-kapa. Yorum en fazla 1000 karakter; paylaşım sahibine bildirim gider |
| Silme | Kişi kendi paylaşımını ve yorumunu kalıcı olarak siler |
| Şikayet | Uygunsuz paylaşım gerekçeyle bildirilir. Aynı kişinin tekrar bildirimi sayılmaz. Moderatörlere bildirim gider. Bildireni paylaşım sahibi görmez |
| Moderasyon | `social.moderate`: Moderasyon sekmesinde şikayetleri "yersiz" bulur ya da paylaşımı kaldırır. Kaldırma gerekçesi zorunludur ve paylaşım sahibine bildirilir. Moderatör yorum da kaldırabilir. Her işlem denetim kaydı bırakır |
| Görsel | Yetki denetimli uçtan, oturum bilgisiyle indirilir. Doğrudan bağlantıyla açılmaz |

## 2. Mesajlaşma

**Ekran:** Genel → **Mesajlar** (`/messages`). Üst çubukta okunmamış sayısıyla mesaj ikonu; profilde "Mesaj gönder".

- **Kapsam ve gizlilik:** Birebir konuşma, yalnızca aktif çalışanlar arasında. Mesajı yalnızca iki taraf görür; üçüncü kişi istese bile boş liste döner.
- **Anlık iletim:** Yeni mesaj SignalR ile anında gelir. Karşı taraf mesajlar sayfasında değilse kısa bir bildirim görür. Bağlantı yoksa 20–30 saniyede bir yenilenir.
- **Okundu bilgisi:** Konuşma açılınca mesajlar okundu işaretlenir. Gönderen çift tik (✓✓) görür.
- **Sınırlar:** Mesaj en fazla 2000 karakter; kişi başı dakikada en fazla 30 mesaj.

## 3. Sık sorulan sorular

**Ekran:** Genel → **Sık sorulan sorular** (`/faq`); üst çubuktaki yardım ikonu.

- **Okuma:** Herkes kategorilere ayrılmış soruları okur ve arar.
- **Yönetim:** `faq.manage` (varsayılan: destek personeli ve sistem yöneticisi) soru ekler, düzenler, taslağa alır ve siler.
- **Başlangıç içeriği:** İlk kurulumda 6 soru eklenir (hesap, destek talepleri, izin).

## 4. API

| Uç | Yetki |
|---|---|
| `GET /api/social/posts` · `GET .../{id}/comments` · `GET .../{id}/image` | Oturum |
| `POST /api/social/posts` (multipart: `body`, `image`) · `POST .../{id}/like` · `POST .../{id}/comments` | `social.post` |
| `DELETE /api/social/posts/{id}` · `DELETE /api/social/comments/{id}` | Sahibi (yorumda moderatör de) |
| `POST /api/social/posts/{id}/report` | Oturum |
| `POST /api/social/posts/{id}/remove` · `GET /api/social/reports` · `POST /api/social/reports/{id}/resolve` | `social.moderate` |
| `GET /api/messages/conversations` · `GET /api/messages/unread-count` | Oturum |
| `GET/POST /api/messages/with/{userId}` · `POST .../read` | Oturum (yalnızca kendi konuşmaları) |
| `GET /api/faq?q=` | Oturum |
| `POST/PUT/DELETE /api/faq[/{id}]` | `faq.manage` |

## 5. Veri modeli

`social_posts`, `social_comments`, `social_likes` (paylaşım + kullanıcı), `social_reports`, `conversations` (katılımcı çifti tekil; `user_a_id < user_b_id` kısıtı), `direct_messages`, `faq_items`. Migration: `SosyalMesajSss`.

## 6. Bu aşamada düzeltilen önemli hata

**Yeni izinler önbellek süresi dolana kadar görünmüyordu.** Sürümle gelen yeni izin rollere eklense de kullanıcıların Redis'teki izin listesi 5 dakika boyunca eski kalıyordu (yeni ekranlar 403 veriyordu). Başlangıçtaki izin eşitleme artık yeni izin eklediğinde izin önbelleğini temizliyor.

## 7. Testler

`SocialMessagingTests` (8 test):
- Beğeni, yorum ve bildirim; başkasının paylaşımını silememe.
- HTML'in temizlenmesi, boş paylaşım ve saatlik sınır.
- Görselin gerçekten görsel olması.
- Şikayetin moderatöre ulaşması; kaldırmanın gerekçe ve yetki istemesi; kaldırılan paylaşıma yorum yapılamaması.
- Yersiz şikayette paylaşımın kalması; moderatörün yorum kaldırması.
- Mesajların gizliliği, okunmamış sayısı, anlık sinyal ve okundu bildirimi.
- Kendine, pasif hesaba ve hızlı mesaj gönderiminin reddi.
- SSS araması; taslağı yalnızca yöneticinin görmesi.

Smoke testinde "Sosyal akış, mesajlaşma, SSS" bölümü (15 kontrol). Test kendi paylaşımını moderasyonla kaldırarak temizler.

## 8. Bilinen eksikler

- Grup sohbeti ve mesajda dosya ekleme yok (yalnızca birebir metin).
- Sosyal akışta paylaşım düzenleme yok (silip yeniden paylaşılır).
