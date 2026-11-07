# Güvenlik

## XSS savunması (katmanlı)

| Katman | Ne yapılıyor? |
|---|---|
| Girdi | Başlık, ad, kategori gibi tek satırlık alanlarda `NoMarkup()` doğrulayıcısı `<`, `>` ve HTML benzeri içeriği reddeder (400). |
| Temizlik | `Text.Clean` kontrol karakterlerini ve görünmez Unicode'u siler, boşlukları düzenler. |
| Çıktı | React her metni otomatik escape eder; projede `dangerouslySetInnerHTML` **kullanılmaz**. E-posta şablonları `WebUtility.HtmlEncode` ile kodlanır. |
| Tarayıcı | Sıkı CSP: arayüzde `default-src 'self'; script-src 'self'; frame-ancestors 'none'`, API yanıtlarında `default-src 'none'`. Inline script yoktur (tema betiği `public/theme.js`). |
| Token | Access token yalnızca bellekte, refresh token `HttpOnly` cookie'de. XSS olsa bile kalıcı oturum çalınamaz. |

## Güvenlik başlıkları

nginx ve API: `Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` ve
`Referrer-Policy` (arayüzde `strict-origin-when-cross-origin`, API'de `no-referrer`).

## Tip güvenliği ve veri bütünlüğü

- Backend: nullable reference types açık (`Directory.Build.props`); DTO'lar `record`, enum'lar metin olarak saklanır.
- Frontend: TypeScript `strict`. API tipleri `lib/types.ts` içinde, izin anahtarları birleşik tip (`Permission`) — yanlış yazılan izin derlenmez.
- Veritabanı: yabancı anahtarlar, unique indeksler ve bilinçli `ON DELETE` kuralları (bkz. [ARCHITECTURE.md](ARCHITECTURE.md#3-veri-modeli)).
- Komutlar tek transaction'da çalışır; olaylar outbox ile aynı transaction'a yazılır.

## Denetim kayıtları (`audit_logs`)

Kim, ne zaman, hangi IP'den, ne yaptı? Kaydedilen olaylar:

`auth.login`, `auth.login_failed`, `auth.logout`, `auth.refresh_reuse_detected`, `user.registered`,
`user.password_reset_requested`, `user.password_reset`, `user.created`, `user.updated`,
`role.permissions_changed`, `ticket.created`, `ticket.deleted`, `asset.created`, `asset.updated`, `asset.deleted`.

Şifre ve token **asla** loglanmaz.

## Kontrol listesi

- [x] Şifreler PBKDF2 ile salt'lı hash; güçlü şifre kuralı
- [x] OAuth 2.0 + PKCE (S256), implicit / password grant yok
- [x] Refresh token rotasyonu + yeniden kullanım tespiti
- [x] Token'lar, kodlar ve sıfırlama bağlantıları DB'de yalnızca hash olarak
- [x] Kaba kuvvet koruması (e-posta, IP, şifre sıfırlama)
- [x] Hesap varlığı sızmaz (şifremi unuttum her zaman aynı yanıt, giriş hatası e-posta/şifre ayrımı yapmaz)
- [x] Pasifleştirilen kullanıcı bir sonraki istekte engellenir (token süresini beklemez)
- [x] Gizli değerler `.env` / ortam değişkeninde; imza anahtarı volume'da, depoda değil
- [x] 500 hatalarında iç ayrıntı kullanıcıya gösterilmez, Seq'e yazılır
- [x] CORS yalnızca tanımlı origin'ler, credentials ile
- [ ] Üretimde HTTPS + `OAuth:SecureCookies=true` (demo ortamı HTTP)
