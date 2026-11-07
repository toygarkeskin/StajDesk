# Mimari Karar Kayıtları

Her kayıt: bağlam → karar → sonuç. Yeni bir karar alındığında en alta eklenir; eski kayıt silinmez, gerekirse "yerini aldı" notu düşülür.

## ADR-001 — Clean Architecture, tek çözüm, altı proje
**Bağlam:** Stajyer projesi; okunabilirlik ve test edilebilirlik önemli, ekip küçük.
**Karar:** Domain / Contracts / Application / Infrastructure / Api / Worker. Bağımlılıklar içe doğru.
**Sonuç:** İş kuralları altyapıdan bağımsız test edilir. Mikroservis karmaşası yok; Worker yalnızca kuyruk tüketicisi.

## ADR-002 — CQRS yalnızca Talepler modülünde, kütüphanesiz
**Bağlam:** Talepler en karmaşık modül (durum akışı, atama, kapsam, olaylar). Kategori/mağaza basit CRUD.
**Karar:** Talepler için küçük bir `IDispatcher` + handler yapısı. MediatR kullanılmadı (lisans değişikliği ve öğrenme amacı). Diğer modüller servis olarak kaldı.
**Sonuç:** Her handler tek sorumluluklu ve ayrı test edilir. CRUD modüllerine gereksiz tören eklenmedi.

## ADR-003 — Kendi OAuth 2.0 sunucumuz
**Bağlam:** OAuth 2.0 uyumu isteniyor; harici IdP (Keycloak vb.) demo ortamını ağırlaştırır.
**Karar:** Authorization Code + PKCE, refresh rotasyonu, client_credentials, revoke, metadata ve JWKS'yi API içinde uygulamak. Giriş ekranı first-party olduğu için onay ekranı yok.
**Sonuç:** Tek `docker compose` ile çalışır. İleride harici IdP'ye geçmek istenirse SPA zaten standart akışı kullandığı için değişiklik küçüktür.

## ADR-004 — Access token bellekte, refresh token httpOnly cookie'de
**Bağlam:** `localStorage`'daki token XSS ile çalınabilir.
**Karar:** Access token 15 dk ve yalnızca bellekte; refresh token `HttpOnly; SameSite=Strict; Path=/connect` cookie'de.
**Sonuç:** Sayfa yenilenince sessiz refresh gerekir (kısa bir yükleme). CSRF riski SameSite=Strict + tek origin ile kapanır.

## ADR-005 — İzinler token'da değil, istekte yüklenir
**Bağlam:** Rol değişikliği veya hesap pasifleştirme token süresini beklememeli.
**Karar:** Token yalnızca kimlik taşır; izinler Redis'te 5 dk önbellekli, değişince silinir.
**Sonuç:** Her istekte bir Redis okuması. Karşılığında anında etkili yetki değişikliği.

## ADR-006 — Transactional outbox
**Bağlam:** "DB'ye yaz, sonra RabbitMQ'ya gönder" iki ayrı işlem; arada çökme olursa olay kaybolur.
**Karar:** Olay iş verisiyle aynı transaction'da `outbox_messages` tablosuna yazılır; arka plan servisi taşır. Worker `processed_messages` ile idempotent.
**Sonuç:** En az bir kez teslim + idempotent tüketim = fiilen tam bir kez etki. Birkaç saniyelik gecikme kabul edildi.

## ADR-007 — Mağaza kavramı ve veri kapsamı
**Bağlam:** "Mağaza Sahibi" rolü kendi mağazasının verisini görmeli.
**Karar:** `stores` tablosu; kullanıcı, talep ve cihaz mağazaya bağlanır. Talep açılırken mağaza, açan kişinin mağazasından alınır. İzinler `own / store / all` üçlüsüyle kapsam belirtir.
**Sonuç:** Yeni bir "bölge" katmanı gerekirse aynı desenle eklenebilir.

## ADR-008 — Demo e-posta: Mailpit
**Bağlam:** E-postaların "gerçekten gönderilmiş gibi" görünmesi isteniyor, ama gerçek kişilere gitmemeli.
**Karar:** Worker MailKit ile SMTP'ye gönderir; demo ortamında SMTP sunucusu Mailpit. Her gönderim `sent_emails` tablosuna da yazılır.
**Sonuç:** Üretimde yalnızca `Smtp__*` ayarları değişir; kod aynı kalır.

## ADR-009 — Arayüz: önceki Mosaic görünümü + yeni UX davranışları
**Bağlam:** UX yenilemesinden sonra eski görünüm daha çok beğenildi.
**Karar:** Mosaic Lite benzeri görsel dil korunurken yeni davranışlar (filtre çipleri, kaydedilmemiş değişiklik uyarısı, klavye kısayolları, iskelet yükleyiciler) tutuldu.
